using System.Diagnostics;
using System.IO;
using System.Windows;
using Game_Manager.Data;

namespace Game_Manager.Views
{
    public enum DatabaseInitFailedAction
    {
        Exit,
        Retry,
        OpenFolder
    }

    public partial class DatabaseInitFailedWindow : Window
    {
        public DatabaseInitFailedAction ResultAction { get; private set; } = DatabaseInitFailedAction.Exit;

        public DatabaseInitFailedWindow(string errorMessage)
        {
            InitializeComponent();
            ErrorMessageText.Text = string.IsNullOrWhiteSpace(errorMessage)
                ? "未知错误"
                : errorMessage;
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            ResultAction = DatabaseInitFailedAction.Retry;
            DialogResult = true;
            Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            ResultAction = DatabaseInitFailedAction.Exit;
            DialogResult = false;
            Close();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var directory = DatabaseManager.DatabaseDirectoryPath;
                if (string.IsNullOrWhiteSpace(directory))
                {
                    MessageBox.Show("无法定位数据目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Directory.CreateDirectory(directory);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{directory}\"",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"打开数据目录失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Keep the dialog open so the user can retry after inspecting the folder.
            ResultAction = DatabaseInitFailedAction.OpenFolder;
        }
    }
}
