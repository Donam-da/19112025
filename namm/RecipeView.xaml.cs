﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public class RecipeIngredient
    {
        public int MaterialID { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string UnitName { get; set; } = string.Empty;
    }

    public class RecipeCost
    {
        public int STT { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal TotalCost { get; set; }
    }

    public partial class RecipeView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private List<RecipeIngredient> currentRecipe = new List<RecipeIngredient>();
        private DataTable recipeSummaryTable = new DataTable();

        public RecipeView()
        {
            InitializeComponent();
            dgCurrentRecipe.ItemsSource = currentRecipe;
        }
 
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadDrinksToComboBox();
                await LoadMaterialsToComboBox();
                await LoadRecipeSummary();
                btnToggleRecipeActive.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nghiêm trọng khi tải trang: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadDrinksToComboBox()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name, DrinkCode, IsActive, IsRecipeActive FROM Drink ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable drinkTable = new DataTable();
                await Task.Run(() => adapter.Fill(drinkTable));
                cbDrink.ItemsSource = drinkTable.DefaultView;
            }
        }

        private async Task LoadMaterialsToComboBox()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name FROM Material WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable materialTable = new DataTable();
                await Task.Run(() => adapter.Fill(materialTable));
                cbMaterial.ItemsSource = materialTable.DefaultView;
            }
        }

        private async void CbDrink_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cbDrink.SelectedItem is DataRowView selectedDrink)
                {
                    string drinkName = selectedDrink["Name"] as string ?? "";
                    txtDrinkCode.Text = (selectedDrink["DrinkCode"] as string ?? "") + "_PC";                    int drinkId = (int)selectedDrink["ID"];
                    await LoadRecipeForDrink(drinkId);
                }
                else
                {
                    txtDrinkCode.Clear();
                    currentRecipe.Clear();
                    txtActualPrice.Clear();
                    dgCurrentRecipe.Items.Refresh();
                    await UpdateCurrentRecipeCost();
                    btnToggleRecipeActive.IsEnabled = false;
                    btnSaveRecipe.IsEnabled = false;
                    // Không cần làm gì thêm vì không còn lưới chi tiết
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi chọn đồ uống: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            await ResetFields();
        }

        private async Task ResetFields()
        {
            cbDrink.SelectedIndex = -1; 
            cbMaterial.SelectedIndex = -1;
            txtQuantity.Clear();
            txtActualPrice.Clear();
            dgRecipeSummary.SelectedItem = null;
            currentRecipe.Clear();
            dgCurrentRecipe.Items.Refresh();
            await UpdateCurrentRecipeCost();
        }

        private async Task LoadRecipeSummary()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    WITH RecipeCosts AS (
                        SELECT 
                            r.DrinkID,
                            d.IsRecipeActive, 
                            m.Name AS MaterialName,
                            r.Quantity AS RecipeQuantity, 
                            m.Quantity AS StockQuantity,
                            (r.Quantity * m.Price) AS Cost,
                            CONCAT(m.Name, '(', FORMAT(r.Quantity, 'G29'), ')') AS RecipePart
                        FROM Recipe r
                        JOIN Material m ON r.MaterialID = m.ID
                        JOIN Drink d ON r.DrinkID = d.ID 
                    )
                    SELECT 
                        d.ID AS DrinkID,
                        d.Name AS DrinkName,
                        d.ActualPrice,
                        d.IsRecipeActive,
                        ISNULL(STRING_AGG(rc.RecipePart, ' + '), 'None') AS RecipeSummary,
                        ISNULL(SUM(rc.Cost), 0) AS TotalCost, 
                        CASE WHEN MIN(rc.RecipeQuantity) IS NULL THEN 0 ELSE MIN(FLOOR(rc.StockQuantity / rc.RecipeQuantity)) END AS MaxCanMake,
                        d.DrinkCode
                    FROM Drink d 
                    LEFT JOIN RecipeCosts rc ON d.ID = rc.DrinkID
                    GROUP BY d.ID, d.Name, d.ActualPrice, d.DrinkCode, d.IsRecipeActive
                    ORDER BY d.Name;
                ";

                query = query.Replace("WHERE d.IsActive = 1", "WHERE d.ID IN (SELECT DISTINCT DrinkID FROM Recipe)");

                var tempTable = new DataTable();
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                await Task.Run(() => adapter.Fill(tempTable));

                tempTable.Columns.Add("STT", typeof(int));
                tempTable.Columns.Add("RecipeStatus", typeof(string));

                for (int i = 0; i < tempTable.Rows.Count; i++)
                {
                    var row = tempTable.Rows[i];
                    row["STT"] = i + 1;
                    bool isRecipeActive = (row["IsRecipeActive"] != DBNull.Value) ? Convert.ToBoolean(row["IsRecipeActive"]) : true;
                    row["RecipeStatus"] = isRecipeActive ? "Đang hoạt động" : "Đã ẩn";
                }

                recipeSummaryTable = tempTable;
                dgRecipeSummary.ItemsSource = recipeSummaryTable.DefaultView;
            }
        }

        private async Task LoadRecipeForDrink(int drinkId)
        {
            currentRecipe.Clear();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = @"SELECT r.MaterialID, m.Name AS MaterialName, r.Quantity, u.Name AS UnitName 
                                       FROM Recipe r 
                                       JOIN Material m ON r.MaterialID = m.ID 
                                       JOIN Unit u ON m.UnitID = u.ID
                                       WHERE r.DrinkID = @DrinkID";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@DrinkID", drinkId);
                await connection.OpenAsync();
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        currentRecipe.Add(new RecipeIngredient
                        {
                            MaterialID = reader.GetInt32(0),
                            MaterialName = reader.GetString(1),
                            Quantity = reader.GetDecimal(2),
                            UnitName = reader.GetString(3)
                        });
                    }
                }
            }
            dgCurrentRecipe.Items.Refresh();
            await UpdateCurrentRecipeCost();
        }

        private async void BtnAddIngredient_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cbMaterial.SelectedItem == null)
                {
                    MessageBox.Show("Vui lòng chọn một nguyên liệu.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!decimal.TryParse(txtQuantity.Text, out decimal quantity) || quantity <= 0)
                {
                    MessageBox.Show("Số lượng phải là một số dương hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var selectedMaterial = (DataRowView)cbMaterial.SelectedItem;
                int materialId = (int)selectedMaterial["ID"];

                if (currentRecipe.Any(i => i.MaterialID == materialId))
                {
                    if (MessageBox.Show("Nguyên liệu này đã có trong công thức. Bạn có muốn cộng dồn số lượng mới vào không?", "Xác nhận cộng dồn", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        var existingIngredient = currentRecipe.First(i => i.MaterialID == materialId);
                        existingIngredient.Quantity += quantity; 
                        ShowNotification($"Đã cập nhật: {existingIngredient.MaterialName} - Tổng số lượng: {existingIngredient.Quantity}");
                    }
                    else
                    {
                        return; 
                    }
                }
                else
                {
                    string unitName = "";
                    using (var connection = new SqlConnection(connectionString))
                    {
                        var command = new SqlCommand("SELECT u.Name FROM Material m JOIN Unit u ON m.UnitID = u.ID WHERE m.ID = @MaterialID", connection);
                        command.Parameters.AddWithValue("@MaterialID", materialId);
                        await connection.OpenAsync();
                        unitName = (await command.ExecuteScalarAsync())?.ToString() ?? "Không xác định";
                    }

                    currentRecipe.Add(new RecipeIngredient { MaterialID = materialId, MaterialName = selectedMaterial["Name"].ToString(), Quantity = quantity, UnitName = unitName });
                    ShowNotification($"Đã thêm: {selectedMaterial["Name"]} - Số lượng: {quantity}");
                }

                dgCurrentRecipe.Items.Refresh();
                await UpdateCurrentRecipeCost();

                txtQuantity.Clear();
                cbMaterial.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm nguyên liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnSaveRecipe_Click(object sender, RoutedEventArgs e)
        {
            if (cbDrink.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để lưu công thức.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (MessageBox.Show("Bạn có chắc chắn muốn lưu công thức này? Công thức cũ sẽ bị ghi đè.", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            if (!decimal.TryParse(txtActualPrice.Text, out _))
            {
                MessageBox.Show("Giá bán phải là một số hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int drinkId = (int)((DataRowView)cbDrink.SelectedItem)["ID"];
            btnSaveRecipe.IsEnabled = true;
            decimal newCostPrice = 0;

            if (currentRecipe.Any())
            {
                var materialIds = currentRecipe.Select(i => i.MaterialID).ToList();
                var prices = await GetMaterialPrices(materialIds);
                newCostPrice = currentRecipe.Sum(item => {
                    prices.TryGetValue(item.MaterialID, out decimal price);
                    return item.Quantity * price;
                });
            }

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var deleteCmd = new SqlCommand("DELETE FROM Recipe WHERE DrinkID = @DrinkID", connection, transaction);
                        deleteCmd.Parameters.AddWithValue("@DrinkID", drinkId);
                        await deleteCmd.ExecuteNonQueryAsync();

                        foreach (var item in currentRecipe)
                        {
                            var insertCmd = new SqlCommand("INSERT INTO Recipe (DrinkID, MaterialID, Quantity) VALUES (@DrinkID, @MaterialID, @Quantity)", connection, transaction);
                            insertCmd.Parameters.AddWithValue("@DrinkID", drinkId);
                            insertCmd.Parameters.AddWithValue("@MaterialID", item.MaterialID);
                            insertCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                            await insertCmd.ExecuteNonQueryAsync();
                        }

                        var updateDrinkCmd = new SqlCommand("UPDATE Drink SET RecipeCost = @RecipeCost, ActualPrice = @ActualPrice WHERE ID = @DrinkID", connection, transaction);
                        updateDrinkCmd.Parameters.AddWithValue("@RecipeCost", newCostPrice);
                        updateDrinkCmd.Parameters.AddWithValue("@ActualPrice", decimal.Parse(txtActualPrice.Text));
                        updateDrinkCmd.Parameters.AddWithValue("@DrinkID", drinkId);
                        await updateDrinkCmd.ExecuteNonQueryAsync();

                        transaction.Commit();
                        ShowNotification("Lưu công thức thành công!");
                        dgCurrentRecipe.Items.Refresh(); 
                        await LoadRecipeSummary(); 
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"Đã xảy ra lỗi khi lưu công thức: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void BtnRemoveRecipe_Click(object sender, RoutedEventArgs e)
        {
            if (cbDrink.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để gỡ công thức.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (MessageBox.Show("Bạn có chắc chắn muốn gỡ bỏ công thức của đồ uống này không? Tất cả nguyên liệu sẽ bị xóa khỏi công thức.", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            int drinkId = (int)((DataRowView)cbDrink.SelectedItem)["ID"];

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var deleteRecipeCmd = new SqlCommand("DELETE FROM Recipe WHERE DrinkID = @DrinkID", connection, transaction);
                        deleteRecipeCmd.Parameters.AddWithValue("@DrinkID", drinkId);
                        await deleteRecipeCmd.ExecuteNonQueryAsync();

                        var updateDrinkCmd = new SqlCommand("UPDATE Drink SET RecipeCost = 0 WHERE ID = @DrinkID", connection, transaction);
                        updateDrinkCmd.Parameters.AddWithValue("@DrinkID", drinkId);
                        await updateDrinkCmd.ExecuteNonQueryAsync();


                        transaction.Commit();
                        ShowNotification("Gỡ công thức thành công!");
                        await LoadRecipeForDrink(drinkId); 
                        await LoadRecipeSummary(); 
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"Đã xảy ra lỗi khi gỡ công thức: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void BtnToggleRecipeActive_Click(object sender, RoutedEventArgs e)
        {
            if (cbDrink.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedDrink = (DataRowView)cbDrink.SelectedItem;
            int drinkId = (int)selectedDrink["ID"];
            string drinkName = selectedDrink["Name"].ToString() ?? "Không tên";
            bool currentStatus = Convert.ToBoolean(selectedDrink["IsRecipeActive"]);
            string actionText = currentStatus ? "ẩn" : "hiển thị";

            if (MessageBox.Show($"Bạn có chắc chắn muốn {actionText} công thức của đồ uống '{drinkName}' không?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand("UPDATE Drink SET IsRecipeActive = @NewStatus WHERE ID = @DrinkID", connection);
                command.Parameters.AddWithValue("@NewStatus", !currentStatus);
                command.Parameters.AddWithValue("@DrinkID", drinkId);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                ShowNotification($"Đã {actionText} công thức thành công!");
                await LoadDrinksToComboBox(); 
                await LoadRecipeSummary();
                cbDrink.SelectedValue = drinkId; 
            }
        }

        private async void DeleteIngredient_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is RecipeIngredient ingredientToRemove)
            {
                currentRecipe.Remove(ingredientToRemove);
                dgCurrentRecipe.Items.Refresh();
                await UpdateCurrentRecipeCost();
            }
        }

        private async Task<Dictionary<int, decimal>> GetMaterialPrices(List<int> materialIds)
        {
            var prices = new Dictionary<int, decimal>();
            if (!materialIds.Any()) return prices;

            string idList = string.Join(",", materialIds);
            string query = $"SELECT ID, Price FROM Material WHERE ID IN ({idList})";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand(query, connection);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        prices[reader.GetInt32(0)] = reader.GetDecimal(1);
                    }
                }
            }
            return prices;
        }

        private async void DgRecipeSummary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (dgRecipeSummary.SelectedItem is DataRowView row)
                {
                    int drinkId = (int)row["DrinkID"];

                    txtDrinkCode.Text = (row["DrinkCode"] as string ?? "") + "_PC";
                    txtActualPrice.Text = Convert.ToDecimal(row["ActualPrice"]).ToString("G0");
                    
                    btnToggleRecipeActive.IsEnabled = true;
                    bool isRecipeActive = Convert.ToBoolean(row["IsRecipeActive"]);
                    btnSaveRecipe.IsEnabled = true;
                    btnToggleRecipeActive.Content = isRecipeActive ? "Ẩn Công thức" : "Hiện Công thức";

                    await LoadRecipeForDrink(drinkId); 

                    if ((int?)cbDrink.SelectedValue != drinkId)
                    {
                        cbDrink.SelectedValue = drinkId;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi chọn từ bảng tóm tắt: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateCurrentRecipeCost()
        {
            decimal totalCost = 0;
            if (currentRecipe.Any())
            {
                var materialIds = currentRecipe.Select(i => i.MaterialID).ToList();
                var prices = await GetMaterialPrices(materialIds);
                totalCost = currentRecipe.Sum(item => {
                    prices.TryGetValue(item.MaterialID, out decimal price);
                    return item.Quantity * price;
                });
            }
            txtCurrentRecipeCost.Text = $"Tổng chi phí: {totalCost:N0}";
        }

        private async void ShowNotification(string message, int displayTimeInMs = 2000)
        {
            notificationText.Text = message;
            notificationPopup.IsOpen = true;

            await Task.Delay(displayTimeInMs);

            notificationPopup.IsOpen = false;
        }
    }
}