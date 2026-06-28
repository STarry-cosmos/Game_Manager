using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
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
            SourceInitialized += MainWindow_SourceInitialized;
            StateChanged += MainWindow_StateChanged;
            SizeChanged += MainWindow_SizeChanged;
            ContentBorder.SizeChanged += ContentBorder_SizeChanged;
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
            {
                hwndSource.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    var workArea = monitorInfo.rcWork;
                    mmi.ptMaxPosition.x = workArea.left - monitorInfo.rcMonitor.left;
                    mmi.ptMaxPosition.y = workArea.top - monitorInfo.rcMonitor.top;
                    mmi.ptMaxSize.x = workArea.right - workArea.left;
                    mmi.ptMaxSize.y = workArea.bottom - workArea.top;
                }
            }
            Marshal.StructureToPtr(mmi, lParam, true);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

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
                RootGrid.Margin = new Thickness(0);
                ContentBorder.CornerRadius = new CornerRadius(0);
                ContentBorder.Clip = null;
            }
            else
            {
                RootGrid.Margin = new Thickness(10);
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
