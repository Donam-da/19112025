using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace namm
{
    public partial class InterfaceSettingsView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private Color selectedAppColor;
        private Color selectedLoginPanelColor;

        // Biến để lưu trữ dữ liệu ảnh đang được chọn, sẵn sàng để lưu
        private byte[]? _selectedImageData;
        private string? _selectedImageFileName;

        public InterfaceSettingsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettingsAsync();
            PopulateColorPalette(appColorPalette, AppColor_Click);
            PopulateColorPalette(loginPanelColorPalette, LoginPanelColor_Click);
        }

        private void PopulateColorPalette(Panel palette, RoutedEventHandler colorClickHandler)
        {
            List<Color> colors = new List<Color>
            {
                Colors.LightCoral, Colors.Khaki, Colors.LightGreen, Colors.PaleTurquoise, 
                Colors.LightSteelBlue, Colors.Plum, Colors.LightGray, Colors.MistyRose
            };

            foreach (var color in colors)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(color),
                    Width = 20,
                    Height = 20,
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(10),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                border.MouseLeftButtonDown += (s, e) => colorClickHandler(s, e);
                border.Tag = color;
                palette.Children.Add(border);
            }
        }

        private void AppColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Color color)
            {
                selectedAppColor = color;
                UpdateAppColor();
            }
        }

        private void LoginPanelColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Color color)
            {
                selectedLoginPanelColor = color;
                UpdateLoginPanelColor();
            }
        }

        private void AppColorAdjustment_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
            {
                UpdateAppColor();
            }
        }

        private void LoginPanelColorAdjustment_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
            {
                UpdateLoginPanelColor();
            }
        }

        private void UpdateAppColor()
        {
            Color adjustedColor = AdjustColor(selectedAppColor, sliderAppLightness.Value, sliderAppAlpha.Value);
            previewGroupBox.Background = new SolidColorBrush(adjustedColor);
            rectAppColorPreview.Fill = new SolidColorBrush(adjustedColor);
            txtAppBackgroundColorHex.Text = adjustedColor.ToString();
        }

        private void UpdateLoginPanelColor()
        {
            Color adjustedColor = AdjustColor(selectedLoginPanelColor, sliderLoginPanelLightness.Value, sliderLoginPanelAlpha.Value);
            previewIconBorder.Background = new SolidColorBrush(adjustedColor);
            rectLoginPanelColorPreview.Fill = new SolidColorBrush(adjustedColor);
            txtLoginPanelBackgroundColorHex.Text = adjustedColor.ToString();
        }

        private Color AdjustColor(Color baseColor, double lightness, double alpha)
        {
            // This is a simplified lightness adjustment.
            float factor = (float)(1 + lightness);
            byte r = (byte)Math.Max(0, Math.Min(255, baseColor.R * factor));
            byte g = (byte)Math.Max(0, Math.Min(255, baseColor.G * factor));
            byte b = (byte)Math.Max(0, Math.Min(255, baseColor.B * factor));
            byte a = (byte)Math.Max(0, Math.Min(255, 255 * alpha));

            return Color.FromArgb(a, r, g, b);
        }

        private void HexColor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (sender is TextBox textBox)
                {
                    UpdateColorFromHex(textBox);
                    // Đánh dấu đã xử lý để ngăn tiếng 'ding' khi nhấn Enter
                    e.Handled = true;
                    // Di chuyển focus ra khỏi TextBox để người dùng thấy kết quả ngay
                    textBox.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                }
            }
        }

        private void HexColor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                UpdateColorFromHex(textBox);
            }
        }

        private void UpdateColorFromHex(TextBox textBox)
        {
            try
            {
                // Sử dụng ColorConverter để phân tích chuỗi hex
                var newColor = (Color)ColorConverter.ConvertFromString(textBox.Text);

                if (textBox.Name == "txtAppBackgroundColorHex")
                {
                    selectedAppColor = newColor;
                    UpdateAppColor();
                }
                else if (textBox.Name == "txtLoginPanelBackgroundColorHex")
                {
                    selectedLoginPanelColor = newColor;
                    UpdateLoginPanelColor();
                }
            }
            catch (FormatException)
            {
                // Nếu định dạng không hợp lệ, hoàn nguyên textbox về màu hợp lệ cuối cùng
                if (textBox.Name == "txtAppBackgroundColorHex")
                {
                    textBox.Text = txtAppBackgroundColorHex.Text;
                }
                else if (textBox.Name == "txtLoginPanelBackgroundColorHex")
                {
                    textBox.Text = txtLoginPanelBackgroundColorHex.Text;
                }
            }
        }

        private void BtnSelectImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {                    
                    // Đọc dữ liệu ảnh vào mảng byte
                    _selectedImageData = File.ReadAllBytes(openFileDialog.FileName);
                    _selectedImageFileName = Path.GetFileName(openFileDialog.FileName);

                    // Cập nhật UI để xem trước
                    txtImagePath.Text = openFileDialog.FileName;
                    imgPreview.Source = LoadImageFromBytes(_selectedImageData);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi đọc file ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    _selectedImageData = null;
                    _selectedImageFileName = null;
                }
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save App Background Color
                Properties.Settings.Default.AppBackgroundColor = txtAppBackgroundColorHex.Text;
                // Save Login Panel Color
                Properties.Settings.Default.LoginIconBgColor = txtLoginPanelBackgroundColorHex.Text;

                // Nếu có ảnh mới được chọn, lưu vào DB
                if (_selectedImageData != null && _selectedImageFileName != null)
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        using (var transaction = connection.BeginTransaction())
                        {
                            // 1. Bỏ kích hoạt tất cả các ảnh đăng nhập cũ
                            var cmdDeactivate = new SqlCommand("UPDATE InterfaceImages SET IsActiveForLogin = 0 WHERE IsActiveForLogin = 1", connection, transaction);
                            await cmdDeactivate.ExecuteNonQueryAsync();

                            // 2. Thêm ảnh mới và kích hoạt nó
                            var cmdInsert = new SqlCommand("INSERT INTO InterfaceImages (ImageName, ImageData, ContentType, IsActiveForLogin) VALUES (@Name, @Data, @Type, 1)", connection, transaction);
                            cmdInsert.Parameters.AddWithValue("@Name", _selectedImageFileName);
                            cmdInsert.Parameters.AddWithValue("@Data", _selectedImageData);
                            cmdInsert.Parameters.AddWithValue("@Type", GetMimeType(_selectedImageFileName)); // Lấy kiểu content
                            await cmdInsert.ExecuteNonQueryAsync();

                            transaction.Commit();
                        }
                    }
                }

                Properties.Settings.Default.Save();
                MessageBox.Show("Đã lưu cài đặt thành công! Vui lòng khởi động lại ứng dụng để các thay đổi có hiệu lực.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu cài đặt: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Bạn có chắc chắn muốn đặt lại tất cả cài đặt giao diện về giá trị mặc định không?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Properties.Settings.Default.Reset();
                Properties.Settings.Default.Save();

                // Xóa ảnh đang active trong DB
                try
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        var cmdDeactivate = new SqlCommand("UPDATE InterfaceImages SET IsActiveForLogin = 0 WHERE IsActiveForLogin = 1", connection);
                        await cmdDeactivate.ExecuteNonQueryAsync();
                    }
                }
                catch (Exception ex)
                {
                     MessageBox.Show($"Không thể đặt lại ảnh trong CSDL: {ex.Message}", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                LoadCurrentSettingsAsync();
                MessageBox.Show("Cài đặt đã được đặt lại về mặc định.", "Hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void LoadCurrentSettingsAsync()
        {
            try
            {
                // Load colors
                var appBgColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.AppBackgroundColor);
                var loginPanelColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.LoginIconBgColor);
                selectedAppColor = appBgColor;
                selectedLoginPanelColor = loginPanelColor;
                UpdateAppColor();
                UpdateLoginPanelColor();

                // Load active image from DB
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand("SELECT ImageData, ImageName FROM InterfaceImages WHERE IsActiveForLogin = 1", connection);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var imageData = (byte[])reader["ImageData"];
                            var imageName = reader["ImageName"].ToString();
                            imgPreview.Source = LoadImageFromBytes(imageData);
                            txtImagePath.Text = $"Ảnh đang dùng từ CSDL: {imageName}";
                        }
                        else
                        {
                            // Nếu không có ảnh trong DB, dùng ảnh mặc định
                            imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));
                            txtImagePath.Text = "(Chưa có ảnh nào được thiết lập)";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load settings, using defaults. Error: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                // In case of error, use hardcoded defaults
                selectedAppColor = Colors.LightGray;
                selectedLoginPanelColor = (Color)ColorConverter.ConvertFromString("#D2B48C");
                UpdateAppColor();
                UpdateLoginPanelColor();
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
            image.Freeze(); // Tối ưu hóa hiệu suất
            return image;
        }

        private string GetMimeType(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                default:
                    return "application/octet-stream"; // Kiểu mặc định cho file nhị phân
            }
        }
    }
}