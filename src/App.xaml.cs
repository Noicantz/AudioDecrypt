using System;
using System.Windows;

namespace AudioDecrypt
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 全局异常处理
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                MessageBox.Show($"发生未处理的异常: {ex.Message}\n\n{ex.StackTrace}", 
                    "程序错误", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            
            // UI线程异常处理
            Current.DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"发生UI线程异常: {args.Exception.Message}\n\n{args.Exception.StackTrace}", 
                    "程序错误", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
} 