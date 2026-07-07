using System.Windows;
using System.Windows.Input;

namespace Game_Manager.Views
{
    public partial class AddCategoryWindow : Window
    {
        public string CategoryName { get; private set; } = string.Empty;

        public AddCategoryWindow()
        {
            InitializeComponent();
            CategoryNameTextBox.Focus();
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
            CategoryName = CategoryNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(CategoryName))
            {
                MessageBox.Show("分类名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                CategoryNameTextBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
