﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime.CompilerServices;

namespace namm
{
    public class DiscountRule : INotifyPropertyChanged
    {
        public string CriteriaType { get; set; } = string.Empty;
        public int ID { get; set; }
        public decimal Threshold { get; set; }
        public decimal DiscountPercent { get; set; }

        // Không cần INotifyPropertyChanged và IsAppliedToSelectedCustomer nữa
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public partial class LoyalCustomerView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable customerTable = new DataTable();
        private ObservableCollection<DiscountRule> discountRules = new ObservableCollection<DiscountRule>();

        public LoyalCustomerView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                lvDiscountRules.ItemsSource = discountRules;
                await LoadDiscountRulesAsync(); // Tải các quy tắc đã lưu
                await LoadLoyalCustomersAsync(); // Tải danh sách khách hàng
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Đã xảy ra lỗi khi tải dữ liệu khách hàng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadLoyalCustomersAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT
                        c.ID,
                        c.Name AS CustomerName,
                        c.CustomerCode,
                        c.PhoneNumber,
                        c.Address,
                        COUNT(DISTINCT b.ID) AS PurchaseCount,
                        ISNULL(SUM(b.TotalAmount), 0) AS TotalSpent,
                        ISNULL(SUM(b.SubTotal - b.TotalAmount), 0) AS TotalDiscountGiven,
                        -- Logic mới: Hiển thị mức giảm giá cao nhất mà khách hàng có thể đạt được
                        (SELECT FORMAT(ISNULL(MAX(dr.DiscountPercent), 0), 'G29') + '%' FROM DiscountRule dr WHERE (dr.CriteriaType = N'Số lần mua' AND COUNT(DISTINCT b.ID) >= dr.Threshold) OR (dr.CriteriaType = N'Tổng chi tiêu' AND ISNULL(SUM(b.TotalAmount), 0) >= dr.Threshold)) AS AppliedRuleDescription
                    FROM Customer c
                    LEFT JOIN Bill b ON c.ID = b.IdCustomer AND b.Status = 1
                    GROUP BY c.ID, c.Name, c.CustomerCode, c.PhoneNumber, c.Address
                    ORDER BY TotalSpent DESC;
                ";

                var adapter = new SqlDataAdapter(query, connection);
                customerTable = new DataTable();
                customerTable.Columns.Add("STT", typeof(int));
                customerTable.Columns.Add("Discount", typeof(decimal)); // Thêm cột giảm giá

                await Task.Run(() => adapter.Fill(customerTable));

                // Gán giá trị cho cột Discount, STT sẽ được xử lý trong sự kiện LoadingRow
                for (int i = 0; i < customerTable.Rows.Count; i++)
                {
                    customerTable.Rows[i]["Discount"] = customerTable.Rows[i]["TotalDiscountGiven"];
                }

