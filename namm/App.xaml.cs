﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace namm
{
    
    
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
          
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            ShowUnhandledException(e.Exception, "Lỗi Giao diện (UI Thread)");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ShowUnhandledException(e.ExceptionObject as Exception, "Lỗi Luồng nền (Background Thread)");
        }

        private void ShowUnhandledException(Exception? ex, string eventName)
        {
            string errorMessage;
            if (ex == null)
            {
                errorMessage = "Đã xảy ra một lỗi nghiêm trọng không xác định.";
            }
            else
            {
                errorMessage = $"Đã xảy ra một lỗi không thể phục hồi và ứng dụng sẽ thoát.\n\n" +
                               $"Nguồn lỗi: {eventName}\n\n" +
                               $"Chi tiết lỗi (vui lòng chụp lại màn hình này):\n\n" +
                               $"{ex}"; 
            }

            MessageBox.Show(errorMessage, "Lỗi nghiêm trọng", MessageBoxButton.OK, MessageBoxImage.Error);

           
            Environment.Exit(1);
        }
    }
}
