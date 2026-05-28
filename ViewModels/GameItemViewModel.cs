using System;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Game_Manager.Models;
using Game_Manager.Services;
using Game_Manager.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;

namespace Game_Manager.ViewModels
{
    public class GameItemViewModel : ObservableObject, IDisposable
    {
        private readonly GameModel _model;
        private readonly ProcessMonitorService _monitor;
        private readonly IDatabaseManager _db;
        private readonly DispatcherTimer _uiTimer;
        private long _currentSessionSeconds;
        private long _totalPlaySeconds;
        private DateTime? _sessionStartUtc;

        public int Id => _model.Id;
        public string Name => _model.Name;
        public string ExecutablePath => _model.ExecutablePath;

        public bool IsRunning
        {
            get => _model.IsRunning;
            private set
            {
                if (_model.IsRunning == value) return;
                _model.IsRunning = value;
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(ButtonText));
            }
        }

        public ICommand DeleteCommand { get; }

        public event Action<GameItemViewModel>? Deleted;
        public string ButtonText => IsRunning ? "游戏中……" : "启动游戏";

        public string CurrentSessionDisplay => TimeSpan.FromSeconds(_currentSessionSeconds).ToString(@"hh\:mm\:ss");

        public ICommand PlayCommand { get; }

        public GameItemViewModel(GameModel model, ProcessMonitorService monitor, IDatabaseManager db)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _db = db ?? throw new ArgumentNullException(nameof(db));

            _totalPlaySeconds = _model.TotalPlayTimeSeconds;
            _currentSessionSeconds = _model.TotalPlayTimeSeconds;
            IsRunning = _model.IsRunning;

            // subscribe to monitor events to stop timer when process exits
            _monitor.GameStopped += OnGameStopped;
            _monitor.GameStarted += OnGameStarted;

            PlayCommand = new RelayCommand(ExecutePlayCommand, CanExecutePlayCommand);
            ChangeCoverCommand = new RelayCommand(ExecuteChangeCover);
            DeleteCommand = new RelayCommand(ExecuteDeleteCommand);

            _uiTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uiTimer.Tick += (s, e) =>
            {
                if (IsRunning && _sessionStartUtc.HasValue)
                {
                    var elapsed = _totalPlaySeconds + _model.CurrentSessionTimeSeconds + (long)(DateTime.UtcNow - _sessionStartUtc.Value).TotalSeconds;
                    _currentSessionSeconds = elapsed;
                    OnPropertyChanged(nameof(CurrentSessionDisplay));
                }
            };

            if (IsRunning)
            {
                // resume UI timer; assume CurrentSessionTimeSeconds contains previously saved seconds
                _sessionStartUtc = DateTime.UtcNow;
                _currentSessionSeconds = _totalPlaySeconds + _model.CurrentSessionTimeSeconds;
                _uiTimer.Start();
            }