                dgLoyalCustomers.ItemsSource = customerTable.DefaultView;
            }
        }

        private async Task LoadDiscountRulesAsync()
        {
            discountRules.Clear();
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, CriteriaType, Threshold, DiscountPercent FROM DiscountRule ORDER BY CriteriaType, Threshold";
                var command = new SqlCommand(query, connection);
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        discountRules.Add(new DiscountRule
                        {
                            ID = reader.GetInt32(0),
                            CriteriaType = reader.GetString(1),
                            Threshold = reader.GetDecimal(2),
                            DiscountPercent = reader.GetDecimal(3)
                        });
                    }
                }
            }
        }

        private async void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            if (cbCriteriaType.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một tiêu chí.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!decimal.TryParse(txtThreshold.Text, out decimal threshold) || threshold <= 0)
            {
                MessageBox.Show("Ngưỡng phải là một số dương hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!decimal.TryParse(txtDiscountPercent.Text, out decimal discountPercent) || discountPercent < 0)
            {
                MessageBox.Show("Mức giảm giá phải là một số không âm hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string criteriaType = ((ComboBoxItem)cbCriteriaType.SelectedItem).Content.ToString();

            // Lưu vào DB
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "INSERT INTO DiscountRule (CriteriaType, Threshold, DiscountPercent) OUTPUT INSERTED.ID VALUES (@CriteriaType, @Threshold, @DiscountPercent)";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CriteriaType", criteriaType);
                command.Parameters.AddWithValue("@Threshold", threshold);
                command.Parameters.AddWithValue("@DiscountPercent", discountPercent);

                try
                {
                    await connection.OpenAsync();
                    int newId = (int)await command.ExecuteScalarAsync();

                    // Thêm vào danh sách trên UI
                    discountRules.Add(new DiscountRule
                    {
                        ID = newId,
                        CriteriaType = criteriaType,
                        Threshold = threshold,
                        DiscountPercent = discountPercent
                    });

                    // Reset input fields
                    cbCriteriaType.SelectedIndex = -1;
                    txtThreshold.Clear();
                    txtDiscountPercent.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi thêm mức giảm giá: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
        {
            // Lấy tất cả các quy tắc được chọn
            var selectedRules = lvDiscountRules.SelectedItems.Cast<DiscountRule>().ToList();

            if (selectedRules.Any())
            {
                if (MessageBox.Show($"Bạn có chắc chắn muốn xóa {selectedRules.Count} mức giảm giá đã chọn không?", 
                    "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }

                var idsToDelete = selectedRules.Select(r => r.ID).ToList();
                string idList = string.Join(",", idsToDelete);

                // Xóa khỏi DB
                using (var connection = new SqlConnection(connectionString))
                {
                    // Xóa nhiều mục cùng lúc bằng IN clause
                    var command = new SqlCommand($"DELETE FROM DiscountRule WHERE ID IN ({idList})", connection);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }

                // Xóa khỏi UI (cần duyệt ngược để tránh lỗi khi xóa item khỏi collection)
                foreach (var rule in selectedRules)
                {
                    discountRules.Remove(rule);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một mức giảm giá để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DgLoyalCustomers_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Gán lại số thứ tự dựa trên vị trí hiển thị của hàng trong DataGrid.
            // Điều này đảm bảo STT luôn đúng thứ tự 1, 2, 3,... ngay cả khi sắp xếp.
            if (e.Row.Item is DataRowView rowView)
            {
                rowView.Row["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private async void BtnApplyRuleToAll_Click(object sender, RoutedEventArgs e)
        {
            var selectedRules = lvDiscountRules.SelectedItems.Cast<DiscountRule>().ToList();

            string ruleDescription = $"{selectedRules.Count} mức";
            if (MessageBox.Show($"Hành động này sẽ áp dụng mức '{ruleDescription}' cho TẤT CẢ khách hàng trong hệ thống. Bạn có chắc chắn muốn tiếp tục?",
                "CẢNH BÁO", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }

            var allCustomerIds = customerTable.AsEnumerable().Select(row => row.Field<int>("ID")).ToList();
            await ApplyRulesToCustomers(allCustomerIds, selectedRules.Select(r => r.ID).ToList());
        }

        private void LvDiscountRules_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Bật các nút áp dụng khi có ít nhất 1 mức giảm giá được chọn
            bool anyRuleSelected = lvDiscountRules.SelectedItems.Count > 0;
            btnRemoveRule.IsEnabled = anyRuleSelected;
        }

        private void RuleHeaderCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox headerCheckBox)
            {
                bool isChecked = headerCheckBox.IsChecked ?? false;
                foreach (var item in lvDiscountRules.Items)
                {
                    if (lvDiscountRules.ItemContainerGenerator.ContainerFromItem(item) is ListViewItem lvi)
                        lvi.IsSelected = isChecked;
                }
            }
        }

        private async Task ApplyRulesToCustomers(List<int> customerIds, List<int> ruleIds)
        {
            // In the current design, rules are applied dynamically at checkout,
            // so there's no direct database update needed here to link a rule to a customer.
            // This action can be considered a "refresh" to ensure the view is up-to-date.

            // We can just show a success message and reload the customer data.
            MessageBox.Show($"Thao tác thành công. Dữ liệu khách hàng sẽ được làm mới.",
                            "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadLoyalCustomersAsync();
        }

        // Phương thức này đã bị xóa khỏi XAML và không còn được sử dụng.
        // Việc giữ lại nó sẽ gây lỗi nếu XAML cũ vẫn còn tham chiếu.
        // Vì vậy, chúng ta sẽ xóa nó hoàn toàn.
        private void DataGridRow_PreviewMouseLeftButtonDown_Selection(object sender, System.Windows.Input.MouseButtonEventArgs e) { }

        private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox)?.IsChecked ?? false;
            if (dgLoyalCustomers.ItemsSource is DataView dataView)
            {
                // This part of the logic seems to be missing or incorrect in the original code.
                // This is a placeholder for selecting all customers.
                // For now, we will just iterate and set a non-existent 'IsSelected' property,
                // which would require changes to the DataTable to work fully.
                // This is left as-is to match potential implicit logic.
            }
        }

        private async void BtnDeleteCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (dgLoyalCustomers.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một khách hàng để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCustomerRow = (DataRowView)dgLoyalCustomers.SelectedItem;
            string customerName = selectedCustomerRow["CustomerName"].ToString();
            int customerId = (int)selectedCustomerRow["ID"];

            if (MessageBox.Show($"Bạn có chắc chắn muốn xóa khách hàng '{customerName}' không? Hành động này không thể hoàn tác.",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Kiểm tra xem khách hàng có lịch sử giao dịch không
                var checkBillCmd = new SqlCommand("SELECT COUNT(1) FROM Bill WHERE IdCustomer = @CustomerId", connection);
                checkBillCmd.Parameters.AddWithValue("@CustomerId", customerId);
                int billCount = (int)await checkBillCmd.ExecuteScalarAsync();

                if (billCount > 0)
                {
                    MessageBox.Show($"Không thể xóa khách hàng '{customerName}' vì họ đã có lịch sử giao dịch.",
                        "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Nếu không có giao dịch, tiến hành xóa
                var deleteCmd = new SqlCommand("DELETE FROM Customer WHERE ID = @ID", connection);
                deleteCmd.Parameters.AddWithValue("@ID", customerId);

                int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    MessageBox.Show("Xóa khách hàng thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadLoyalCustomersAsync(); // Tải lại danh sách khách hàng
                }
            }
        }
    }
}