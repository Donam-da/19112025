﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace namm
{
    public partial class ProfitStatisticsView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable profitDataTable = new DataTable();

        public ProfitStatisticsView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            dpStartDate.SelectedDateChanged -= DpStartDate_SelectedDateChanged; 
            dpStartDate.SelectedDateChanged -= Filters_Changed;
            dpEndDate.SelectedDateChanged -= Filters_Changed;
            cbFilterDrinkName.SelectionChanged -= Filters_Changed;
            cbFilterDrinkType.SelectionChanged -= Filters_Changed;
            cbFilterCategory.SelectionChanged -= Filters_Changed;

            await LoadFilterComboBoxes();

            DateTime? firstInvoiceDate = await GetFirstInvoiceDateAsync();

            dpStartDate.SelectedDate = firstInvoiceDate?.Date ?? DateTime.Today;
            dpEndDate.SelectedDate = DateTime.Today;
            
            dpStartDate.SelectedDateChanged += DpStartDate_SelectedDateChanged; 
            dpStartDate.SelectedDateChanged += Filters_Changed;
            dpEndDate.SelectedDateChanged += Filters_Changed;
            cbFilterDrinkName.SelectionChanged += Filters_Changed;
            cbFilterDrinkType.SelectionChanged += Filters_Changed;
            cbFilterCategory.SelectionChanged += Filters_Changed;

            await FilterData();
        }

        private async Task<DateTime?> GetFirstInvoiceDateAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT MIN(DateCheckOut) FROM Bill WHERE Status = 1";
                var command = new SqlCommand(query, connection);
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    return (DateTime)result;
                }
            }
            return null; 
        }

        private async Task LoadFilterComboBoxes()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT 0 AS ID, N'Tất cả' AS Name, 0 AS SortOrder
                    UNION ALL
                    SELECT DISTINCT ID, Name, 1 AS SortOrder FROM Drink ORDER BY SortOrder, Name";
                var adapter = new SqlDataAdapter(query, connection);
                var drinkTable = new DataTable();
                await Task.Run(() => adapter.Fill(drinkTable));
                cbFilterDrinkName.ItemsSource = drinkTable.DefaultView;
                cbFilterDrinkName.SelectedIndex = 0;
            }

            cbFilterDrinkType.Items.Add("Tất cả");
            cbFilterDrinkType.Items.Add("Pha chế");
            cbFilterDrinkType.Items.Add("Nguyên bản");
            cbFilterDrinkType.SelectedIndex = 0;

            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT 0 AS ID, N'Tất cả' AS Name, 0 AS SortOrder 
                    UNION ALL 
                    SELECT ID, Name, 1 AS SortOrder FROM Category WHERE IsActive = 1 ORDER BY SortOrder, Name";
                var adapter = new SqlDataAdapter(query, connection);
                var categoryTable = new DataTable();
                await Task.Run(() => adapter.Fill(categoryTable));
                cbFilterCategory.ItemsSource = categoryTable.DefaultView;
                cbFilterCategory.SelectedIndex = 0;
            }
        }

        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            await FilterData();
        }

        private async void Filters_Changed(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded) return;
            await FilterData();
        }

        private async Task FilterData()
        {
            DateTime? startDate = dpStartDate.SelectedDate?.Date;
            DateTime? endDate = dpEndDate.SelectedDate?.Date.AddDays(1).AddTicks(-1); 
            int? drinkIdFilter = (cbFilterDrinkName.SelectedValue != null && (int)cbFilterDrinkName.SelectedValue > 0) ? (int)cbFilterDrinkName.SelectedValue : (int?)null;
            string? drinkTypeFilter = cbFilterDrinkType.SelectedIndex > 0 ? cbFilterDrinkType.SelectedItem.ToString() : null;
            int? categoryFilter = (cbFilterCategory.SelectedValue != null && (int)cbFilterCategory.SelectedValue > 0) ? (int)cbFilterCategory.SelectedValue : (int?)null;

            await LoadProfitDataAsync(startDate, endDate, drinkIdFilter, drinkTypeFilter, categoryFilter);
        }

        private async Task LoadProfitDataAsync(DateTime? startDate, DateTime? endDate, int? drinkId, string? drinkType, int? categoryId)
        {
            var parameters = new List<SqlParameter>();

            using (var connection = new SqlConnection(connectionString))
            {
                var queryBuilder = new System.Text.StringBuilder(@"
                    WITH BillItemDetails AS (
                        SELECT
                            bi.BillID, bi.DrinkID, bi.DrinkType, bi.Quantity, bi.Price,
                            (bi.Quantity * bi.Price) AS ItemRevenue,
                            b.SubTotal, b.TotalAmount
                        FROM BillInfo bi
                        JOIN Bill b ON bi.BillID = b.ID WHERE b.Status = 1
                    )
                    SELECT 
                    d.Name AS DrinkName, 
                    d.CategoryID,
                        bid.DrinkType,
                        SUM(bid.Quantity) AS TotalQuantitySold,
                    ISNULL(CASE 
                        WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost 
                        WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice 
                        ELSE 0 
                    END, 0) AS UnitCost,
                    SUM(bid.Quantity * ISNULL(CASE 
                        WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost 
                        WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice 
                        ELSE 0 
                    END, 0)) AS TotalCost,
                    SUM(
                        CASE 
                            WHEN bid.SubTotal > 0 THEN 
                                (CAST(bid.ItemRevenue AS DECIMAL(18, 2)) * CAST(bid.TotalAmount AS DECIMAL(18, 2))) / CAST(bid.SubTotal AS DECIMAL(18, 2))
                            ELSE bid.ItemRevenue 
                        END
                    ) AS TotalRevenue
                    FROM BillItemDetails bid
                    JOIN Drink d ON bid.DrinkID = d.ID
                    WHERE 1=1 ");

                if (startDate.HasValue)
                {
                    queryBuilder.Append(" AND bid.BillID IN (SELECT ID FROM Bill WHERE DateCheckOut >= @StartDate)");
                    parameters.Add(new SqlParameter("@StartDate", startDate.Value));
                }

                if (endDate.HasValue)
                {
                    queryBuilder.Append(" AND bid.BillID IN (SELECT ID FROM Bill WHERE DateCheckOut <= @EndDate)");
                    parameters.Add(new SqlParameter("@EndDate", endDate.Value));
                }

                if (drinkId.HasValue)
                {
                    queryBuilder.Append(" AND d.ID = @DrinkID");
                    parameters.Add(new SqlParameter("@DrinkID", drinkId.Value));
                }
                if (!string.IsNullOrWhiteSpace(drinkType))
                {
                    queryBuilder.Append(" AND bid.DrinkType = @DrinkType");
                    parameters.Add(new SqlParameter("@DrinkType", drinkType));
                }
                if (categoryId.HasValue)
                {
                    queryBuilder.Append(" AND d.CategoryID = @CategoryID");
                    parameters.Add(new SqlParameter("@CategoryID", categoryId.Value));
                }

                queryBuilder.Append(@"
                    GROUP BY d.Name, d.CategoryID, bid.DrinkType, d.RecipeCost, d.OriginalPrice
                    ORDER BY Profit DESC;
                ");

                string finalQuery = queryBuilder.ToString().Replace("ORDER BY Profit DESC", 
                    @"ORDER BY 
                        (SUM(
                            CASE 
                                WHEN bid.SubTotal > 0 THEN 
                                    (CAST(bid.ItemRevenue AS DECIMAL(18, 2)) * CAST(bid.TotalAmount AS DECIMAL(18, 2))) / CAST(bid.SubTotal AS DECIMAL(18, 2))
                                ELSE bid.ItemRevenue 
                            END
                        )) 
                        - 
                        (SUM(bid.Quantity * ISNULL(CASE WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice ELSE 0 END, 0))) 
                    DESC");


                var adapter = new SqlDataAdapter(finalQuery, connection);
                adapter.SelectCommand.Parameters.AddRange(parameters.ToArray());

                profitDataTable = new DataTable();
                profitDataTable.Columns.Add("STT", typeof(int));
                profitDataTable.Columns.Add("Profit", typeof(decimal));
                profitDataTable.Columns.Add("ProfitMargin", typeof(decimal));

                await Task.Run(() => adapter.Fill(profitDataTable));

                for (int i = 0; i < profitDataTable.Rows.Count; i++)
                {
                    var row = profitDataTable.Rows[i];
                    row["STT"] = i + 1;

                    decimal totalRevenue = Convert.ToDecimal(row["TotalRevenue"]);
                    decimal totalCost = Convert.ToDecimal(row["TotalCost"]);
                    decimal profit = totalRevenue - totalCost;
                    row["Profit"] = profit;

                    row["ProfitMargin"] = (totalRevenue > 0) ? (profit / totalRevenue) * 100 : 0;
                }

                dgProfitStats.ItemsSource = profitDataTable.DefaultView;
                CalculateTotals();
            }
        }
        private void CalculateTotals()
        {
            decimal totalRevenue = 0;
            decimal totalCost = 0;

            foreach (DataRow row in profitDataTable.Rows)
            {
                if (row["TotalRevenue"] != DBNull.Value)
                {
                    totalRevenue += Convert.ToDecimal(row["TotalRevenue"]);
                }
                if (row["TotalCost"] != DBNull.Value)
                {
                    totalCost += Convert.ToDecimal(row["TotalCost"]);
                }
            }
            tbTotalRevenue.Text = $"{totalRevenue:N0} VNĐ";
            tbTotalCost.Text = $"{totalCost:N0} VNĐ";
            tbTotalProfit.Text = $"{totalRevenue - totalCost:N0} VNĐ";
        }

        private void DpStartDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpStartDate.SelectedDate.HasValue)
            {
                dpEndDate.DisplayDateStart = dpStartDate.SelectedDate;

                if (dpEndDate.SelectedDate < dpStartDate.SelectedDate)
                {
                    dpEndDate.SelectedDate = dpStartDate.SelectedDate;
                }
            }
        }
    }
}