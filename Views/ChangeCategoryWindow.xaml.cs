using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Game_Manager.ViewModels;

namespace Game_Manager.Views
{
    public partial class ChangeCategoryWindow : Window
    {
        public string SelectedCategoryKey { get; private set; } = GameCategories.Uncategorized;

        public ChangeCategoryWindow(IEnumerable<GameCategoryViewModel> categories, string currentCategoryKey)
        {
            InitializeComponent();

            var assignableCategories = categories.ToList();
            CategoryListBox.ItemsSource = assignableCategories;

            var normalizedKey = string.IsNullOrWhiteSpace(currentCategoryKey)
                ? GameCategories.Uncategorized
                : currentCategoryKey;
            CategoryListBox.SelectedItem = assignableCategories.FirstOrDefault(category => category.Key == normalizedKey)
                ?? assignableCategories.FirstOrDefault();
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
            if (CategoryListBox.SelectedItem is not GameCategoryViewModel selectedCategory)
            {
                MessageBox.Show("请选择一个分类", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedCategoryKey = selectedCategory.Key;
            DialogResult = true;
            Close();
        }
    }
}
