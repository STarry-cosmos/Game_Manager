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
        public string Name
        {
            get => _model.Name;
            private set
            {
                if (_model.Name == value) return;
                _model.Name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        public string ExecutablePath
        {
            get => _model.ExecutablePath;
            private set
            {
                if (_model.ExecutablePath == value) return;
                _model.ExecutablePath = value;
                OnPropertyChanged(nameof(ExecutablePath));
            }
        }

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

        public int SortOrder
        {
            get => _model.SortOrder;
            private set
            {
                if (_model.SortOrder == value) return;
                _model.SortOrder = value;
                OnPropertyChanged(nameof(SortOrder));
            }
        }

        private int _displayIndex;
        public int DisplayIndex
        {
            get => _displayIndex;
            private set
            {
                if (_displayIndex == value) return;
                _displayIndex = value;
                OnPropertyChanged(nameof(DisplayIndex));
            }
        }

        public DateTime? LastPlayedDate => _model.LastPlayedDate;

        public ICommand DeleteCommand { get; }
        public ICommand RenameCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand ChangePathCommand { get; }

        public event Action<GameItemViewModel>? Deleted;
        public string ButtonText => IsRunning ? "游戏中……" : "启动游戏";

        public long TotalPlayTimeSeconds => _totalPlaySeconds + (IsRunning && _sessionStartUtc.HasValue ? (long)(DateTime.UtcNow - _sessionStartUtc.Value).TotalSeconds : 0);

        public string CurrentSessionDisplay => FormatDuration(_currentSessionSeconds);

        public string LastPlayedDisplay
        {
            get
            {
                var date = _model.LastPlayedDate;
                return date.HasValue ? date.Value.ToString("yyyy.MM.dd") : "未运行";
            }
        }

        public ICommand PlayCommand { get; }

        private bool _isDropTarget;
        public bool IsDropTarget
        {
            get => _isDropTarget;
            set
            {
                if (_isDropTarget == value) return;
                _isDropTarget = value;
                OnPropertyChanged(nameof(IsDropTarget));
            }
        }

        public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

        public void SetDisplayIndex(int displayIndex) => DisplayIndex = displayIndex;

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
            RenameCommand = new RelayCommand(ExecuteRenameCommand);
            OpenFolderCommand = new RelayCommand(ExecuteOpenFolder);
            ChangePathCommand = new RelayCommand(ExecuteChangePath);

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
                _model.LastPlayedDate = record.LastPlayed;
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
                    OnPropertyChanged(nameof(LastPlayedDisplay));
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
                OnPropertyChanged(nameof(LastPlayedDisplay));
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

        private void ExecuteRenameCommand()
        {
            try
            {
                var dialog = new Views.RenameWindow(Name)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };

                if (dialog.ShowDialog() == true)
                {
                    var newName = dialog.NewName;
                    if (string.IsNullOrWhiteSpace(newName) || newName == Name) return;

                    var rec = _db.GetGameById(Id);
                    if (rec != null)
                    {
                        rec.Name = newName;
                        _db.UpdateGame(rec);
                        Name = newName;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rename failed: {ex}");
                System.Windows.MessageBox.Show($"重命名失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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

        private void ExecuteOpenFolder()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ExecutablePath) || !System.IO.File.Exists(ExecutablePath))
                {
                    System.Windows.MessageBox.Show("可执行文件不存在", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                var fileInfo = new System.IO.FileInfo(ExecutablePath);
                var folderPath = fileInfo.DirectoryName;

                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    System.Windows.MessageBox.Show("无法获取文件夹路径", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                // Open folder and select the file
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{ExecutablePath}\""
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenFolder failed: {ex}");
                System.Windows.MessageBox.Show($"打开文件夹失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteChangePath()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                    Title = "选择新的游戏可执行文件"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var selectedPath = dialog.FileName;
                if (string.IsNullOrWhiteSpace(selectedPath) || !System.IO.File.Exists(selectedPath))
                {
                    return;
                }

                var rec = _db.GetGameById(Id);
                if (rec == null)
                {
                    return;
                }

                rec.ExecutablePath = selectedPath;
                if (_db.UpdateGame(rec))
                {
                    ExecutablePath = selectedPath;
                    LoadCover();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ChangePath failed: {ex}");
                System.Windows.MessageBox.Show($"修改路径失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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

        private static string FormatDuration(long totalSeconds)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
            return $"{(long)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }
    }
}
