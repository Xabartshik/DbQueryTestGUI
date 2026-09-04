using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DbQueryTestGUI
{
    // Модель данных для таблицы PEOPLE
    public class PersonModel
    {
        public long Id { get; set; }
        public string Fam { get; set; }
        public string Im { get; set; }
        public string Ot { get; set; }
        public string Dr { get; set; }
        public string Enp { get; set; }
        public string Lpu { get; set; }
        public string Lpudx { get; set; }
    }

    public partial class MainWindow : Window
    {
        private readonly string _dbPath = Path.Combine(AppContext.BaseDirectory, "test_db.db");

        // Полный список всех записей из базы данных
        private List<PersonModel> _allPeople = new List<PersonModel>();
        // Отфильтрованный список для отображения в таблице DataGrid
        private ObservableCollection<PersonModel> _peopleList = new ObservableCollection<PersonModel>();

        public MainWindow()
        {
            InitializeComponent();
            LoadData();
        }

        private string GetConnectionString() => $"Data Source={_dbPath}";

        /// <summary>
        /// Загрузка всех записей из базы данных SQLite в оперативную память
        /// </summary>
        private void LoadData()
        {
            _allPeople.Clear();

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                string query = "SELECT id, fam, im, ot, dr, enp, lpu, lpudx FROM PEOPLE";
                using var command = new SqliteCommand(query, connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    _allPeople.Add(new PersonModel
                    {
                        Id = reader.GetInt64(0),
                        Fam = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Im = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Ot = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Dr = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        Enp = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        Lpu = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        Lpudx = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                    });
                }

                // Применяем текущий фильтр поиска
                ApplyFilter(TxtSearch != null ? TxtSearch.Text.Trim() : string.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Фильтрация записей на стороне C# (поддерживает кириллицу в любом регистре)
        /// </summary>
        private void ApplyFilter(string filter)
        {
            _peopleList.Clear();

            var query = _allPeople.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(p =>
                    p.Fam.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    p.Im.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    p.Ot.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    p.Enp.Contains(filter, StringComparison.OrdinalIgnoreCase)
                );
            }

            foreach (var person in query)
            {
                _peopleList.Add(person);
            }

            DgPeople.ItemsSource = _peopleList;
            if (TxtCount != null)
            {
                TxtCount.Text = _peopleList.Count.ToString();
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(TxtSearch.Text.Trim());
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter(TxtSearch.Text.Trim());
        }

        private void BtnResetSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            ApplyFilter(string.Empty);
        }

        private void BtnCheckAttachment_Click(object sender, RoutedEventArgs e)
        {
            // Спрашиваем пользователя с помощью кнопок: Да = по полису, Нет = по ФИО и ДР
            var choice = MessageBox.Show(
                "Выберите критерий для проверки прикрепления:\n\n• Нажмите \"Да\" - для поиска по номеру полиса (ЕНП)\n• Нажмите \"Нет\" - для поиска по ФИО и дате рождения",
                "Выбор способа проверки",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            bool isPolicySearch = (choice == MessageBoxResult.Yes);
            string promptText = isPolicySearch
                ? "Введите номер полиса (ЕНП):\n(Например: 7594045746370284)"
                : "Введите ФИО и дату рождения:\n(Например: Иванов Иван Иванович 01.01.1960)";
            string windowTitle = isPolicySearch ? "Проверка по полису" : "Проверка по ФИО и дате рождения";

            string input = Microsoft.VisualBasic.Interaction.InputBox(promptText, windowTitle, "", -1, -1);

            if (string.IsNullOrWhiteSpace(input)) return;
            input = input.Trim();

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                List<PersonModel> foundPersons;

                if (isPolicySearch)
                {
                    // Поиск по ЕНП
                    foundPersons = _allPeople.Where(p => p.Enp.Equals(input, StringComparison.OrdinalIgnoreCase)).ToList();
                }
                else
                {
                    // Поиск по ФИО и дате рождения
                    foundPersons = _allPeople.Where(p =>
                        $"{p.Fam} {p.Im} {p.Ot} {p.Dr}".Contains(input, StringComparison.OrdinalIgnoreCase) ||
                        input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                             .All(word => $"{p.Fam} {p.Im} {p.Ot} {p.Dr}".Contains(word, StringComparison.OrdinalIgnoreCase))
                    ).ToList();
                }

                if (!foundPersons.Any())
                {
                    MessageBox.Show($"По запросу \"{input}\" ничего не найдено в базе данных.", "Результат поиска", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string resultMessage = "";
                foreach (var p in foundPersons)
                {
                    resultMessage += $"{p.Fam} {p.Im} {p.Ot}, {p.Dr} (ЕНП: {(string.IsNullOrEmpty(p.Enp) ? "нет" : p.Enp)})\n";

                    if (string.IsNullOrEmpty(p.Lpu) || !string.IsNullOrEmpty(p.Lpudx))
                    {
                        resultMessage += " Статус: Не прикреплён.\n\n";
                        continue;
                    }

                    string detailsQuery = @"
                SELECT 
                    l.caption AS lpu_name,
                    t1.nam_mo AS subdiv_name,
                    t7.name_depth AS district_name
                FROM LPU l
                LEFT JOIN HISTLPU h ON h.pid = @pid AND (h.lpudx IS NULL OR h.lpudx = '')
                LEFT JOIN T001 t1 ON t1.mcod = l.code AND t1.nom_podr = h.subdiv
                LEFT JOIN T007 t7 ON t7.code_mo = l.code AND t7.nom_podr = h.subdiv AND t7.depth = h.district
                WHERE l.code = @lpuCode
                LIMIT 1;";

                    using var cmd = new SqliteCommand(detailsQuery, connection);
                    cmd.Parameters.AddWithValue("@pid", p.Id);
                    cmd.Parameters.AddWithValue("@lpuCode", p.Lpu);

                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        string lpuName = reader.IsDBNull(0) ? p.Lpu : reader.GetString(0);
                        string subdivName = reader.IsDBNull(1) ? null : reader.GetString(1);
                        string districtName = reader.IsDBNull(2) ? null : reader.GetString(2);

                        resultMessage += $" Прикреплен к: {lpuName}";
                        if (!string.IsNullOrEmpty(subdivName)) resultMessage += $", {subdivName}";
                        if (!string.IsNullOrEmpty(districtName)) resultMessage += $", {districtName}";
                        resultMessage += "\n\n";
                    }
                    else
                    {
                        resultMessage += $" Прикреплен к МО (код: {p.Lpu})\n\n";
                    }
                }

                MessageBox.Show(resultMessage.Trim(), "Результаты проверки прикрепления", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при поиске: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <summary>
        /// Обработка выбора строки в таблице
        /// </summary>
        private void DgPeople_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgPeople.SelectedItem is not PersonModel selectedPerson)
            {
                TxtAttachmentStatus.Text = "Выберите запись в таблице...";
                return;
            }

            CheckAttachmentDetails(selectedPerson);
        }

        private void CheckAttachmentDetails(object person)
        {
            if (person is not PersonModel p) return;

            string personHeader = $"{p.Fam} {p.Im} {p.Ot}".Trim() + $", {p.Dr}";

            if (string.IsNullOrEmpty(p.Lpu) || !string.IsNullOrEmpty(p.Lpudx))
            {
                TxtAttachmentStatus.Text = $"{personHeader} – не прикреплён";
                return;
            }

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                string detailsQuery = @"
                    SELECT 
                        l.caption AS lpu_name,
                        t1.nam_mo AS subdiv_name,
                        t7.name_depth AS district_name
                    FROM PEOPLE pe
                    LEFT JOIN LPU l ON l.code = pe.lpu
                    LEFT JOIN HISTLPU h ON h.id = (
                        SELECT latest.id
                        FROM HISTLPU latest
                        WHERE latest.pid = pe.id
                          AND latest.lpu = pe.lpu
                          AND (latest.lpudx IS NULL OR latest.lpudx = '')
                        ORDER BY latest.id DESC
                        LIMIT 1
                    )
                    LEFT JOIN T001 t1 ON t1.mcod = pe.lpu AND t1.nom_podr = h.subdiv
                    LEFT JOIN T007 t7 ON t7.code_mo = pe.lpu AND t7.nom_podr = h.subdiv AND t7.depth = COALESCE(h.district, pe.lpuuch)
                    WHERE pe.id = @pid
                    LIMIT 1;";

                using var cmd = new SqliteCommand(detailsQuery, connection);
                cmd.Parameters.AddWithValue("@pid", p.Id);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    string lpuName = reader.IsDBNull(0) ? "Неизвестная МО" : reader.GetString(0);
                    string subdivName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    string districtName = reader.IsDBNull(2) ? null : reader.GetString(2);

                    if (!string.IsNullOrEmpty(subdivName) && !string.IsNullOrEmpty(districtName))
                    {
                        TxtAttachmentStatus.Text = $"{personHeader} – прикреплен к {lpuName}, {subdivName.ToLower()}, {districtName.ToLower()}";
                    }
                    else if (!string.IsNullOrEmpty(districtName))
                    {
                        TxtAttachmentStatus.Text = $"{personHeader} – прикреплен к {lpuName}, участок: {districtName.ToLower()}";
                    }
                    else if (!string.IsNullOrEmpty(subdivName))
                    {
                        TxtAttachmentStatus.Text = $"{personHeader} – прикреплен к {lpuName}, {subdivName.ToLower()} (нет участка)";
                    }
                    else
                    {
                        TxtAttachmentStatus.Text = $"{personHeader} – прикреплен к {lpuName} (нет участка)";
                    }
                }
                else
                {
                    TxtAttachmentStatus.Text = $"{personHeader} – прикреплен к МО (код: {p.Lpu}), детали отсутствуют";
                }
            }
            catch (Exception ex)
            {
                TxtAttachmentStatus.Text = $"Ошибка расчета прикрепления: {ex.Message}";
            }
        }
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddPersonWindow(_dbPath);
            if (addWindow.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void BtnManageAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (DgPeople.SelectedItem is not PersonModel selectedPerson)
            {
                MessageBox.Show("Пожалуйста, выберите пациента из таблицы.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fullName = $"{selectedPerson.Fam} {selectedPerson.Im} {selectedPerson.Ot}".Trim();
            var attachWindow = new AttachWindow(_dbPath, selectedPerson.Id, fullName);

            if (attachWindow.ShowDialog() == true)
            {
                LoadData();
                var updatedPerson = _peopleList.FirstOrDefault(p => p.Id == selectedPerson.Id);
                if (updatedPerson != null)
                {
                    DgPeople.SelectedItem = updatedPerson;
                    CheckAttachmentDetails(updatedPerson);
                }
            }
        }

        private void MenuCopyCell_Click(object sender, RoutedEventArgs e)
        {
            if (DgPeople.CurrentCell.Column != null)
            {
                var cellContent = DgPeople.CurrentCell.Column.GetCellContent(DgPeople.CurrentCell.Item);
                if (cellContent is TextBlock textBlock)
                {
                    Clipboard.SetText(textBlock.Text ?? string.Empty);
                    MessageBox.Show("Текст скопирован в буфер обмена!", "Копирование", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void MenuCopyRow_Click(object sender, RoutedEventArgs e)
        {
            if (DgPeople.SelectedItem is PersonModel p)
            {
                string rowText = $"{p.Fam} {p.Im} {p.Ot} | ДР: {p.Dr} | ЕНП: {p.Enp} | ЛПУ: {p.Lpu}";
                Clipboard.SetText(rowText);
                MessageBox.Show("Строка скопирована в буфер обмена!", "Копирование", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (DgPeople.SelectedItem is not PersonModel selectedPerson)
            {
                MessageBox.Show("Пожалуйста, выберите запись для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Вы действительно хотите удалить пациента:\n{selectedPerson.Fam} {selectedPerson.Im} {selectedPerson.Ot}?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                string deleteQuery = "DELETE FROM PEOPLE WHERE id = @id;";
                using var cmd = new SqliteCommand(deleteQuery, connection);
                cmd.Parameters.AddWithValue("@id", selectedPerson.Id);
                cmd.ExecuteNonQuery();

                LoadData();
                TxtAttachmentStatus.Text = "Запись успешно удалена.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении записи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}