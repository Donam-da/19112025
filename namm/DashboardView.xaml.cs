﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Controls;


namespace namm
{
    public partial class DashboardView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable? menuDataTable;
        private DataTable? tableDataTable;
        private readonly Dictionary<int, ObservableCollection<BillItem>> billsByTable = new Dictionary<int, ObservableCollection<BillItem>>();
        private readonly AccountDTO? loggedInAccount;

        public DashboardView(AccountDTO? account = null)
        {
            InitializeComponent();
            this.loggedInAccount = account;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var loadTablesTask = LoadTables();
                var loadCategoriesTask = LoadCategories();
                var loadMenuTask = LoadMenu();

                await Task.WhenAll(loadTablesTask, loadCategoriesTask, loadMenuTask);

                cbCategory.SelectionChanged += CbCategory_SelectionChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nghiêm trọng khi tải màn hình chính: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadCategories()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT 0 AS ID, N'Tất cả' AS Name UNION ALL SELECT ID, Name FROM Category WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                var categoryTable = new DataTable();
                await Task.Run(() => adapter.Fill(categoryTable));

                cbCategory.ItemsSource = categoryTable.DefaultView;
                cbCategory.SelectedValuePath = "ID";
                cbCategory.DisplayMemberPath = "Name";
                cbCategory.SelectedIndex = 0; 
            }
        }

        private async Task LoadMenu()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name, DrinkCode, ActualPrice, CategoryID FROM Drink WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                menuDataTable = new DataTable();
                menuDataTable.Columns.Add("STT", typeof(int));
                await Task.Run(() => adapter.Fill(menuDataTable));

                dgMenu.ItemsSource = menuDataTable.DefaultView;
            }
        }

        private async Task LoadTables()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name, Status FROM TableFood ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                tableDataTable = new DataTable();
                tableDataTable.Columns.Add("STT", typeof(int));
                await Task.Run(() => adapter.Fill(tableDataTable));

                dgTables.ItemsSource = tableDataTable.DefaultView;
            }
        }

        private void CbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterMenu();
        }

        private void FilterMenu()
        {
            if (menuDataTable == null || cbCategory.SelectedValue == null)
            {
                return;
            }

            int categoryId = (int)cbCategory.SelectedValue;

            if (categoryId == 0) 
            {
                menuDataTable.DefaultView.RowFilter = string.Empty; 
            }
            else
            {
                menuDataTable.DefaultView.RowFilter = $"CategoryID = {categoryId}"; 
            }
        }

        private void DgTables_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Logic này có thể đã có sẵn, nếu chưa có thì thêm vào
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgMenu_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private async void DgTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgTables.SelectedItem is DataRowView selectedTable)
            {
                string tableName = selectedTable["Name"].ToString() ?? "Không xác định";
                tbSelectedTable.Text = $"Chọn: {tableName}";
                tbSelectedTable.FontStyle = FontStyles.Normal;
                tbSelectedTable.Foreground = System.Windows.Media.Brushes.Black;

                int tableId = (int)selectedTable["ID"];
                var billItems = await LoadUnpaidBillForTableAsync(tableId);
                billsByTable[tableId] = billItems;

                dgBill.ItemsSource = billItems;
                UpdateTotalAmount();

                SyncTableStatusBasedOnBill();
            }
            else
            {
                tbSelectedTable.Text = "(Chưa chọn bàn)";
                tbSelectedTable.FontStyle = FontStyles.Italic;
                tbSelectedTable.Foreground = System.Windows.Media.Brushes.Gray;

                dgBill.ItemsSource = null;
                UpdateTotalAmount();
            }
        }

        private async void DgMenu_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgTables.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một bàn trước khi thêm món.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgMenu.SelectedItem is DataRowView selectedDrinkRow)
            {
                try
                {
                    int drinkId = (int)selectedDrinkRow["ID"];
                    string drinkName = selectedDrinkRow["Name"].ToString() ?? "Không tên";

                    var availableStock = await GetDrinkStockAsync(drinkId);

                    if (!availableStock.Any())
                    {
                        MessageBox.Show("Đồ uống này chưa được cấu hình để bán (chưa có giá hoặc công thức).", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var dialog = new SelectDrinkTypeDialog(drinkName, availableStock);
                    dialog.Owner = Window.GetWindow(this);

                    if (dialog.ShowDialog() == true)
                    {
                        int currentTableId = (int)((DataRowView)dgTables.SelectedItem)["ID"];
                        var currentBillItems = billsByTable[currentTableId];

                        foreach (var selectedItem in dialog.SelectedQuantities)
                        {
                            string drinkType = selectedItem.Key;
                            int quantity = selectedItem.Value;
                            decimal price = Convert.ToDecimal(selectedDrinkRow["ActualPrice"]);
                            string baseDrinkCode = selectedDrinkRow["DrinkCode"].ToString() ?? "";

                            var existingItem = currentBillItems.FirstOrDefault(item => item.DrinkId == drinkId && item.DrinkType == drinkType);

                            if (existingItem != null)
                            {
                                existingItem.Quantity += quantity;
                            }
                            else
                            {
                                currentBillItems.Add(new BillItem { 
                                    DrinkId = drinkId, 
                                    DrinkName = drinkName, 
                                    DrinkTypeCode = $"{baseDrinkCode}_{(drinkType == "Nguyên bản" ? "NB" : "PC")}",
                                    DrinkType = drinkType, 
                                    Quantity = quantity, 
                                    Price = price 
                                });
                            }
                            await UpdateStockForDrinkAsync(drinkId, drinkType, quantity);
                        }
                        await SaveBillToDbAsync(currentTableId, currentBillItems);

                        UpdateTotalAmount();
                        SyncTableStatusBasedOnBill();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Đã xảy ra lỗi không mong muốn: {ex.Message}", "Lỗi nghiêm trọng", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateTotalAmount()
        {
            if (dgBill.ItemsSource is ObservableCollection<BillItem> currentBillItems)
            {
                decimal total = currentBillItems.Sum(item => item.TotalPrice);
                tbTotalAmount.Text = $"{total:N0} VNĐ";
            }
            else
            {
                tbTotalAmount.Text = "0 VNĐ";
            }
        }

        private async void DeleteBillItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is BillItem itemToRemove && dgBill.ItemsSource is ObservableCollection<BillItem> currentBillItems)
            {
                _ = UpdateStockForDrinkAsync(itemToRemove.DrinkId, itemToRemove.DrinkType, -itemToRemove.Quantity);

                currentBillItems.Remove(itemToRemove);
                UpdateTotalAmount();

                if (currentBillItems.Any())
                {
                    int currentTableId = (int)((DataRowView)dgTables.SelectedItem)["ID"];
                    await SaveBillToDbAsync(currentTableId, currentBillItems);
                }
                SyncTableStatusBasedOnBill();
            }
        }

        private async Task<Dictionary<string, int>> GetDrinkStockAsync(int drinkId)
        {
            var stock = new Dictionary<string, int>();
            using (var connection = new SqlConnection(connectionString))
            {
                var cmdOriginal = new SqlCommand("SELECT StockQuantity FROM Drink WHERE ID = @ID AND OriginalPrice > 0", connection);
                cmdOriginal.Parameters.AddWithValue("@ID", drinkId);

                var cmdRecipe = new SqlCommand(@"
                    SELECT MIN(FLOOR(m.Quantity / r.Quantity))
                    FROM Recipe r
                    JOIN Material m ON r.MaterialID = m.ID
                    JOIN Drink d ON r.DrinkID = d.ID
                    WHERE r.DrinkID = @ID AND d.IsRecipeActive = 1
                    HAVING COUNT(r.DrinkID) > 0", connection);
                cmdRecipe.Parameters.AddWithValue("@ID", drinkId);

                await connection.OpenAsync();

                var originalStockResult = await cmdOriginal.ExecuteScalarAsync();
                if (originalStockResult != null && originalStockResult != DBNull.Value)
                {
                    stock["Nguyên bản"] = Convert.ToInt32(originalStockResult);
                }

                var recipeStockResult = await cmdRecipe.ExecuteScalarAsync();
                if (recipeStockResult != null && recipeStockResult != DBNull.Value)
                {
                    stock["Pha chế"] = Convert.ToInt32(recipeStockResult);
                }
            }
            return stock;
        }

        private async Task UpdateTableStatusInDbAsync(int tableId, string newStatus)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    var command = new SqlCommand("UPDATE TableFood SET Status = @Status WHERE ID = @ID", connection);
                    command.Parameters.AddWithValue("@Status", newStatus);
                    command.Parameters.AddWithValue("@ID", tableId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể cập nhật trạng thái bàn vào cơ sở dữ liệu: {ex.Message}", "Lỗi nền", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task UpdateStockForDrinkAsync(int drinkId, string drinkType, int quantityChange)
        {
            if (quantityChange == 0) return;

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                if (drinkType == "Nguyên bản")
                {
                    var cmd = new SqlCommand("UPDATE Drink SET StockQuantity = StockQuantity - @QuantityChange WHERE ID = @DrinkID", connection);
                    cmd.Parameters.AddWithValue("@DrinkID", drinkId);
                    cmd.Parameters.AddWithValue("@QuantityChange", quantityChange);
                    await cmd.ExecuteNonQueryAsync();
                }
                else if (drinkType == "Pha chế")
                {
                    var recipeCmd = new SqlCommand("SELECT MaterialID, Quantity FROM Recipe WHERE DrinkID = @DrinkID", connection);
                    recipeCmd.Parameters.AddWithValue("@DrinkID", drinkId);

                    var materialsToUpdate = new List<(int MaterialID, decimal RecipeQuantity)>();
                    using (var reader = await recipeCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            materialsToUpdate.Add((reader.GetInt32(0), reader.GetDecimal(1)));
                        }
                    }

                    foreach (var material in materialsToUpdate)
                    {
                        var updateMaterialCmd = new SqlCommand("UPDATE Material SET Quantity = Quantity - @QuantityChange WHERE ID = @MaterialID", connection);
                        decimal totalMaterialChange = (decimal)material.RecipeQuantity * quantityChange;
                        updateMaterialCmd.Parameters.AddWithValue("@MaterialID", material.MaterialID);
                        updateMaterialCmd.Parameters.AddWithValue("@QuantityChange", totalMaterialChange);
                        await updateMaterialCmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        private async Task<ObservableCollection<BillItem>> LoadUnpaidBillForTableAsync(int tableId)
        {
            var billItems = new ObservableCollection<BillItem>();
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"                    SELECT bi.DrinkID, d.Name, bi.DrinkType, bi.Quantity, bi.Price, d.DrinkCode
                    FROM BillInfo bi
                    JOIN Bill b ON bi.BillID = b.ID
                    JOIN Drink d ON bi.DrinkID = d.ID
                    WHERE b.TableID = @TableID AND b.Status = 0";

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TableID", tableId);

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string drinkType = reader.GetString(2);
                        string baseDrinkCode = reader.GetString(5);

                        billItems.Add(new BillItem
                        {
                            DrinkTypeCode = $"{baseDrinkCode}_{(drinkType == "Nguyên bản" ? "NB" : "PC")}",
                            DrinkId = reader.GetInt32(0),
                            DrinkName = reader.GetString(1),
                            DrinkType = reader.GetString(2),
                            Quantity = reader.GetInt32(3),
                            Price = reader.GetDecimal(4)
                        });
                    }
                }
            }
            return billItems;
        }

        private async Task SaveBillToDbAsync(int tableId, ObservableCollection<BillItem> billItems)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var cmdFindBill = new SqlCommand("SELECT ID FROM Bill WHERE TableID = @TableID AND Status = 0", connection, transaction);
                        cmdFindBill.Parameters.AddWithValue("@TableID", tableId);
                        var billIdResult = await cmdFindBill.ExecuteScalarAsync();
                        int billId;

                        if (billIdResult != null)
                        {
                            billId = (int)billIdResult;
                            var cmdDeleteInfo = new SqlCommand("DELETE FROM BillInfo WHERE BillID = @BillID", connection, transaction);
                            cmdDeleteInfo.Parameters.AddWithValue("@BillID", billId);
                            await cmdDeleteInfo.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            var cmdCreateBill = new SqlCommand("INSERT INTO Bill (TableID, Status) OUTPUT INSERTED.ID VALUES (@TableID, 0)", connection, transaction);
                            cmdCreateBill.Parameters.AddWithValue("@TableID", tableId);
                            billId = (int)await cmdCreateBill.ExecuteScalarAsync();
                        }

                        foreach (var item in billItems)
                        {
                            var cmdInsertInfo = new SqlCommand("INSERT INTO BillInfo (BillID, DrinkID, DrinkType, Quantity, Price) VALUES (@BillID, @DrinkID, @DrinkType, @Quantity, @Price)", connection, transaction);
                            cmdInsertInfo.Parameters.AddWithValue("@BillID", billId);
                            cmdInsertInfo.Parameters.AddWithValue("@DrinkID", item.DrinkId);
                            cmdInsertInfo.Parameters.AddWithValue("@DrinkType", item.DrinkType);
                            cmdInsertInfo.Parameters.AddWithValue("@Quantity", item.Quantity);
                            cmdInsertInfo.Parameters.AddWithValue("@Price", item.Price);
                            await cmdInsertInfo.ExecuteNonQueryAsync();
                        }

                        decimal totalAmount = billItems.Sum(i => i.TotalPrice);
                        var cmdUpdateTotal = new SqlCommand("UPDATE Bill SET TotalAmount = @TotalAmount WHERE ID = @BillID", connection, transaction);
                        cmdUpdateTotal.Parameters.AddWithValue("@TotalAmount", totalAmount);
                        cmdUpdateTotal.Parameters.AddWithValue("@BillID", billId);
                        await cmdUpdateTotal.ExecuteNonQueryAsync();

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw; 
                    }
                }
            }
        }

        private async Task ClearBillFromDbAsync(int tableId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    DELETE FROM BillInfo WHERE BillID IN (SELECT ID FROM Bill WHERE TableID = @TableID AND Status = 0);
                    DELETE FROM Bill WHERE TableID = @TableID AND Status = 0;";

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TableID", tableId);
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        private void SyncTableStatusBasedOnBill()
        {
            if (dgTables.SelectedItem is DataRowView selectedTable && dgBill.ItemsSource is ObservableCollection<BillItem> currentBill)
            {
                int tableId = (int)selectedTable.Row["ID"];
                string currentStatus = selectedTable.Row["Status"].ToString();
                string newStatus;

                if (currentBill.Any())
                {
                    newStatus = "Có người";
                }
                else
                {
                    newStatus = "Trống";
                    _ = ClearBillFromDbAsync(tableId);
                }

                if (currentStatus != newStatus)
                {
                    selectedTable.Row["Status"] = newStatus;
                    _ = UpdateTableStatusInDbAsync(tableId, newStatus);
                }
            }
        }

        private void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            if (dgTables.SelectedItem == null || !(dgBill.ItemsSource is ObservableCollection<BillItem> currentBill) || !currentBill.Any())
            {
                MessageBox.Show("Vui lòng chọn bàn có hóa đơn để thanh toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedTable = (DataRowView)dgTables.SelectedItem;
            int tableId = (int)selectedTable["ID"];
            string tableName = selectedTable["Name"].ToString();

            var mainAppWindow = Window.GetWindow(this) as MainAppWindow;
            if (mainAppWindow != null)
            {
                mainAppWindow.MainContent.Children.Clear();
                mainAppWindow.MainContent.Children.Add(new SelectCustomerView(tableId, tableName, currentBill, mainAppWindow.LoggedInAccount));
            }
        }
    }
}