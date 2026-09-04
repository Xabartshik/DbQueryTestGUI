using Microsoft.Data.Sqlite;
using System;
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

            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                string query = @"
                    INSERT INTO PEOPLE (fam, im, ot, dr, enp, lpu) 
                    VALUES (@fam, @im, @ot, @dr, @enp, @lpu);";

                using var cmd = new SqliteCommand(query, connection);
                cmd.Parameters.AddWithValue("@fam", TxtFam.Text.Trim());
                cmd.Parameters.AddWithValue("@im", TxtIm.Text.Trim());
                cmd.Parameters.AddWithValue("@ot", string.IsNullOrWhiteSpace(TxtOt.Text) ? DBNull.Value : TxtOt.Text.Trim());
                cmd.Parameters.AddWithValue("@dr", TxtDr.Text.Trim());
                cmd.Parameters.AddWithValue("@enp", string.IsNullOrWhiteSpace(TxtEnp.Text) ? DBNull.Value : TxtEnp.Text.Trim());
                cmd.Parameters.AddWithValue("@lpu", string.IsNullOrWhiteSpace(TxtLpu.Text) ? DBNull.Value : TxtLpu.Text.Trim());

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