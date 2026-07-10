using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private GameItemViewModel? _draggedGame;
        private FrameworkElement? _draggedElement;
        private Point _dragStartPoint;
        private bool _isDragging;
        private GameItemViewModel? _dragTargetGame;
        private const double CategorySidebarExpandedWidth = 190;
        private const double CategorySidebarCollapsedWidth = 64;
        private const double ArchiveBackgroundExpandedHeight = 350;
        private MainViewModel? _boundViewModel;

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += MainWindow_SourceInitialized;
            StateChanged += MainWindow_StateChanged;
            SizeChanged += MainWindow_SizeChanged;
            ContentBorder.SizeChanged += ContentBorder_SizeChanged;
            CategorySidebar.SizeChanged += CategorySidebar_SizeChanged;
            DataContextChanged += MainWindow_DataContextChanged;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AttachViewModel(DataContext as MainViewModel);
            UpdateArchiveBackgroundPanel(animate: false);
            UpdateCategorySidebarClip();
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachViewModel(e.NewValue as MainViewModel);
            if (IsLoaded)
            {
                UpdateArchiveBackgroundPanel(animate: false);
            }
        }

        private void AttachViewModel(MainViewModel? viewModel)
        {
            if (_boundViewModel != null)
            {
                _boundViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _boundViewModel = viewModel;
            if (_boundViewModel != null)
            {
                _boundViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.IsArchiveView)
                or nameof(MainViewModel.IsCategorySidebarCollapsed))
            {
                UpdateArchiveBackgroundPanel(animate: true);
            }
        }

        private void UpdateArchiveBackgroundPanel(bool animate)
        {
            if (ArchiveBackgroundPanel == null)
            {
                return;
            }

            var shouldExpand = _boundViewModel is { IsArchiveView: true, IsCategorySidebarCollapsed: false };
            var targetHeight = shouldExpand ? ArchiveBackgroundExpandedHeight : 0;
            // 抵消分类栏 Padding=10，使背景图贴齐左右与底部边缘
            var targetMargin = shouldExpand
                ? new Thickness(-10, 6, -10, -10)
                : new Thickness(0);

            if (!animate)
            {
                ArchiveBackgroundPanel.BeginAnimation(HeightProperty, null);
                ArchiveBackgroundPanel.Height = targetHeight;
                ArchiveBackgroundPanel.Margin = targetMargin;
                UpdateCategorySidebarClip();
                return;
            }

            var currentHeight = double.IsNaN(ArchiveBackgroundPanel.Height)
                ? ArchiveBackgroundPanel.ActualHeight
                : ArchiveBackgroundPanel.Height;

            var heightAnimation = new DoubleAnimation
            {
                From = currentHeight,
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var marginAnimation = new ThicknessAnimation
            {
                To = targetMargin,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            ArchiveBackgroundPanel.BeginAnimation(HeightProperty, heightAnimation, HandoffBehavior.SnapshotAndReplace);
            ArchiveBackgroundPanel.BeginAnimation(MarginProperty, marginAnimation, HandoffBehavior.SnapshotAndReplace);

            // 高度动画过程中同步更新分类栏圆角裁剪，避免图片直角溢出
            heightAnimation.CurrentTimeInvalidated += (_, _) => UpdateCategorySidebarClip();
            heightAnimation.Completed += (_, _) => UpdateCategorySidebarClip();
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
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

        private void CategorySidebar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCategorySidebarClip();
        }

        private void UpdateCategorySidebarClip()
        {
            if (CategorySidebar == null || CategorySidebar.ActualWidth <= 0 || CategorySidebar.ActualHeight <= 0)
            {
                return;
            }

            const double radius = 12;
            if (CategorySidebar.Clip is RectangleGeometry existing)
            {
                existing.Rect = new Rect(0, 0, CategorySidebar.ActualWidth, CategorySidebar.ActualHeight);
                existing.RadiusX = radius;
                existing.RadiusY = radius;
                return;
            }

            CategorySidebar.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, CategorySidebar.ActualWidth, CategorySidebar.ActualHeight),
                RadiusX = radius,
                RadiusY = radius
            };
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
                RootGrid.Margin = new Thickness(8);
                ContentBorder.CornerRadius = new CornerRadius(14);
                ContentBorder.Clip = new RectangleGeometry
                {
                    Rect = new Rect(0, 0, ContentBorder.ActualWidth, ContentBorder.ActualHeight),
                    RadiusX = 14,
                    RadiusY = 14
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

        private void ToggleCategorySidebarButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel viewModel)
            {
                return;
            }

            viewModel.IsCategorySidebarCollapsed = !viewModel.IsCategorySidebarCollapsed;
            AnimateCategorySidebarWidth(viewModel.IsCategorySidebarCollapsed
                ? CategorySidebarCollapsedWidth
                : CategorySidebarExpandedWidth);
        }

        private void AnimateCategorySidebarWidth(double targetWidth)
        {
            var startWidth = double.IsNaN(CategorySidebar.Width)
                ? CategorySidebar.ActualWidth
                : CategorySidebar.Width;

            var animation = new DoubleAnimation
            {
                From = startWidth,
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(240),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            CategorySidebar.BeginAnimation(WidthProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MainViewModel viewModel || !viewModel.IsCustomSortMode || !viewModel.IsCustomDragEnabled)
            {
                return;
            }

            if (e.OriginalSource is DependencyObject source && IsInsideButton(source))
            {
                return;
            }

            if (sender is FrameworkElement element && element.DataContext is GameItemViewModel game)
            {
                _draggedGame = game;
                _draggedElement = element;
                _dragTargetGame = null;
                _isDragging = false;
                _dragStartPoint = e.GetPosition(element);
                element.CaptureMouse();
                element.Opacity = 0.9;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                element.RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(1.03, 1.03),
                        new TranslateTransform(0, 0)
                    }
                };
                element.SetValue(Panel.ZIndexProperty, 20);
                e.Handled = true;
            }
        }

        private void Card_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedGame == null || _draggedElement == null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if (sender is not FrameworkElement element || element != _draggedElement)
            {
                return;
            }

            var delta = e.GetPosition(element) - _dragStartPoint;
            if (!_isDragging && Math.Abs(delta.X) < 3 && Math.Abs(delta.Y) < 3)
            {
                return;
            }

            _isDragging = true;
            element.Opacity = 0.75;
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(1.05, 1.05));
            transformGroup.Children.Add(new TranslateTransform(delta.X, delta.Y));
            element.RenderTransform = transformGroup;

            if (DataContext is MainViewModel viewModel)
            {
                var mousePosition = e.GetPosition(this);
                var hitResult = VisualTreeHelper.HitTest(this, mousePosition);
                var targetElement = viewModel.IsListView
                    ? FindGameListRowElement(hitResult?.VisualHit as DependencyObject) ?? FindNearestGameListRowElement(mousePosition)
                    : FindGameCardElement(hitResult?.VisualHit as DependencyObject) ?? FindNearestGameCardElement(mousePosition);
                if (targetElement?.DataContext is GameItemViewModel targetGame && targetGame != _draggedGame)
                {
                    if (_dragTargetGame != targetGame)
                    {
                        ClearDropTargets(viewModel);
                        _dragTargetGame = targetGame;
                        targetGame.IsDropTarget = true;
                    }

                    var insertAfter = viewModel.IsListView
                        ? ShouldInsertAfterListRow(targetElement, mousePosition)
                        : ShouldInsertAfterTarget(targetElement, mousePosition);
                    viewModel.ReorderGames(_draggedGame, targetGame, insertAfter);
                }
                else if (_dragTargetGame != null)
                {
                    ClearDropTargets(viewModel);
                }
            }

            e.Handled = true;
        }

        private void Card_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedGame != null)
            {
                FinishDragging();
                e.Handled = true;
            }
        }

        private void FinishDragging()
        {
            if (_draggedElement != null)
            {
                _draggedElement.Opacity = 1.0;
                _draggedElement.RenderTransform = null;
                _draggedElement.SetValue(Panel.ZIndexProperty, 0);
                _draggedElement.ReleaseMouseCapture();
                _draggedElement = null;
            }

            if (DataContext is MainViewModel viewModel)
            {
                ClearDropTargets(viewModel);
            }

            _draggedGame = null;
            _dragTargetGame = null;
            _isDragging = false;
        }

        private static FrameworkElement? FindGameCardElement(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is Card element && element.DataContext is GameItemViewModel)
                {
                    return element;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return null;
        }

        private FrameworkElement? FindNearestGameCardElement(Point mousePosition)
        {
            FrameworkElement? nearestElement = null;
            var nearestDistance = double.MaxValue;

            foreach (var element in FindVisualChildren<Card>(GameItemsControl))
            {
                if (element.DataContext is not GameItemViewModel game || game == _draggedGame)
                {
                    continue;
                }

                var bounds = GetElementBounds(element);
                if (bounds == Rect.Empty)
                {
                    continue;
                }

                var horizontalReach = bounds.Width * 0.75;
                var verticalReach = bounds.Height * 0.35;
                var interactionBounds = bounds;
                interactionBounds.Inflate(horizontalReach, verticalReach);
                if (!interactionBounds.Contains(mousePosition))
                {
                    continue;
                }

                var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                var distance = Math.Pow(mousePosition.X - center.X, 2) + Math.Pow(mousePosition.Y - center.Y, 2);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestElement = element;
                }
            }

            return nearestElement;
        }

        private static FrameworkElement? FindGameListRowElement(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is FrameworkElement element &&
                    element.DataContext is GameItemViewModel &&
                    Equals(element.Tag, "GameListRow"))
                {
                    return element;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return null;
        }

        private FrameworkElement? FindNearestGameListRowElement(Point mousePosition)
        {
            FrameworkElement? nearestElement = null;
            var nearestDistance = double.MaxValue;

            foreach (var element in FindVisualChildren<FrameworkElement>(GameListItemsControl))
            {
                if (!Equals(element.Tag, "GameListRow") ||
                    element.DataContext is not GameItemViewModel game ||
                    game == _draggedGame)
                {
                    continue;
                }

                var bounds = GetElementBounds(element);
                if (bounds == Rect.Empty)
                {
                    continue;
                }

                var interactionBounds = bounds;
                interactionBounds.Inflate(0, bounds.Height * 0.45);
                if (!interactionBounds.Contains(mousePosition))
                {
                    continue;
                }

                var centerY = bounds.Top + bounds.Height / 2;
                var distance = Math.Abs(mousePosition.Y - centerY);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestElement = element;
                }
            }

            return nearestElement;
        }

        private bool ShouldInsertAfterTarget(FrameworkElement targetElement, Point mousePosition)
        {
            var bounds = GetElementBounds(targetElement);
            if (bounds == Rect.Empty)
            {
                return false;
            }

            return mousePosition.X >= bounds.Left + bounds.Width / 2;
        }

        private bool ShouldInsertAfterListRow(FrameworkElement targetElement, Point mousePosition)
        {
            var bounds = GetElementBounds(targetElement);
            if (bounds == Rect.Empty)
            {
                return false;
            }

            return mousePosition.Y >= bounds.Top + bounds.Height / 2;
        }

        private Rect GetElementBounds(FrameworkElement element)
        {
            try
            {
                var topLeft = element.TransformToAncestor(this).Transform(new Point(0, 0));
                return new Rect(topLeft, new Size(element.ActualWidth, element.ActualHeight));
            }
            catch
            {
                return Rect.Empty;
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var index = 0; index < count; index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (var nestedChild in FindVisualChildren<T>(child))
                {
                    yield return nestedChild;
                }
            }
        }

        private static void ClearDropTargets(MainViewModel viewModel)
        {
            foreach (var game in viewModel.Games)
            {
                game.IsDropTarget = false;
            }
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

        private void MoreMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.ContextMenu == null)
            {
                return;
            }

            button.ContextMenu.DataContext = button.DataContext;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }
}
