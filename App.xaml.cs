using System;
using System.Windows;
using Game_Manager.Data;
using Game_Manager.Views;

namespace Game_Manager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            while (true)
            {
                try
                {
                    DatabaseManager.InitializeDatabase();
                    break;
                }
                catch (Exception ex)
                {
                    var dialog = new DatabaseInitFailedWindow(ex.Message);
                    dialog.ShowDialog();

                    if (dialog.ResultAction == DatabaseInitFailedAction.Retry)
                    {
                        continue;
                    }

                    Shutdown();
                    return;
                }
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}
