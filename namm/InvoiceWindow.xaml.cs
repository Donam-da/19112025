using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace namm
{
    public partial class InvoiceWindow : Window
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public InvoiceWindow(int tableId, string tableName, string customerName, string customerCode, decimal subTotal, decimal discountPercent, decimal finalTotal, ObservableCollection<BillItem> billItems, int billId)
        {
            InitializeComponent();

            // Tải và thiết lập ảnh nền động
            _ = SetBackgroundImageAsync();

            // Điền thông tin vào hóa đơn
            tbInvoiceId.Text = billId.ToString("D6"); // Định dạng số hóa đơn, ví dụ: 000123
            tbTableName.Text = tableName;
            tbCustomerCode.Text = customerCode;
            tbCustomerName.Text = customerName;
            tbDateTime.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            tbSubTotal.Text = $"{subTotal:N0}";
            tbTotalAmount.Text = $"{finalTotal:N0} VNĐ";

            if (discountPercent > 0)
            {
                decimal discountAmount = subTotal - finalTotal;
                tbDiscountAmount.Text = $"-{discountAmount:N0} ({discountPercent:G29}%)";
                gridDiscount.Visibility = Visibility.Visible;
            }

            // Hiển thị danh sách món
            dgBillItems.ItemsSource = billItems;
        }

        private async Task SetBackgroundImageAsync()
        {
            try
            {
                byte[]? imageData = null;
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    // Lấy ảnh đang được kích hoạt cho màn hình đăng nhập
                    var command = new SqlCommand("SELECT ImageData FROM InterfaceImages WHERE IsActiveForLogin = 1", connection);
                    imageData = await command.ExecuteScalarAsync() as byte[];
                }

                if (imageData != null && imageData.Length > 0)
                {
                    var imageSource = await Task.Run(() => LoadImageFromBytes(imageData));
                    var imageBrush = new ImageBrush(imageSource)
                    {
                        Stretch = Stretch.UniformToFill,
                        Opacity = 0.15 // Giữ độ mờ để làm ảnh chìm
                    };
                    imageBrush.Freeze(); // Tối ưu hóa hiệu suất
                    InvoiceGrid.Background = imageBrush;
                }
                // Nếu không tìm thấy ảnh, Grid sẽ có nền trong suốt mặc định
            }
            catch (Exception)
            {
                // Bỏ qua lỗi, không hiển thị ảnh nền nếu có sự cố
            }
        }

        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            // Đóng cửa sổ và trả về kết quả là true để xác nhận thanh toán
            this.DialogResult = true;
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            // Chức năng in hóa đơn
            // Đây là một chức năng phức tạp, tạm thời chỉ hiển thị thông báo
            MessageBox.Show("Chức năng in hóa đơn đang được phát triển!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

            // Nếu bạn muốn triển khai in thật, bạn có thể sử dụng PrintDialog
            // PrintDialog printDialog = new PrintDialog();
            // if (printDialog.ShowDialog() == true)
            // {
            //     printDialog.PrintVisual(this, "In hóa đơn");
            // }
        }
    }
}