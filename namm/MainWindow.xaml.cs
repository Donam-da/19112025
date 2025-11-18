﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Data.SqlClient;
using System.IO; 
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
            this.Loaded += MainWindow_Loaded; 
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCustomInterface(); 
            txtUsername.Focus(); 
        }

        private async Task LoadCustomInterface()
        {
            try
            {
                string bgColor = Properties.Settings.Default.LoginIconBgColor;
                iconBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(bgColor);

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(); 
                    var command = new SqlCommand("SELECT ImageData FROM InterfaceImages WHERE IsActiveForLogin = 1", connection);
                    command.CommandTimeout = 120; 
                    var imageData = await command.ExecuteScalarAsync() as byte[]; 

                    if (imageData != null && imageData.Length > 0)
                    {
                        imgLoginIcon.Source = await Task.Run(() => LoadImageFromBytes(imageData)); 
                    }
                    else
                    {
                        imgLoginIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));
                    }
                }
            }
            catch (Exception ex)
            {
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
                if (chkRememberMe.IsChecked == true)
                {
                    Settings.Default.Username = username;
                    Settings.Default.Password = password; 
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
                this.Close(); 
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
                command.Parameters.AddWithValue("@Password", password); 

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
