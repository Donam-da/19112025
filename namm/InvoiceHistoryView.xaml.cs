﻿﻿﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class InvoiceHistoryView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable invoiceDataTable = new DataTable();

        public InvoiceHistoryView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            DateTime? firstInvoiceDate = await GetFirstInvoiceDateAsync();

            dpStartDate.SelectedDate = firstInvoiceDate?.Date ?? DateTime.Today;
            dpEndDate.SelectedDate = DateTime.Today;

            BtnFilter_Click(sender, e);
        }

        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (dpStartDate.SelectedDate == null || dpEndDate.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày bắt đầu và ngày kết thúc.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime startDate = dpStartDate.SelectedDate.Value.Date;
            DateTime endDate = dpEndDate.SelectedDate.Value.Date.AddDays(1).AddTicks(-1); 

            await LoadInvoicesAsync(startDate, endDate);
        }

        private async Task LoadInvoicesAsync(DateTime startDate, DateTime endDate)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT 
                        b.ID, 
                        ISNULL(c.CustomerCode, b.GuestCustomerCode) as CustomerCode,
                        b.DateCheckOut, 
                        ISNULL(c.Name, N'Khách vãng lai') AS CustomerName, 
                        tf.Name AS TableName, 
                        b.TotalAmount,
                        b.SubTotal
                    FROM Bill b
                    LEFT JOIN Customer c ON b.IdCustomer = c.ID
                    JOIN TableFood tf ON b.TableID = tf.ID
                    WHERE b.Status = 1 AND b.DateCheckOut BETWEEN @StartDate AND @EndDate
                    ORDER BY b.DateCheckOut DESC";

                var adapter = new SqlDataAdapter(query, connection);
                adapter.SelectCommand.Parameters.AddWithValue("@StartDate", startDate);
                adapter.SelectCommand.Parameters.AddWithValue("@EndDate", endDate);

                invoiceDataTable = new DataTable();
                invoiceDataTable.Columns.Add("STT", typeof(int));

                await Task.Run(() => adapter.Fill(invoiceDataTable));

                for (int i = 0; i < invoiceDataTable.Rows.Count; i++)
                {
                    string tableName = invoiceDataTable.Rows[i]["TableName"].ToString();
                    invoiceDataTable.Rows[i]["TableName"] = tableName.Replace("Bàn ", "");
                }

                dgInvoices.ItemsSource = invoiceDataTable.DefaultView;
                CalculateTotalRevenue();
                ClearSelection();
            }
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

        private async void DgInvoices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgInvoices.SelectedItem is DataRowView selectedInvoice)
            {
                int billId = (int)selectedInvoice["ID"];
                var detailsView = await LoadInvoiceDetailsAsync(billId);

                invoicePreview.DisplayInvoice(selectedInvoice, detailsView);
            }
            else
            {
                ClearSelection();
            }
        }

        private async Task<DataView> LoadInvoiceDetailsAsync(int billId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT 
                        d.Name + N' (' + 
                            CASE bi.DrinkType 
                                WHEN N'Pha chế' THEN N'PC' 
                                WHEN N'Nguyên bản' THEN N'NB' 
                                ELSE bi.DrinkType 
                            END 
                        + N')' AS DrinkName, 
                        bi.Quantity, 
                        bi.Price, 
                        (bi.Quantity * bi.Price) AS TotalPrice
                    FROM BillInfo bi
                    JOIN Drink d ON bi.DrinkID = d.ID
                    WHERE bi.BillID = @BillID";

                var adapter = new SqlDataAdapter(query, connection);
                adapter.SelectCommand.Parameters.AddWithValue("@BillID", billId);

                var detailsTable = new DataTable();
                await Task.Run(() => adapter.Fill(detailsTable));

                return detailsTable.DefaultView;
            }
        }

        private void CalculateTotalRevenue()
        {
            decimal total = 0;
            foreach (DataRow row in invoiceDataTable.Rows)
            {
                total += (decimal)row["TotalAmount"];
            }
            tbTotalRevenue.Text = $"{total:N0} VNĐ";
        }

        private void ClearSelection()
        {
            invoicePreview.Clear();
        }

        private void DgInvoices_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView.Row["STT"] = e.Row.GetIndex() + 1;
            }
        }
    }
}