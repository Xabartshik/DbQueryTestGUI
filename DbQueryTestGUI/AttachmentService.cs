using Microsoft.Data.Sqlite;
using System;
using System.Globalization;

namespace DbQueryTestGUI
{
    /// <summary>Единая точка работы с текущим прикреплением пациента.</summary>
    internal static class AttachmentService
    {
        internal sealed class AttachmentInfo
        {
            public bool IsAttached { get; init; }
            public string LpuCode { get; init; }
            public string LpuName { get; init; }
            public string SubdivCode { get; init; }
            public string SubdivName { get; init; }
            public string DistrictCode { get; init; }
            public string DistrictName { get; init; }
            public string AttachmentDate { get; init; }
            public string DetachmentDate { get; init; }
        }

        internal static AttachmentInfo GetCurrent(string dbPath, long personId)
        {
            using var connection = OpenConnection(dbPath);
            const string query = @"
                SELECT pe.lpu, pe.lpudt, pe.lpuuch, pe.lpudx,
                       l.caption, h.subdiv, h.district, t1.nam_mo, t7.name_depth
                FROM PEOPLE pe
                LEFT JOIN HISTLPU h ON h.id = (
                    SELECT current.id
                    FROM HISTLPU current
                    WHERE current.pid = pe.id
                      AND current.lpu = pe.lpu
                      AND ((current.lpudx IS NULL OR current.lpudx = '')
                           OR (pe.lpudx IS NOT NULL AND pe.lpudx <> ''))
                    ORDER BY CASE WHEN current.lpudx IS NULL OR current.lpudx = '' THEN 0 ELSE 1 END,
                             current.id DESC
                    LIMIT 1
                )
                LEFT JOIN LPU l ON l.code = pe.lpu
                LEFT JOIN T001 t1 ON t1.mcod = pe.lpu AND t1.nom_podr = h.subdiv
                LEFT JOIN T007 t7 ON t7.code_mo = pe.lpu
                                  AND t7.nom_podr = h.subdiv
                                  AND t7.depth = COALESCE(h.district, pe.lpuuch)
                WHERE pe.id = @personId;";

            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@personId", personId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Пациент не найден.");
            }

