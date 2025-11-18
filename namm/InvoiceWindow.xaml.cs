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

            _ = SetBackgroundImageAsync();

            tbInvoiceId.Text = billId.ToString("D6"); 
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
                    var command = new SqlCommand("SELECT ImageData FROM InterfaceImages WHERE IsActiveForLogin = 1", connection);
                    imageData = await command.ExecuteScalarAsync() as byte[];
                }

                if (imageData != null && imageData.Length > 0)
                {
                    var imageSource = await Task.Run(() => LoadImageFromBytes(imageData));
                    var imageBrush = new ImageBrush(imageSource)
                    {
                        Stretch = Stretch.UniformToFill, Opacity = 0.15 
                    };
                    imageBrush.Freeze(); 
                    InvoiceGrid.Background = imageBrush;
                }
            }
            catch (Exception)
            {
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
            this.DialogResult = true;
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Chức năng in hóa đơn đang được phát triển!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

        }
    }
}