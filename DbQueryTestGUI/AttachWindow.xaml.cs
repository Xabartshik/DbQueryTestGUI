using Microsoft.Data.Sqlite;
using System;
using System.Windows;
using System.Windows.Controls;

namespace DbQueryTestGUI
{
    public partial class AttachWindow : Window
    {
        private readonly string _dbPath;
        private readonly long _personId;
        private bool _isLoading = true;

        public class ComboBoxItemModel
        {
            public string Code { get; set; }
            public string Name { get; set; }
        }

        public AttachWindow(string dbPath, long personId, string fullName)
        {
            InitializeComponent();
            _dbPath = dbPath;
            _personId = personId;
            TxtPersonInfo.Text = $"Пациент: {fullName}";

            LoadLpuList();
            LoadCurrentData();
            _isLoading = false;
        }

        private string GetConnectionString() => $"Data Source={_dbPath}";

        /// <summary>
        /// Загрузка списка медицинских организаций (LPU)
        /// </summary>
        private void LoadLpuList()
        {
            CmbLpu.Items.Clear();
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                string query = "SELECT code, caption FROM LPU ORDER BY caption;";
                using var cmd = new SqliteCommand(query, connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string code = reader.GetString(0);
                    string caption = reader.IsDBNull(1) ? code : reader.GetString(1);
                    CmbLpu.Items.Add(new ComboBoxItemModel
                    {
                        Code = code,
                        Name = $"{code} - {caption}"
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки LPU: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Загрузка текущих данных пациента и установка выбранных элементов в ComboBox
        /// </summary>
        private void LoadCurrentData()
        {
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                string query = "SELECT lpu, lpudt, lpudx, lpuuch FROM PEOPLE WHERE id = @id;";
                using var cmd = new SqliteCommand(query, connection);
                cmd.Parameters.AddWithValue("@id", _personId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    string currentLpu = reader.IsDBNull(0) ? null : reader.GetString(0);
                    TxtLpuDt.Text = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    TxtLpuDx.Text = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    string currentDistrict = reader.IsDBNull(3) ? null : reader.GetString(3);

                    // Устанавливаем LPU
                    if (!string.IsNullOrEmpty(currentLpu))
                    {
                        foreach (ComboBoxItemModel item in CmbLpu.Items)
                        {
                            if (item.Code == currentLpu)
                            {
                                CmbLpu.SelectedItem = item;
                                break;
                            }
                        }
                    }

                    // Подтягиваем подразделения и пытаемся найти сохраненное subdiv из истории
                    if (!string.IsNullOrEmpty(currentLpu))
                    {
                        string subQuery = "SELECT subdiv FROM HISTLPU WHERE pid = @id AND lpu = @lpu ORDER BY rowid DESC LIMIT 1;";
                        using var subCmd = new SqliteCommand(subQuery, connection);
                        subCmd.Parameters.AddWithValue("@id", _personId);
                        subCmd.Parameters.AddWithValue("@lpu", currentLpu);
                        var subResult = subCmd.ExecuteScalar();

                        if (subResult != null && subResult != DBNull.Value)
                        {
                            string currentSubdiv = subResult.ToString();
                            LoadSubdivList(currentLpu, currentSubdiv);

                            if (!string.IsNullOrEmpty(currentSubdiv))
                            {
                                LoadDistrictList(currentLpu, currentSubdiv, currentDistrict);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки текущих данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSubdivList(string mcod, string selectSubdivCode = null)
        {
            CmbSubdiv.Items.Clear();
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                string query = "SELECT nom_podr, nam_mo FROM T001 WHERE mcod = @mcod ORDER BY nam_mo;";
                using var cmd = new SqliteCommand(query, connection);
                cmd.Parameters.AddWithValue("@mcod", mcod);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string code = reader.GetValue(0).ToString();
                    string name = reader.IsDBNull(1) ? code : reader.GetString(1);
                    var item = new ComboBoxItemModel
                    {
                        Code = code,
                        Name = $"{code} - {name}"
                    };
                    CmbSubdiv.Items.Add(item);

                    if (code == selectSubdivCode)
                    {
                        CmbSubdiv.SelectedItem = item;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки подразделений: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDistrictList(string mcod, string subdivCode, string selectDistrictCode = null)
        {
            CmbDistrict.Items.Clear();
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                string query = "SELECT depth, name_depth FROM T007 WHERE code_mo = @mcod AND nom_podr = @subdiv ORDER BY name_depth;";
                using var cmd = new SqliteCommand(query, connection);
                cmd.Parameters.AddWithValue("@mcod", mcod);
                cmd.Parameters.AddWithValue("@subdiv", subdivCode);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string code = reader.GetValue(0).ToString();
                    string name = reader.IsDBNull(1) ? code : reader.GetString(1);
                    var item = new ComboBoxItemModel
                    {
                        Code = code,
                        Name = $"{code} - {name}"
                    };
                    CmbDistrict.Items.Add(item);

                    if (code == selectDistrictCode)
                    {
                        CmbDistrict.SelectedItem = item;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки участков: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbLpu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            CmbSubdiv.Items.Clear();
            CmbDistrict.Items.Clear();

            if (CmbLpu.SelectedItem is ComboBoxItemModel selectedLpu)
            {
                LoadSubdivList(selectedLpu.Code);
            }
        }

        private void CmbSubdiv_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            CmbDistrict.Items.Clear();

            if (CmbLpu.SelectedItem is ComboBoxItemModel selectedLpu &&
                CmbSubdiv.SelectedItem is ComboBoxItemModel selectedSubdiv)
            {
                LoadDistrictList(selectedLpu.Code, selectedSubdiv.Code);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string lpu = (CmbLpu.SelectedItem is ComboBoxItemModel lpuItem) ? lpuItem.Code : null;
                string subdiv = (CmbSubdiv.SelectedItem is ComboBoxItemModel subItem) ? subItem.Code : null;
                string district = (CmbDistrict.SelectedItem is ComboBoxItemModel distItem) ? distItem.Code : null;

                string lpudt = string.IsNullOrWhiteSpace(TxtLpuDt.Text) ? null : TxtLpuDt.Text.Trim();
                string lpudx = string.IsNullOrWhiteSpace(TxtLpuDx.Text) ? null : TxtLpuDx.Text.Trim();

                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                using var transaction = connection.BeginTransaction();

                // 1. Обновляем основную таблицу PEOPLE
                string updatePeopleQuery = @"
                    UPDATE PEOPLE 
                    SET lpu = @lpu, lpudt = @lpudt, lpudx = @lpudx, lpuuch = @lpuuch 
                    WHERE id = @id;";

                using var cmdPeople = new SqliteCommand(updatePeopleQuery, connection, transaction);
                cmdPeople.Parameters.AddWithValue("@lpu", (object)lpu ?? DBNull.Value);
                cmdPeople.Parameters.AddWithValue("@lpudt", (object)lpudt ?? DBNull.Value);
                cmdPeople.Parameters.AddWithValue("@lpudx", (object)lpudx ?? DBNull.Value);
                cmdPeople.Parameters.AddWithValue("@lpuuch", (object)district ?? DBNull.Value);
                cmdPeople.Parameters.AddWithValue("@id", _personId);
                cmdPeople.ExecuteNonQuery();

                // 2. Добавляем запись в историю HISTLPU при наличии LPU
                if (!string.IsNullOrEmpty(lpu))
                {
                    string historyQuery = @"
                        INSERT INTO HISTLPU (pid, lpu, lpudt, lpudx, district, subdiv) 
                        VALUES (@pid, @lpu, @lpudt, @lpudx, @district, @subdiv);";

                    using var cmdHist = new SqliteCommand(historyQuery, connection, transaction);
                    cmdHist.Parameters.AddWithValue("@pid", _personId);
                    cmdHist.Parameters.AddWithValue("@lpu", lpu);
                    cmdHist.Parameters.AddWithValue("@lpudt", (object)lpudt ?? DBNull.Value);
                    cmdHist.Parameters.AddWithValue("@lpudx", (object)lpudx ?? DBNull.Value);
                    cmdHist.Parameters.AddWithValue("@district", (object)district ?? DBNull.Value);
                    cmdHist.Parameters.AddWithValue("@subdiv", (object)subdiv ?? DBNull.Value);
                    cmdHist.ExecuteNonQuery();
                }

                transaction.Commit();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения прикрепления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}