using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace namm
{
    public class SavedImage
    {
        public int ID { get; set; }
        public string ImageName { get; set; } = string.Empty;
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public BitmapImage? Thumbnail { get; set; }
    }

    public partial class InterfaceSettingsView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private Color selectedAppColor;
        private Color selectedLoginPanelColor;

        private byte[]? _selectedImageData;
        private string? _selectedImageFileName;
        private int? _selectedSavedImageId;

        public InterfaceSettingsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettingsAsync();
            PopulateColorPalette(appColorPalette, AppColor_Click);
            PopulateColorPalette(loginPanelColorPalette, LoginPanelColor_Click);
            LoadSavedImagesAsync();
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
                    e.Handled = true;
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

        private async void BtnSelectImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _selectedImageData = await Task.Run(() => File.ReadAllBytes(openFileDialog.FileName)); 
                    _selectedImageFileName = Path.GetFileName(openFileDialog.FileName);

                    txtImagePath.Text = openFileDialog.FileName;
                    imgPreview.Source = await Task.Run(() => LoadImageFromBytes(_selectedImageData)); 

                    _selectedSavedImageId = null;
                    lvSavedImages.SelectedItem = null;
                    btnAddImageToLibrary.IsEnabled = true; 
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi đọc file ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    _selectedImageData = null;
                    _selectedImageFileName = null;
                }
            }
        }

        private async void BtnAddImageToLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedImageData == null || string.IsNullOrEmpty(_selectedImageFileName))
            {
                MessageBox.Show("Vui lòng chọn một ảnh từ máy tính trước khi thêm vào thư viện.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Bạn có muốn thêm ảnh '{_selectedImageFileName}' vào thư viện ảnh không?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var cmdInsert = new SqlCommand("INSERT INTO InterfaceImages (ImageName, ImageData, ContentType, IsActiveForLogin) OUTPUT INSERTED.ID VALUES (@Name, @Data, @Type, 0)", connection);
                    cmdInsert.Parameters.AddWithValue("@Name", _selectedImageFileName);
                    cmdInsert.Parameters.AddWithValue("@Data", _selectedImageData);
                    cmdInsert.Parameters.AddWithValue("@Type", GetMimeType(_selectedImageFileName));
                    cmdInsert.CommandTimeout = 120;

                    int newImageId = (int)await cmdInsert.ExecuteScalarAsync();

                    MessageBox.Show($"Ảnh '{_selectedImageFileName}' đã được thêm vào thư viện thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                    _selectedImageData = null;
                    _selectedImageFileName = null;
                    txtImagePath.Text = "(Chưa chọn ảnh nào)";
                    btnAddImageToLibrary.IsEnabled = false; 

                    await LoadSavedImagesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm ảnh vào thư viện: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Properties.Settings.Default.AppBackgroundColor = txtAppBackgroundColorHex.Text;
                Properties.Settings.Default.LoginIconBgColor = txtLoginPanelBackgroundColorHex.Text;

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        var cmdDeactivate = new SqlCommand("UPDATE InterfaceImages SET IsActiveForLogin = 0 WHERE IsActiveForLogin = 1", connection, transaction);
                            cmdDeactivate.CommandTimeout = 120; 
                        await cmdDeactivate.ExecuteNonQueryAsync();

                        if (_selectedImageData != null && _selectedImageFileName != null)
                        {
                            var cmdInsert = new SqlCommand("INSERT INTO InterfaceImages (ImageName, ImageData, ContentType, IsActiveForLogin) OUTPUT INSERTED.ID VALUES (@Name, @Data, @Type, 1)", connection, transaction);
                            cmdInsert.Parameters.AddWithValue("@Name", _selectedImageFileName);
                            cmdInsert.CommandTimeout = 120; 
                            cmdInsert.Parameters.AddWithValue("@Data", _selectedImageData);
                            cmdInsert.Parameters.AddWithValue("@Type", GetMimeType(_selectedImageFileName));
                            var newImageId = (int)await cmdInsert.ExecuteScalarAsync();
                            
                            await LoadSavedImagesAsync();
                        }
                        else if (_selectedSavedImageId.HasValue)
                        {
                            var cmdActivate = new SqlCommand("UPDATE InterfaceImages SET IsActiveForLogin = 1 WHERE ID = @ID", connection, transaction);
                            cmdActivate.CommandTimeout = 120; 
                            cmdActivate.Parameters.AddWithValue("@ID", _selectedSavedImageId.Value);
                            await cmdActivate.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();

                        _selectedImageData = null;
                        _selectedImageFileName = null;
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

                await LoadCurrentSettingsAsync();
                MessageBox.Show("Cài đặt đã được đặt lại về mặc định.", "Hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task LoadCurrentSettingsAsync()
        {
            try
            {
                var appBgColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.AppBackgroundColor);
                var loginPanelColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.LoginIconBgColor);
                selectedAppColor = appBgColor;
                selectedLoginPanelColor = loginPanelColor;
                UpdateAppColor();
                UpdateLoginPanelColor();

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand("SELECT ImageData, ImageName FROM InterfaceImages WHERE IsActiveForLogin = 1", connection);
                    command.CommandTimeout = 120; 
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var imageData = (byte[])reader["ImageData"];
                            var imageName = reader["ImageName"].ToString();
                            imgPreview.Source = await Task.Run(() => LoadImageFromBytes(imageData));
                            txtImagePath.Text = $"Ảnh đang dùng từ CSDL: {imageName}";
                        }
                        else
                        {
                            imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));
                            txtImagePath.Text = "(Chưa có ảnh nào được thiết lập)";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load settings, using defaults. Error: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                selectedAppColor = Colors.LightGray;
                selectedLoginPanelColor = (Color)ColorConverter.ConvertFromString("#D2B48C");
                UpdateAppColor();
                UpdateLoginPanelColor();
            }
        }

        private async Task LoadSavedImagesAsync()
        {
            var savedImages = new ObservableCollection<SavedImage>();
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand("SELECT ID, ImageName, ImageData FROM InterfaceImages ORDER BY DateCreated DESC", connection);
                    command.CommandTimeout = 120; 
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var imageData = (byte[])reader["ImageData"];
                            var savedImage = new SavedImage
                            {
                                ID = reader.GetInt32(0),
                                ImageName = reader.GetString(1),
                                ImageData = imageData,
                                Thumbnail = await Task.Run(() => LoadImageFromBytes(imageData)) 
                            };
                            savedImages.Add(savedImage);
                        }
                    }
                }
                lvSavedImages.ItemsSource = savedImages;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải danh sách ảnh đã lưu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void LvSavedImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvSavedImages.SelectedItems.Count == 1 && lvSavedImages.SelectedItem is SavedImage selected)
            {
                imgPreview.Source = selected.Thumbnail;
                _selectedSavedImageId = selected.ID;
                txtImagePath.Text = $"Đã chọn ảnh từ CSDL: {selected.ImageName}";
                _selectedImageData = null;
                _selectedImageFileName = null;
                btnAddImageToLibrary.IsEnabled = false;
            }
            else if (lvSavedImages.SelectedItems.Count > 1)
            {
                _selectedSavedImageId = null;
                txtImagePath.Text = $"Đã chọn {lvSavedImages.SelectedItems.Count} ảnh.";
            }
            else 
            {
                _selectedSavedImageId = null;
                await LoadCurrentSettingsAsync();
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            lvSavedImages.SelectAll();
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            lvSavedImages.UnselectAll();
        }

        private async void BtnDeleteSelectedImages_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = lvSavedImages.SelectedItems.Cast<SavedImage>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Vui lòng chọn ít nhất một ảnh để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Bạn có chắc chắn muốn xóa {selectedItems.Count} ảnh đã chọn không? Hành động này không thể hoàn tác.",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }

            var idsToDelete = selectedItems.Select(item => item.ID).ToList();
            string idList = string.Join(",", idsToDelete);

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand($"DELETE FROM InterfaceImages WHERE ID IN ({idList})", connection);
                    command.CommandTimeout = 120;
                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    MessageBox.Show($"Đã xóa thành công {rowsAffected} ảnh.", "Hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadSavedImagesAsync(); 
                    await LoadCurrentSettingsAsync(); 
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xóa ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    return "application/octet-stream"; 
            }
        }
    }
}