            LoadCover();
        }

        public ICommand ChangeCoverCommand { get; }

        private void LoadCover()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_model.CoverImagePath) && System.IO.File.Exists(_model.CoverImagePath))
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.UriSource = new Uri(_model.CoverImagePath);
                    img.EndInit();
                    img.Freeze();
                    CoverImage = img;
                    return;
                }

                // fallback: extract icon from exe
                if (System.IO.File.Exists(ExecutablePath))
                {
                    try
                    {
                        using var ico = System.Drawing.Icon.ExtractAssociatedIcon(ExecutablePath);
                        if (ico != null)
                        {
                            var img = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                                ico.Handle,
                                System.Windows.Int32Rect.Empty,
                                BitmapSizeOptions.FromWidthAndHeight(128, 128));
                            img.Freeze();
                            CoverImage = img;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private ImageSource? _coverImage;
        public ImageSource? CoverImage
        {
            get => _coverImage;
            private set
            {
                _coverImage = value;
                OnPropertyChanged(nameof(CoverImage));
            }
        }

        private void OnGameStarted(int gameId)
        {
            if (gameId != Id) return;
            var disp = System.Windows.Application.Current?.Dispatcher;
            var record = _db.GetGameById(Id);
            if (record != null)
            {
                _model.TotalPlayTimeSeconds = record.TotalPlayTime;
                _model.CurrentSessionTimeSeconds = record.CurrentSessionTime;
            }
            if (disp != null)
            {
                disp.Invoke(() =>
                {
                    IsRunning = true;
                    _totalPlaySeconds = _model.TotalPlayTimeSeconds;
                    _sessionStartUtc = DateTime.UtcNow;
                    _currentSessionSeconds = _totalPlaySeconds + _model.CurrentSessionTimeSeconds;
                    _uiTimer.Start();
                    (PlayCommand as RelayCommand)?.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(CurrentSessionDisplay));
                });
            }
            else
            {
                IsRunning = true;
                _totalPlaySeconds = _model.TotalPlayTimeSeconds;
                _sessionStartUtc = DateTime.UtcNow;
                _currentSessionSeconds = _totalPlaySeconds + _model.CurrentSessionTimeSeconds;
                _uiTimer.Start();
                (PlayCommand as RelayCommand)?.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CurrentSessionDisplay));
            }
        }

        private void OnGameStopped(int gameId)
        {
            if (gameId != Id) return;
            var record = _db.GetGameById(Id);
            if (record != null)
            {
                _model.TotalPlayTimeSeconds = record.TotalPlayTime;
                _model.CurrentSessionTimeSeconds = record.CurrentSessionTime;
            }
            var disp = System.Windows.Application.Current?.Dispatcher;
            if (disp != null)
            {
                disp.Invoke(() =>
                {
                    IsRunning = false;
                    _sessionStartUtc = null;
                    _totalPlaySeconds = _model.TotalPlayTimeSeconds;
                    _currentSessionSeconds = _totalPlaySeconds;
                    try { _uiTimer.Stop(); } catch { }
                    (PlayCommand as RelayCommand)?.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(CurrentSessionDisplay));
                    OnPropertyChanged(nameof(ButtonText));
                });
            }
            else
            {
                IsRunning = false;
                _sessionStartUtc = null;
                _totalPlaySeconds = _model.TotalPlayTimeSeconds;
                _currentSessionSeconds = _totalPlaySeconds;
                try { _uiTimer.Stop(); } catch { }
                (PlayCommand as RelayCommand)?.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CurrentSessionDisplay));
                OnPropertyChanged(nameof(ButtonText));
            }
        }

        private void ExecuteChangeCover()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*",
                Title = "选择封面图片"
            };

            if (dlg.ShowDialog() != true) return;
            var picked = dlg.FileName;
            if (string.IsNullOrWhiteSpace(picked) || !System.IO.File.Exists(picked)) return;

            try
            {
                // save path to DB
                var rec = _db.GetGameById(Id);
                if (rec != null)
                {
                    rec.CoverImagePath = picked;
                    _db.UpdateGame(rec);
                    _model.CoverImagePath = picked;
                    LoadCover();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ChangeCover failed: {ex}");
            }
        }

        private void ExecuteDeleteCommand()
        {
            var result = System.Windows.MessageBox.Show($"确认删除游戏：{Name} ?", "删除确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                // stop monitoring if any
                try { _monitor.StopMonitoring(Id); } catch { }

                var ok = _db.DeleteGame(Id);
                if (ok)
                {
                    Deleted?.Invoke(this);
                    Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete failed: {ex}");
                System.Windows.MessageBox.Show($"删除失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        private bool CanExecutePlayCommand()
        {
            // prevent duplicate starts
            return !IsRunning && !string.IsNullOrWhiteSpace(ExecutablePath);
        }

        private void ExecutePlayCommand()
        {
            if (!CanExecutePlayCommand()) return;

            try
            {
                _monitor.StartGame(ExecutablePath, Id);
                IsRunning = true;
                _sessionStartUtc = DateTime.UtcNow;
                _uiTimer.Start();
                (PlayCommand as RelayCommand)?.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PlayCommand failed: {ex}");
            }
        }

        public void Dispose()
        {
            try
            {
                _uiTimer.Stop();
            }
            catch { }
            // unsubscribe
            try
            {
                _monitor.GameStopped -= OnGameStopped;
                _monitor.GameStarted -= OnGameStarted;
            }
            catch { }
        }
    }
}
