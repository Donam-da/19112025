﻿using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace namm
{
    public class BillItem : INotifyPropertyChanged
    {
        public int DrinkId { get; set; }
        public string DrinkName { get; set; } = string.Empty;
        public string DrinkTypeCode { get; set; } = string.Empty; 
        public string DrinkType { get; set; } = string.Empty; 

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(TotalPrice)); 
                }
            }
        }

        public decimal Price { get; set; }
        public decimal TotalPrice => Quantity * Price;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}