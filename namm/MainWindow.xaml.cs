﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Data.SqlClient;
using System.IO; // Added for File.Exists
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using namm.Properties;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace namm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public MainWindow()
        {
            InitializeComponent();
            LoadRememberedUser();
            this.Loaded += MainWindow_Loaded; // Attach Loaded event
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCustomInterface(); // Await the async method
            txtUsername.Focus(); // Focus vào ô username khi cửa sổ được tải
        }

        private async Task LoadCustomInterface()
        {
            try
            {
                // Tải màu sắc
                string bgColor = Properties.Settings.Default.LoginIconBgColor;
                iconBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(bgColor);

                // Tải ảnh từ cơ sở dữ liệu
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(); // Use async version
                    var command = new SqlCommand("SELECT ImageData FROM InterfaceImages WHERE IsActiveForLogin = 1", connection);
                    command.CommandTimeout = 120; // Tăng thời gian chờ lên 120 giây
                    var imageData = await command.ExecuteScalarAsync() as byte[]; // Use async version

                    if (imageData != null && imageData.Length > 0)
                    {
                        imgLoginIcon.Source = await Task.Run(() => LoadImageFromBytes(imageData)); // Offload image conversion
                    }
                    else
                    {
                        imgLoginIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));
                    }
                }
            }
            catch (Exception ex)
            {
                // Nếu có lỗi (ví dụ: file ảnh bị xóa), sử dụng ảnh mặc định
                imgLoginIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));
                Debug.WriteLine($"Lỗi khi tải giao diện tùy chỉnh: {ex.Message}");
            }
        }

        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));

            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }

        private void LoadRememberedUser()
        {
            if (Settings.Default.RememberMe)
            {
                txtUsername.Text = Settings.Default.Username;
                pwbPassword.Password = Settings.Default.Password;
                chkRememberMe.IsChecked = true;
            }
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text;
            string password = pwbPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vui lòng nhập tên đăng nhập và mật khẩu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AccountDTO? loginAccount = CheckLogin(username, password);
            if (loginAccount != null)
            {
                // Lưu hoặc xóa cài đặt tùy thuộc vào checkbox
                if (chkRememberMe.IsChecked == true)
                {
                    Settings.Default.Username = username;
                    Settings.Default.Password = password; // Cảnh báo: Lưu mật khẩu dạng plain text không an toàn
                    Settings.Default.RememberMe = true;
                }
                else
                {
                    Settings.Default.Username = "";
                    Settings.Default.Password = "";
                    Settings.Default.RememberMe = false;
                }
                Settings.Default.Save();

                MainAppWindow mainApp = new MainAppWindow(loginAccount);
                mainApp.Show();
                this.Close(); // Đóng cửa sổ đăng nhập
            }
            else
            {
                MessageBox.Show("Tên đăng nhập hoặc mật khẩu không chính xác.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private AccountDTO? CheckLogin(string username, string password)
        {
            AccountDTO? account = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT * FROM Account WHERE UserName=@UserName AND Password=@Password";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserName", username);
                command.Parameters.AddWithValue("@Password", password); // Nhắc lại: nên hash mật khẩu

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    account = new AccountDTO
                    {
                        UserName = reader["UserName"].ToString() ?? "",
                        DisplayName = reader["DisplayName"].ToString() ?? "",
                        Type = (int)reader["Type"],
                    };
                }
            }
            return account;
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
