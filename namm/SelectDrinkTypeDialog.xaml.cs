﻿﻿﻿using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace namm
{
    public partial class SelectDrinkTypeDialog : Window
    {
        private readonly Dictionary<string, (TextBox textBox, int stock)> _typeControls = new Dictionary<string, (TextBox, int)>();
        public Dictionary<string, int> SelectedQuantities { get; } = new Dictionary<string, int>();

        public SelectDrinkTypeDialog(string drinkName, Dictionary<string, int> availableStock)
        {
            InitializeComponent();
            tbDrinkName.Text = drinkName;

            int rowIndex = 0;
            foreach (var typeStockPair in availableStock)
            {
                string typeName = typeStockPair.Key;
                int stock = typeStockPair.Value;

                gridDrinkTypes.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var typeLabel = new TextBlock
                {
                    Text = $"{typeName}:",
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 5, 10, 5)
                };
                Grid.SetRow(typeLabel, rowIndex);
                Grid.SetColumn(typeLabel, 0);

                var inputPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(inputPanel, rowIndex);
                Grid.SetColumn(inputPanel, 1);

                var textBox = new TextBox
                {
                    Name = "txt" + typeName.Replace(" ", ""),
                    Text = "", // Để trống ô nhập liệu
                    Width = 50,
                    VerticalContentAlignment = VerticalAlignment.Center, 
                    HorizontalContentAlignment = HorizontalAlignment.Center 
                };
                var stockLabel = new TextBlock { Text = $"/ {stock}", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0), Foreground = Brushes.Gray };

                inputPanel.Children.Add(textBox);
                inputPanel.Children.Add(stockLabel);

                gridDrinkTypes.Children.Add(typeLabel);
                gridDrinkTypes.Children.Add(inputPanel);

                _typeControls.Add(typeName, (textBox, stock));

                rowIndex++;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            bool hasError = false;
            bool hasValue = false;

            foreach (var pair in _typeControls)
            {
                string type = pair.Key;
                TextBox textBox = pair.Value.textBox;
                int stock = pair.Value.stock;

                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    continue;
                }

                if (int.TryParse(textBox.Text, out int quantity))
                {
                    if (quantity < 0)
                    {
                        MessageBox.Show($"Số lượng cho kiểu '{type}' không thể là số âm.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        hasError = true;
                        break;
                    }
                    if (quantity > stock)
                    {
                        MessageBox.Show($"Số lượng cho kiểu '{type}' không được vượt quá số lượng tồn kho ({stock}).", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        hasError = true;
                        break;
                    }
                    if (quantity > 0)
                    {
                        SelectedQuantities[type] = quantity;
                        hasValue = true;
                    }
                }
                else
                {
                    MessageBox.Show($"Số lượng cho kiểu '{type}' không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    hasError = true;
                    break;
                }
            }

            if (hasError) return;

            if (!hasValue)
            {
                MessageBox.Show("Vui lòng nhập số lượng lớn hơn 0 cho ít nhất một kiểu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _typeControls.Values.FirstOrDefault().textBox?.Focus();
        }
    }
}