            string lpu = GetString(reader, 0);
            string detached = GetString(reader, 3);
            string district = GetString(reader, 6) ?? GetString(reader, 2);
            return new AttachmentInfo
            {
                IsAttached = !string.IsNullOrWhiteSpace(lpu) && string.IsNullOrWhiteSpace(detached),
                LpuCode = lpu,
                LpuName = GetString(reader, 4),
                SubdivCode = GetString(reader, 5),
                DistrictCode = district,
                SubdivName = GetString(reader, 7),
                DistrictName = GetString(reader, 8),
                AttachmentDate = GetString(reader, 1),
                DetachmentDate = detached
            };
        }

        internal static void SaveCurrent(
            string dbPath, long personId, string lpu, string subdiv, string district,
            string attachmentDate, string detachmentDate)
        {
            lpu = Normalize(lpu);
            subdiv = Normalize(subdiv);
            district = Normalize(district);
            attachmentDate = NormalizeDate(attachmentDate, "дата прикрепления");
            detachmentDate = NormalizeDate(detachmentDate, "дата открепления");

            if (lpu == null)
            {
                throw new InvalidOperationException("Выберите медицинскую организацию.");
            }
            if (subdiv == null || district == null)
            {
                throw new InvalidOperationException("Для прикрепления выберите подразделение и участок.");
            }
            if (attachmentDate == null)
            {
                throw new InvalidOperationException("Укажите дату прикрепления.");
            }
            if (detachmentDate != null && DateTime.ParseExact(detachmentDate, "dd.MM.yyyy", CultureInfo.InvariantCulture) < DateTime.ParseExact(attachmentDate, "dd.MM.yyyy", CultureInfo.InvariantCulture))
            {
                throw new InvalidOperationException("Дата открепления не может быть раньше даты прикрепления.");
            }

            using var connection = OpenConnection(dbPath);
            using var transaction = connection.BeginTransaction();

            EnsureReferenceExists(connection, transaction,
                "SELECT COUNT(*) FROM LPU WHERE code = @lpu;", "@lpu", lpu, "Медицинская организация не найдена.");
            EnsureReferenceExists(connection, transaction,
                "SELECT COUNT(*) FROM T001 WHERE mcod = @lpu AND nom_podr = @subdiv;", "@lpu", lpu, "Подразделение не относится к выбранной МО.", "@subdiv", subdiv);
            EnsureReferenceExists(connection, transaction,
                "SELECT COUNT(*) FROM T007 WHERE code_mo = @lpu AND nom_podr = @subdiv AND depth = @district;", "@lpu", lpu, "Участок не относится к выбранному подразделению.", "@subdiv", subdiv, "@district", district);

            long activeHistoryId = GetActiveHistoryId(connection, transaction, personId);
            if (detachmentDate == null)
            {
                if (activeHistoryId == 0)
                {
                    InsertHistory(connection, transaction, personId, lpu, subdiv, district, attachmentDate, null);
                }
                else
                {
                    UpdateHistory(connection, transaction, activeHistoryId, lpu, subdiv, district, attachmentDate, null);
                    CloseDuplicateActiveHistory(connection, transaction, personId, activeHistoryId, attachmentDate);
                }
            }
            else
            {
                if (activeHistoryId == 0)
                {
                    throw new InvalidOperationException("У пациента нет активного прикрепления для открепления.");
                }

                UpdateHistory(connection, transaction, activeHistoryId, lpu, subdiv, district, attachmentDate, detachmentDate);
                CloseDuplicateActiveHistory(connection, transaction, personId, activeHistoryId, detachmentDate);
            }

            const string updatePerson = @"
                UPDATE PEOPLE
                SET lpu = @lpu, lpudt = @attachmentDate, lpudx = @detachmentDate, lpuuch = @district
                WHERE id = @personId;";
            using var peopleCommand = new SqliteCommand(updatePerson, connection, transaction);
            peopleCommand.Parameters.AddWithValue("@lpu", lpu);
            peopleCommand.Parameters.AddWithValue("@attachmentDate", attachmentDate);
            peopleCommand.Parameters.AddWithValue("@detachmentDate", (object)detachmentDate ?? DBNull.Value);
            peopleCommand.Parameters.AddWithValue("@district", district);
            peopleCommand.Parameters.AddWithValue("@personId", personId);
            if (peopleCommand.ExecuteNonQuery() != 1)
            {
                throw new InvalidOperationException("Пациент не найден.");
            }

            transaction.Commit();
        }

        private static SqliteConnection OpenConnection(string dbPath)
        {
            var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            return connection;
        }

        private static long GetActiveHistoryId(SqliteConnection connection, SqliteTransaction transaction, long personId)
        {
            const string query = "SELECT id FROM HISTLPU WHERE pid = @personId AND (lpudx IS NULL OR lpudx = '') ORDER BY id DESC LIMIT 1;";
            using var command = new SqliteCommand(query, connection, transaction);
            command.Parameters.AddWithValue("@personId", personId);
            object value = command.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value);
        }

        private static void InsertHistory(SqliteConnection connection, SqliteTransaction transaction, long personId, string lpu, string subdiv, string district, string attachmentDate, string detachmentDate)
        {
            const string query = @"INSERT INTO HISTLPU (pid, lpu, lpudt, lpudx, district, subdiv)
                                   VALUES (@personId, @lpu, @attachmentDate, @detachmentDate, @district, @subdiv);";
            ExecuteHistoryCommand(connection, transaction, query, personId, lpu, subdiv, district, attachmentDate, detachmentDate);
        }

        private static void UpdateHistory(SqliteConnection connection, SqliteTransaction transaction, long historyId, string lpu, string subdiv, string district, string attachmentDate, string detachmentDate)
        {
            const string query = @"UPDATE HISTLPU SET lpu = @lpu, subdiv = @subdiv, district = @district,
                                   lpudt = @attachmentDate, lpudx = @detachmentDate WHERE id = @historyId;";
            using var command = new SqliteCommand(query, connection, transaction);
            command.Parameters.AddWithValue("@historyId", historyId);
            command.Parameters.AddWithValue("@lpu", lpu);
            command.Parameters.AddWithValue("@subdiv", subdiv);
            command.Parameters.AddWithValue("@district", district);
            command.Parameters.AddWithValue("@attachmentDate", attachmentDate);
            command.Parameters.AddWithValue("@detachmentDate", (object)detachmentDate ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        private static void ExecuteHistoryCommand(SqliteConnection connection, SqliteTransaction transaction, string query, long personId, string lpu, string subdiv, string district, string attachmentDate, string detachmentDate)
        {
            using var command = new SqliteCommand(query, connection, transaction);
            command.Parameters.AddWithValue("@personId", personId);
            command.Parameters.AddWithValue("@lpu", lpu);
            command.Parameters.AddWithValue("@subdiv", subdiv);
            command.Parameters.AddWithValue("@district", district);
            command.Parameters.AddWithValue("@attachmentDate", attachmentDate);
            command.Parameters.AddWithValue("@detachmentDate", (object)detachmentDate ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        private static void CloseDuplicateActiveHistory(SqliteConnection connection, SqliteTransaction transaction, long personId, long retainedId, string detachmentDate)
        {
            const string query = @"UPDATE HISTLPU SET lpudx = @detachmentDate
                                   WHERE pid = @personId AND id <> @retainedId AND (lpudx IS NULL OR lpudx = '');";
            using var command = new SqliteCommand(query, connection, transaction);
            command.Parameters.AddWithValue("@detachmentDate", detachmentDate);
            command.Parameters.AddWithValue("@personId", personId);
            command.Parameters.AddWithValue("@retainedId", retainedId);
            command.ExecuteNonQuery();
        }

        private static void EnsureReferenceExists(SqliteConnection connection, SqliteTransaction transaction, string query, string firstName, string firstValue, string error, string secondName = null, string secondValue = null, string thirdName = null, string thirdValue = null)
        {
            using var command = new SqliteCommand(query, connection, transaction);
            command.Parameters.AddWithValue(firstName, firstValue);
            if (secondName != null) command.Parameters.AddWithValue(secondName, secondValue);
            if (thirdName != null) command.Parameters.AddWithValue(thirdName, thirdValue);
            if (Convert.ToInt64(command.ExecuteScalar()) == 0) throw new InvalidOperationException(error);
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string NormalizeDate(string value, string fieldName)
        {
            value = Normalize(value);
            if (value == null) return null;
            if (!DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                throw new InvalidOperationException($"Поле «{fieldName}» должно содержать дату в формате ДД.ММ.ГГГГ.");
            }
            return date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }

        private static string GetString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
