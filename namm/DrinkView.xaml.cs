﻿﻿﻿using System;
using System.Configuration;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace namm
{
    public partial class DrinkView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable? drinkDataTable;

        public DrinkView()
        {
            InitializeComponent();
            // Đăng ký sự kiện IsVisibleChanged để tải lại dữ liệu mỗi khi view được hiển thị
            this.IsVisibleChanged += DrinkView_IsVisibleChanged;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Không cần tải dữ liệu ở đây nữa vì đã có IsVisibleChanged xử lý
        }

        private async void DrinkView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Nếu UserControl trở nên sichtbar (visible), tải lại dữ liệu
            if ((bool)e.NewValue)
            {
                await LoadDataAsync();
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                await LoadDrinksToComboBoxAsync();
                await LoadDrinksAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadDrinksToComboBoxAsync()
        {
            // Lấy cả DrinkCode để hiển thị khi chọn
            const string query = "SELECT ID, Name, DrinkCode FROM Drink ORDER BY Name";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable drinkListTable = new DataTable();
                await Task.Run(() => adapter.Fill(drinkListTable));
                cbDrink.ItemsSource = drinkListTable.DefaultView;
            }
        }

        private async Task LoadDrinksAsync()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT 
                        d.ID, 
                        CASE WHEN d.DrinkCode IS NOT NULL THEN (d.DrinkCode + '_NB') ELSE '' END AS DrinkCode, 
                        d.Name, d.OriginalPrice, d.ActualPrice, d.StockQuantity, d.CategoryID,
                        ISNULL(c.Name, 'N/A') AS CategoryName 
                    FROM Drink d
                    LEFT JOIN Category c ON d.CategoryID = c.ID
                    -- Thay đổi logic: hiển thị đồ uống nếu nó có giá nhập nguyên bản > 0
                    WHERE d.OriginalPrice > 0"; 

                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                drinkDataTable = new DataTable(); // Initialize the DataTable
                drinkDataTable.Columns.Add("STT", typeof(int));
                await Task.Run(() => adapter.Fill(drinkDataTable));

                dgDrinks.ItemsSource = drinkDataTable.DefaultView;
            }
        }

        private void DgDrinks_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgDrinks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Khi chọn từ Grid, đồng bộ lên ComboBox và các trường
            if (dgDrinks.SelectedItem is DataRowView row)
            {
                cbDrink.SelectionChanged -= CbDrink_SelectionChanged; // Tạm ngắt event
                cbDrink.SelectedValue = row["ID"];
                cbDrink.SelectionChanged += CbDrink_SelectionChanged; // Bật lại event

                txtDrinkCode.Text = row["DrinkCode"] as string ?? string.Empty;
                txtPrice.Text = Convert.ToDecimal(row["OriginalPrice"]).ToString("G0"); // Bỏ phần thập phân .00
                txtActualPrice.Text = Convert.ToDecimal(row["ActualPrice"]).ToString("G0"); // Bỏ phần thập phân .00
                txtStockQuantity.Text = Convert.ToDecimal(row["StockQuantity"]).ToString("G0");
                cbDrink.IsEnabled = false; // Không cho đổi đồ uống khi đang sửa
                btnDelete.IsEnabled = true; // Bật nút xóa khi chọn một mục
            }
        }

        // Đổi tên BtnAdd và BtnEdit thành một nút BtnSave duy nhất
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (cbDrink.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để gán thuộc tính.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!ValidateInput()) return;

            int drinkId = (int)cbDrink.SelectedValue;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Luôn là UPDATE, không có INSERT ở màn hình này
                const string query = "UPDATE Drink SET OriginalPrice = @OriginalPrice, ActualPrice = @ActualPrice, StockQuantity = @StockQuantity WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                AddParameters(command, drinkId); // Truyền ID vào
                try
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Cập nhật đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    _ = LoadDrinksAsync(); // Tải lại danh sách
                    ResetFields();
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi cập nhật: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgDrinks.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgDrinks.SelectedItem is DataRowView row)
            {
                int drinkId = (int)row["ID"];
                string drinkName = row["Name"].ToString();

                // Kiểm tra xem đồ uống có tồn tại trong bất kỳ hóa đơn nào không
                bool isInBill = false;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    const string checkQuery = "SELECT TOP 1 1 FROM BillInfo WHERE DrinkID = @DrinkID";
                    SqlCommand checkCommand = new SqlCommand(checkQuery, connection);
                    checkCommand.Parameters.AddWithValue("@DrinkID", drinkId);
                    await connection.OpenAsync();
                    var result = await checkCommand.ExecuteScalarAsync();
                    if (result != null)
                    {
                        isInBill = true;
                    }
                }

                if (isInBill)
                {
                    MessageBox.Show($"Không thể xóa đồ uống '{drinkName}' vì đã có lịch sử giao dịch. Bạn có thể ẩn đồ uống này ở trang 'Quản lí Đồ uống (Tổng)'.", "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Nếu không có trong hóa đơn, tiến hành hỏi xóa
                if (MessageBox.Show($"Bạn có chắc chắn muốn xóa vĩnh viễn đồ uống '{drinkName}' không? Hành động này sẽ xóa cả công thức pha chế (nếu có) và không thể hoàn tác.", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        // Xóa công thức liên quan trước, sau đó xóa đồ uống
                        const string deleteQuery = "DELETE FROM Recipe WHERE DrinkID = @ID; DELETE FROM Drink WHERE ID = @ID;";
                        SqlCommand command = new SqlCommand(deleteQuery, connection);
                        command.Parameters.AddWithValue("@ID", drinkId);

                        try
                        {
                            await connection.OpenAsync();
                            await command.ExecuteNonQueryAsync();
                            MessageBox.Show("Xóa đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                            await LoadDataAsync(); // Tải lại toàn bộ dữ liệu
                            ResetFields();
                        }
                        catch (SqlException ex)
                        {
                            MessageBox.Show($"Lỗi khi xóa đồ uống: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (drinkDataTable != null)
            {
                drinkDataTable.DefaultView.RowFilter = $"Name LIKE '%{txtSearch.Text}%'";
            }
        }

        private void CbDrink_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDrink.SelectedItem is DataRowView selectedDrink && drinkDataTable != null)
            {
                int selectedId = (int)selectedDrink["ID"];

                // Tìm kiếm đồ uống trong bảng dữ liệu đã tải
                DataRow? existingDrinkRow = drinkDataTable.AsEnumerable()
                    .FirstOrDefault(row => (int)row["ID"] == selectedId);

                if (existingDrinkRow != null)
                {
                    // Nếu đồ uống đã tồn tại trong danh sách, hiển thị thông tin của nó
                    txtDrinkCode.Text = existingDrinkRow["DrinkCode"] as string ?? string.Empty;
                    txtPrice.Text = Convert.ToDecimal(existingDrinkRow["OriginalPrice"]).ToString("G0");
                    txtActualPrice.Text = Convert.ToDecimal(existingDrinkRow["ActualPrice"]).ToString("G0");
                    txtStockQuantity.Text = Convert.ToDecimal(existingDrinkRow["StockQuantity"]).ToString("G0");
                }
                else
                {
                    // Nếu là đồ uống mới, tạo mã và xóa các trường khác
                    txtDrinkCode.Text = (selectedDrink["DrinkCode"] as string ?? "") + "_NB";
                    txtPrice.Clear();
                    txtActualPrice.Clear();
                    txtStockQuantity.Text = "0"; // Mặc định tồn kho là 0
                }
            }
        }

        private void ResetFields()
        {
            cbDrink.SelectedIndex = -1;
            txtDrinkCode.Clear();
            txtPrice.Clear();
            txtActualPrice.Clear();
            txtStockQuantity.Clear();
            dgDrinks.SelectedItem = null;
            cbDrink.IsEnabled = true;
            btnDelete.IsEnabled = false; // Tắt nút xóa khi làm mới
        }

        private bool ValidateInput()
        {
            if (cbDrink.SelectedItem == null || string.IsNullOrWhiteSpace(txtPrice.Text) || 
                string.IsNullOrWhiteSpace(txtActualPrice.Text) || string.IsNullOrWhiteSpace(txtStockQuantity.Text))
            {
                MessageBox.Show("Vui lòng chọn đồ uống và nhập đầy đủ giá nhập, giá bán và số lượng tồn kho.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!decimal.TryParse(txtPrice.Text, out _) || !decimal.TryParse(txtActualPrice.Text, out _) || !decimal.TryParse(txtStockQuantity.Text, out _))
            {
                MessageBox.Show("Giá nhập, giá bán và số lượng tồn kho phải là số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void AddParameters(SqlCommand command, int? id = null)
        {
            if (id.HasValue)
            {
                command.Parameters.AddWithValue("@ID", id.Value);
            }
            command.Parameters.AddWithValue("@OriginalPrice", Convert.ToDecimal(txtPrice.Text));
            command.Parameters.AddWithValue("@ActualPrice", Convert.ToDecimal(txtActualPrice.Text));
            command.Parameters.AddWithValue("@StockQuantity", Convert.ToDecimal(txtStockQuantity.Text));
        }
    }
}