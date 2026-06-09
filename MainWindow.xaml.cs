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
using MaterialDesignThemes.Wpf;

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
            StateChanged += MainWindow_StateChanged;
            SizeChanged += MainWindow_SizeChanged;
            ContentBorder.SizeChanged += ContentBorder_SizeChanged;
        }

        private void ContentBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ContentBorder.Clip is RectangleGeometry clip)
            {
                clip.Rect = new Rect(0, 0, ContentBorder.ActualWidth, ContentBorder.ActualHeight);
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                ContentBorder.CornerRadius = new CornerRadius(0);
                ContentBorder.Clip = null;
            }
            else
            {
                ContentBorder.CornerRadius = new CornerRadius(12);
                ContentBorder.Clip = new RectangleGeometry
                {
                    Rect = new Rect(0, 0, ContentBorder.ActualWidth, ContentBorder.ActualHeight),
                    RadiusX = 12,
                    RadiusY = 12
                };
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (MaximizeBtn?.Template?.FindName("MaxIcon", MaximizeBtn) is PackIcon icon)
            {
                icon.Kind = WindowState == WindowState.Maximized
                    ? PackIconKind.WindowRestore
                    : PackIconKind.WindowMaximize;
            }
            MaximizeBtn!.ToolTip = WindowState == WindowState.Maximized ? "还原" : "最大化";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }
            DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
