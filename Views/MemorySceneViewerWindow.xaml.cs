using System.Windows;
using System.Windows.Input;
using Game_Manager.ViewModels;

namespace Game_Manager.Views
{
    public partial class MemorySceneViewerWindow : Window
    {
        public MemorySceneViewerWindow(GameItemViewModel game)
        {
            InitializeComponent();
            DataContext = game;
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Keep focus for keyboard navigation; no-op otherwise.
            Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not GameItemViewModel game)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Left:
                    if (game.PrevMemorySceneCommand.CanExecute(null))
                    {
                        game.PrevMemorySceneCommand.Execute(null);
                    }
                    e.Handled = true;
                    break;
                case Key.Right:
                    if (game.NextMemorySceneCommand.CanExecute(null))
                    {
                        game.NextMemorySceneCommand.Execute(null);
                    }
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Close();
                    e.Handled = true;
                    break;
            }
        }
    }
}
