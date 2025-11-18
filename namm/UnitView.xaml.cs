﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class UnitView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable unitDataTable = new DataTable();

        public UnitView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUnitsAsync();
            ResetFields(); 
        }

        private async Task LoadUnitsAsync()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT ID, Name, Abbreviation, Description, IsActive FROM Unit";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                unitDataTable = new DataTable();
                unitDataTable.Columns.Add("STT", typeof(int)); 
                unitDataTable.Columns.Add("StatusText", typeof(string)); 
                await Task.Run(() => adapter.Fill(unitDataTable));

                UpdateStatusText();
                dgUnits.ItemsSource = unitDataTable.DefaultView;
            }
        }

        private void DgUnits_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgUnits_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgUnits.SelectedItem is DataRowView row)
            {
                txtName.Text = row["Name"].ToString();
                txtAbbreviation.Text = row["Abbreviation"].ToString();
                txtDescription.Text = row["Description"].ToString();
                chkIsActive.IsChecked = (bool)row["IsActive"];

                BtnAdd.IsEnabled = false;
                BtnEdit.IsEnabled = true;
                BtnDelete.IsEnabled = true;
            }
            else
            {
                ResetFields();
            }
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Tên đơn vị tính không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "INSERT INTO Unit (Name, Abbreviation, Description, IsActive) VALUES (@Name, @Abbreviation, @Description, @IsActive)";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Name", txtName.Text);
                    command.Parameters.AddWithValue("@Abbreviation", string.IsNullOrWhiteSpace(txtAbbreviation.Text) ? (object)DBNull.Value : txtAbbreviation.Text);
                    command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text);
                    command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);


                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    MessageBox.Show("Thêm đơn vị tính thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadUnitsAsync();
                    ResetFields();
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Lỗi khi thêm: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgUnits.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đơn vị tính để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DataRowView row = (DataRowView)dgUnits.SelectedItem;
            int unitId = (int)row["ID"];

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "UPDATE Unit SET Name = @Name, Abbreviation = @Abbreviation, Description = @Description, IsActive = @IsActive WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", unitId);
                    command.Parameters.AddWithValue("@Name", txtName.Text);
                    command.Parameters.AddWithValue("@Abbreviation", string.IsNullOrWhiteSpace(txtAbbreviation.Text) ? (object)DBNull.Value : txtAbbreviation.Text);
                    command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text);
                    command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    MessageBox.Show("Cập nhật đơn vị tính thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadUnitsAsync();
                    ResetFields();
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Lỗi khi cập nhật: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgUnits.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đơn vị tính để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Bạn có chắc chắn muốn xóa đơn vị tính này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DataRowView row = (DataRowView)dgUnits.SelectedItem;
                int unitId = (int)row["ID"];

                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        string query = "DELETE FROM Unit WHERE ID = @ID";
                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@ID", unitId);
                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                        MessageBox.Show("Xóa đơn vị tính thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadUnitsAsync();
                        ResetFields();
                    }
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Không thể xóa đơn vị tính này vì đang được sử dụng ở nơi khác.\n\nChi tiết: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        private void ResetFields()
        {
            txtName.Clear();
            txtAbbreviation.Clear();
            txtDescription.Clear();
            chkIsActive.IsChecked = true;
            dgUnits.SelectedItem = null;

            BtnAdd.IsEnabled = true;
            BtnEdit.IsEnabled = false;
            BtnDelete.IsEnabled = false;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = txtSearch.Text;
            if (unitDataTable != null)
            {
                if (string.IsNullOrEmpty(filter))
                {
                    unitDataTable.DefaultView.RowFilter = "";
                }
                else
                {
                    unitDataTable.DefaultView.RowFilter = $"Name LIKE '%{filter.Replace("'", "''")}%'";
                }
            }
        }

        private void UpdateStatusText()
        {
            foreach (DataRow row in unitDataTable.Rows)
            {
                row["StatusText"] = (bool)row["IsActive"] ? "Kích hoạt" : "Vô hiệu hóa";
            }
        }
    }
}