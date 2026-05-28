using System;
using System.Windows;
using Game_Manager.Data;

namespace Game_Manager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                DatabaseManager.InitializeDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法初始化数据库：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            base.OnStartup(e);
        }
    }
}
