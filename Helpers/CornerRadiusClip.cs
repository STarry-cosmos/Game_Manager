using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Game_Manager.Helpers
{
    /// <summary>
    /// 按 Border.CornerRadius 裁剪子元素（WPF 的 CornerRadius 默认不会裁剪 Content）。
    /// </summary>
    public static class CornerRadiusClip
    {
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(CornerRadiusClip),
                new PropertyMetadata(false, OnEnabledChanged));

        public static void SetEnabled(DependencyObject element, bool value) =>
            element.SetValue(EnabledProperty, value);

        public static bool GetEnabled(DependencyObject element) =>
            (bool)element.GetValue(EnabledProperty);

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                element.Loaded += OnLayoutChanged;
                element.SizeChanged += OnLayoutChanged;
                UpdateClip(element);
            }
            else
            {
                element.Loaded -= OnLayoutChanged;
                element.SizeChanged -= OnLayoutChanged;
                element.Clip = null;
            }
        }

        private static void OnLayoutChanged(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                UpdateClip(element);
            }
        }

        private static void UpdateClip(FrameworkElement element)
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                return;
            }

            var width = element.ActualWidth;
            var height = element.ActualHeight;
            var radius = element is Border border ? border.CornerRadius : new CornerRadius(0);

            // 四角半径相同：用 RectangleGeometry
            if (AreEqual(radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft))
            {
                element.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius.TopLeft, radius.TopLeft);
                return;
            }

            // 非对称圆角：用 PathGeometry
            element.Clip = CreateAsymmetricClip(width, height, radius);
        }

        private static bool AreEqual(double a, double b, double c, double d)
        {
            const double epsilon = 0.01;
            return System.Math.Abs(a - b) < epsilon
                   && System.Math.Abs(b - c) < epsilon
                   && System.Math.Abs(c - d) < epsilon;
        }

        private static Geometry CreateAsymmetricClip(double width, double height, CornerRadius radius)
        {
            var tl = System.Math.Min(radius.TopLeft, System.Math.Min(width, height) / 2);
            var tr = System.Math.Min(radius.TopRight, System.Math.Min(width, height) / 2);
            var br = System.Math.Min(radius.BottomRight, System.Math.Min(width, height) / 2);
            var bl = System.Math.Min(radius.BottomLeft, System.Math.Min(width, height) / 2);

            var figure = new PathFigure { StartPoint = new Point(tl, 0), IsClosed = true };
            figure.Segments.Add(new LineSegment(new Point(width - tr, 0), true));
            if (tr > 0)
            {
                figure.Segments.Add(new ArcSegment(new Point(width, tr), new Size(tr, tr), 0, false, SweepDirection.Clockwise, true));
            }

            figure.Segments.Add(new LineSegment(new Point(width, height - br), true));
            if (br > 0)
            {
                figure.Segments.Add(new ArcSegment(new Point(width - br, height), new Size(br, br), 0, false, SweepDirection.Clockwise, true));
            }

            figure.Segments.Add(new LineSegment(new Point(bl, height), true));
            if (bl > 0)
            {
                figure.Segments.Add(new ArcSegment(new Point(0, height - bl), new Size(bl, bl), 0, false, SweepDirection.Clockwise, true));
            }

            figure.Segments.Add(new LineSegment(new Point(0, tl), true));
            if (tl > 0)
            {
                figure.Segments.Add(new ArcSegment(new Point(tl, 0), new Size(tl, tl), 0, false, SweepDirection.Clockwise, true));
            }

            return new PathGeometry(new[] { figure });
        }
    }
}
