using Microsoft.Data.Sqlite;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;

namespace DbQueryTestGUI
{
    public partial class AddPersonWindow : Window
    {
        private readonly string _dbPath;

        public AddPersonWindow(string dbPath)
        {
            InitializeComponent();
            _dbPath = dbPath;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtFam.Text) ||
                string.IsNullOrWhiteSpace(TxtIm.Text) ||
                string.IsNullOrWhiteSpace(TxtDr.Text))
            {
                MessageBox.Show("Заполните обязательные поля: Фамилия, Имя и Дата рождения.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!DateTime.TryParseExact(TxtDr.Text.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                MessageBox.Show("Дата рождения должна быть указана в формате ДД.ММ.ГГГГ.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(TxtEnp.Text) && !Regex.IsMatch(TxtEnp.Text.Trim(), "^\\d{16}$"))
            {
                MessageBox.Show("ЕНП должен состоять из 16 цифр.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                string query = @"
                    INSERT INTO PEOPLE (fam, im, ot, dr, enp)
                    VALUES (@fam, @im, @ot, @dr, @enp);";

                using var cmd = new SqliteCommand(query, connection);
                cmd.Parameters.AddWithValue("@fam", TxtFam.Text.Trim());
                cmd.Parameters.AddWithValue("@im", TxtIm.Text.Trim());
                cmd.Parameters.AddWithValue("@ot", string.IsNullOrWhiteSpace(TxtOt.Text) ? DBNull.Value : TxtOt.Text.Trim());
                cmd.Parameters.AddWithValue("@dr", TxtDr.Text.Trim());
                cmd.Parameters.AddWithValue("@enp", string.IsNullOrWhiteSpace(TxtEnp.Text) ? DBNull.Value : TxtEnp.Text.Trim());
                cmd.ExecuteNonQuery();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения записи в БД: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
