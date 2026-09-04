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
                var current = AttachmentService.GetCurrent(_dbPath, _personId);
                string currentLpu = current.LpuCode;
                TxtLpuDt.Text = current.AttachmentDate ?? DateTime.Today.ToString("dd.MM.yyyy");
                TxtLpuDx.Text = current.DetachmentDate ?? string.Empty;

                if (!string.IsNullOrEmpty(currentLpu))
                {
                    // Устанавливаем LPU.
                    foreach (ComboBoxItemModel item in CmbLpu.Items)
                    {
                        if (item.Code == currentLpu)
                        {
                            CmbLpu.SelectedItem = item;
                            break;
                        }
                    }

                    LoadSubdivList(currentLpu, current.SubdivCode);
                    if (!string.IsNullOrEmpty(current.SubdivCode))
                    {
                        LoadDistrictList(currentLpu, current.SubdivCode, current.DistrictCode);
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

                AttachmentService.SaveCurrent(_dbPath, _personId, lpu, subdiv, district, lpudt, lpudx);

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
