﻿﻿﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class TableView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public TableView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTables();
        }

        private void LoadTables()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT 
                        tf.ID, 
                        tf.Name, 
                        tf.Capacity, 
                        CASE 
                            WHEN EXISTS (SELECT 1 FROM Bill b WHERE b.TableID = tf.ID AND b.Status = 0) THEN N'Có người' 
                            ELSE N'Trống' 
                        END AS Status
                    FROM TableFood tf";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                dataTable.Columns.Add("STT", typeof(int));
                adapter.Fill(dataTable);
                dgTables.ItemsSource = dataTable.DefaultView;
            }
        }

        private void DgTables_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgTables.SelectedItem is DataRowView row)
            {
                string fullName = row["Name"].ToString() ?? "";
                string tableNumber = fullName.Replace("Bàn ", "").Trim();
                txtName.Text = tableNumber;

                txtCapacity.Text = row["Capacity"].ToString();
                txtStatus.Text = row["Status"].ToString();

                btnAdd.IsEnabled = false;
                btnEdit.IsEnabled = true;
                btnDelete.IsEnabled = true;
            }
            else
            {
                ResetFields();
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtName.Text, out _))
            {
                MessageBox.Show("Mã bàn phải là một số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "INSERT INTO TableFood (Name, Capacity, Status) VALUES (@Name, @Capacity, @Status)";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Name", "Bàn " + txtName.Text);
                command.Parameters.AddWithValue("@Capacity", Convert.ToInt32(txtCapacity.Text));
                command.Parameters.AddWithValue("@Status", "Trống"); 

                try
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Thêm bàn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadTables();
                    ResetFields();
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi thêm bàn: {ex.Message}\n\nCó thể tên bàn này đã tồn tại.", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgTables.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một bàn để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtName.Text, out _))
            {
                MessageBox.Show("Mã bàn phải là một số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DataRowView row = (DataRowView)dgTables.SelectedItem;
            int tableId = (int)row["ID"];

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "UPDATE TableFood SET Name = @Name, Capacity = @Capacity WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", tableId);
                command.Parameters.AddWithValue("@Name", "Bàn " + txtName.Text);
                command.Parameters.AddWithValue("@Capacity", Convert.ToInt32(txtCapacity.Text));

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Cập nhật thông tin bàn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadTables();
                ResetFields();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgTables.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một bàn để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Bạn có chắc chắn muốn xóa bàn này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DataRowView row = (DataRowView)dgTables.SelectedItem;
                int tableId = (int)row["ID"];

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "DELETE FROM TableFood WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", tableId);
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Xóa bàn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadTables();
                    ResetFields();
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
            txtCapacity.Clear();
            txtStatus.Clear();
            dgTables.SelectedItem = null;

            btnAdd.IsEnabled = true;
            btnEdit.IsEnabled = false;
            btnDelete.IsEnabled = false;
        }
    }
}