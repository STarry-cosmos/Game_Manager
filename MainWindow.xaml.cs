using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Game_Manager.ViewModels;

namespace Game_Manager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnCardClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source && IsInsideButton(source))
            {
                return;
            }

            if (sender is FrameworkElement element && element.DataContext is GameItemViewModel game && DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.SelectGameCommand.Execute(game);
            }
        }

        private static bool IsInsideButton(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is Button)
                {
                    return true;
                }
                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }
    }
}