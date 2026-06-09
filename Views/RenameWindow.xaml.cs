using System.Windows;
using System.Windows.Input;

namespace Game_Manager.Views
{
    public partial class RenameWindow : Window
    {
        public string NewName { get; private set; } = string.Empty;

        public RenameWindow(string currentName)
        {
            InitializeComponent();
            NameTextBox.Text = currentName;
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            NewName = NameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(NewName))
            {
                MessageBox.Show("游戏名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
