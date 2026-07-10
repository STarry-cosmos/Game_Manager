using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
        private readonly Func<IReadOnlyList<GameCategoryViewModel>> _getAssignableCategories;
        private readonly Func<string, string> _getCategoryDisplayName;
        private readonly Action? _onCategoryChanged;
        private readonly DispatcherTimer _uiTimer;
        private long _currentSessionSeconds;
        private long _totalPlaySeconds;
        private DateTime? _sessionStartUtc;
        private readonly ObservableCollection<MemorySceneRecord> _memoryScenes = new();
        private int _currentMemorySceneIndex;

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
        public DateTime AddedAt => _model.AddedAt;

        public string CategoryKey
        {
            get => _model.CategoryKey;
            private set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? GameCategories.Uncategorized : value;
                if (_model.CategoryKey == normalized) return;
                _model.CategoryKey = normalized;
                OnPropertyChanged(nameof(CategoryKey));
                OnPropertyChanged(nameof(CategoryDisplayName));
            }
        }

        public string CategoryDisplayName => _getCategoryDisplayName(CategoryKey);

        public ICommand DeleteCommand { get; }
        public ICommand RenameCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand ChangePathCommand { get; }
        public ICommand ChangeCategoryCommand { get; }
        public ICommand ArchiveCommand { get; }
        public ICommand RestoreArchiveCommand { get; }

        public event Action<GameItemViewModel>? Deleted;
        public string ButtonText => IsRunning ? "游戏中……" : "启动游戏";

        public long TotalPlayTimeSeconds => _totalPlaySeconds + (IsRunning && _sessionStartUtc.HasValue ? (long)(DateTime.UtcNow - _sessionStartUtc.Value).TotalSeconds : 0);

        public string CurrentSessionDisplay => FormatDuration(_currentSessionSeconds);

        public string TotalPlayTimeCompactDisplay
        {
            get
            {
                var duration = TimeSpan.FromSeconds(Math.Max(0, TotalPlayTimeSeconds));
                return $"{(long)duration.TotalHours}h {duration.Minutes:D2}m";
            }
        }

        public string LastPlayedDisplay
        {
            get
            {
                var date = _model.LastPlayedDate;
                return date.HasValue ? date.Value.ToLocalTime().ToString("yyyy.MM.dd") : "未运行";
            }
        }

        public string FirstPlayedDisplay => _model.AddedAt.ToLocalTime().ToString("yyyy.MM.dd");

        public string AddedAtDisplay => _model.AddedAt.ToLocalTime().ToString("yyyy.MM.dd");

        public bool IsArchived
        {
            get => _model.IsArchived;
            private set
            {
                if (_model.IsArchived == value) return;
                _model.IsArchived = value;
                OnPropertyChanged(nameof(IsArchived));
                OnPropertyChanged(nameof(ArchiveStatusDisplay));
            }
        }

        public DateTime? ArchivedAt
        {
            get => _model.ArchivedAt;
            private set
            {
                if (_model.ArchivedAt == value) return;
                _model.ArchivedAt = value;
                OnPropertyChanged(nameof(ArchivedAt));
                OnPropertyChanged(nameof(ArchivedAtDisplay));
                OnPropertyChanged(nameof(ArchivedAtListDisplay));
            }
        }

        public string ArchivedAtDisplay => ArchivedAt.HasValue
            ? ArchivedAt.Value.ToLocalTime().ToString("yyyy.MM.dd")
            : "—";

        public string ArchivedAtListDisplay => ArchivedAt.HasValue
            ? $"归档于 {ArchivedAt.Value.ToLocalTime():yyyy.MM.dd}"
            : "归档于 —";

        public int UserRating
        {
            get => _model.UserRating;
            private set
            {
                var clamped = Math.Clamp(value, 0, 5);
                if (_model.UserRating == clamped) return;
                _model.UserRating = clamped;
                OnPropertyChanged(nameof(UserRating));
                OnPropertyChanged(nameof(IsStar1Filled));
                OnPropertyChanged(nameof(IsStar2Filled));
                OnPropertyChanged(nameof(IsStar3Filled));
                OnPropertyChanged(nameof(IsStar4Filled));
                OnPropertyChanged(nameof(IsStar5Filled));
                OnPropertyChanged(nameof(MedalTierText));
                OnPropertyChanged(nameof(MedalImagePath));
                OnPropertyChanged(nameof(IsGoldMedal));
                OnPropertyChanged(nameof(IsSilverMedal));
                OnPropertyChanged(nameof(IsBronzeMedal));
            }
        }

        public bool IsStar1Filled => UserRating >= 1;
        public bool IsStar2Filled => UserRating >= 2;
        public bool IsStar3Filled => UserRating >= 3;
        public bool IsStar4Filled => UserRating >= 4;
        public bool IsStar5Filled => UserRating >= 5;

        /// <summary>5 星金、4 星银、3 星及以下铜。</summary>
        public string MedalTierText => UserRating >= 5 ? "金" : UserRating == 4 ? "银" : "铜";
        public string MedalImagePath => UserRating >= 5 ? "/img/golden.png" : UserRating == 4 ? "/img/silver.png" : "/img/copper.png";
        public bool IsGoldMedal => UserRating >= 5;
        public bool IsSilverMedal => UserRating == 4;
        public bool IsBronzeMedal => UserRating <= 3;

        public string ArchiveStatusDisplay => IsArchived ? "已归档" : "未归档";

        public ICommand PlayCommand { get; }
        public ICommand SetRatingCommand { get; }
        public ICommand PrevMemorySceneCommand { get; }
        public ICommand NextMemorySceneCommand { get; }
        public ICommand OpenMemorySceneViewerCommand { get; }
        public ICommand AddMemorySceneCommand { get; }
        public ICommand DeleteCurrentMemorySceneCommand { get; }

        public IReadOnlyList<MemorySceneRecord> MemoryScenes => _memoryScenes;

        public int CurrentMemorySceneIndex
        {
            get => _currentMemorySceneIndex;
            private set
            {
                if (_currentMemorySceneIndex == value) return;
                _currentMemorySceneIndex = value;
                OnPropertyChanged(nameof(CurrentMemorySceneIndex));
                NotifyMemoryScenePresentationChanged();
            }
        }

        public bool HasMemoryScenes => _memoryScenes.Count > 0;

        public ImageSource? CurrentMemorySceneImage
        {
            get
            {
                if (_memoryScenes.Count == 0 || CurrentMemorySceneIndex < 0 || CurrentMemorySceneIndex >= _memoryScenes.Count)
                {
                    return null;
                }

                return LoadImageSource(_memoryScenes[CurrentMemorySceneIndex].ImagePath);
            }
        }

        public string MemorySceneCounterDisplay => _memoryScenes.Count == 0
            ? "0/0"
            : $"{CurrentMemorySceneIndex + 1}/{_memoryScenes.Count}";

        public bool CanNavigateMemoryScenes => _memoryScenes.Count > 1;

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

        public void SetCategoryKey(string categoryKey) => CategoryKey = categoryKey;

        public GameItemViewModel(
            GameModel model,
            ProcessMonitorService monitor,
            IDatabaseManager db,
            Func<IReadOnlyList<GameCategoryViewModel>> getAssignableCategories,
            Func<string, string> getCategoryDisplayName,
            Action? onCategoryChanged = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _getAssignableCategories = getAssignableCategories ?? throw new ArgumentNullException(nameof(getAssignableCategories));
            _getCategoryDisplayName = getCategoryDisplayName ?? throw new ArgumentNullException(nameof(getCategoryDisplayName));
            _onCategoryChanged = onCategoryChanged;

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
            ChangeCategoryCommand = new RelayCommand(ExecuteChangeCategory);
            ArchiveCommand = new RelayCommand(ExecuteArchive, () => !IsArchived);
            RestoreArchiveCommand = new RelayCommand(ExecuteRestoreArchive, () => IsArchived);
            SetRatingCommand = new RelayCommand<object?>(ExecuteSetRating);
            PrevMemorySceneCommand = new RelayCommand(ExecutePrevMemoryScene, () => CanNavigateMemoryScenes);
            NextMemorySceneCommand = new RelayCommand(ExecuteNextMemoryScene, () => CanNavigateMemoryScenes);
            OpenMemorySceneViewerCommand = new RelayCommand(ExecuteOpenMemorySceneViewer);
            AddMemorySceneCommand = new RelayCommand(ExecuteAddMemoryScene);
            DeleteCurrentMemorySceneCommand = new RelayCommand(ExecuteDeleteCurrentMemoryScene, () => HasMemoryScenes);

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
                    OnPropertyChanged(nameof(TotalPlayTimeCompactDisplay));
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
            LoadMemoryScenes();
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
                    OnPropertyChanged(nameof(TotalPlayTimeCompactDisplay));
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
                OnPropertyChanged(nameof(TotalPlayTimeCompactDisplay));
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

        private void ExecuteChangeCategory()
        {
            try
            {
                var dialog = new Views.ChangeCategoryWindow(_getAssignableCategories(), CategoryKey)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };

                if (dialog.ShowDialog() != true || dialog.SelectedCategoryKey == CategoryKey)
                {
                    return;
                }

                var record = _db.GetGameById(Id);
                if (record == null)
                {
                    return;
                }

                record.CategoryKey = dialog.SelectedCategoryKey;
                if (!_db.UpdateGame(record))
                {
                    return;
                }

                CategoryKey = dialog.SelectedCategoryKey;
                _onCategoryChanged?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ChangeCategory failed: {ex}");
                System.Windows.MessageBox.Show($"修改分类失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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

        private void ExecuteArchive()
        {
            if (IsArchived)
            {
                return;
            }

            if (IsRunning)
            {
                System.Windows.MessageBox.Show("运行中的游戏暂时不能归档。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            SetArchivedState(true);
        }

        private void ExecuteRestoreArchive()
        {
            if (!IsArchived)
            {
                return;
            }

            SetArchivedState(false);
        }

        private void SetArchivedState(bool isArchived)
        {
            try
            {
                var rec = _db.GetGameById(Id);
                if (rec == null)
                {
                    return;
                }

                rec.IsArchived = isArchived;
                rec.ArchivedAt = isArchived ? DateTime.UtcNow : null;
                if (!_db.UpdateGame(rec))
                {
                    return;
                }

                IsArchived = isArchived;
                ArchivedAt = rec.ArchivedAt;
                (ArchiveCommand as RelayCommand)?.NotifyCanExecuteChanged();
                (RestoreArchiveCommand as RelayCommand)?.NotifyCanExecuteChanged();
                _onCategoryChanged?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetArchivedState failed: {ex}");
                System.Windows.MessageBox.Show($"归档操作失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteSetRating(object? parameter)
        {
            try
            {
                if (!TryParseRating(parameter, out var rating))
                {
                    return;
                }

                var clamped = Math.Clamp(rating, 0, 5);
                if (UserRating == clamped)
                {
                    return;
                }

                var rec = _db.GetGameById(Id);
                if (rec == null)
                {
                    return;
                }

                rec.UserRating = clamped;
                if (!_db.UpdateGame(rec))
                {
                    return;
                }

                UserRating = clamped;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetRating failed: {ex}");
                System.Windows.MessageBox.Show($"评分保存失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void LoadMemoryScenes()
        {
            try
            {
                _memoryScenes.Clear();
                foreach (var scene in _db.GetMemoryScenes(Id))
                {
                    _memoryScenes.Add(scene);
                }

                if (_memoryScenes.Count == 0)
                {
                    _currentMemorySceneIndex = 0;
                }
                else if (_currentMemorySceneIndex >= _memoryScenes.Count)
                {
                    _currentMemorySceneIndex = _memoryScenes.Count - 1;
                }

                NotifyMemoryScenePresentationChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadMemoryScenes failed: {ex}");
            }
        }

        private void ExecutePrevMemoryScene()
        {
            if (_memoryScenes.Count <= 1) return;
            CurrentMemorySceneIndex = (CurrentMemorySceneIndex - 1 + _memoryScenes.Count) % _memoryScenes.Count;
        }

        private void ExecuteNextMemoryScene()
        {
            if (_memoryScenes.Count <= 1) return;
            CurrentMemorySceneIndex = (CurrentMemorySceneIndex + 1) % _memoryScenes.Count;
        }

        private void ExecuteOpenMemorySceneViewer()
        {
            try
            {
                var owner = System.Windows.Application.Current?.MainWindow;
                var viewer = new Views.MemorySceneViewerWindow(this)
                {
                    Owner = owner
                };
                viewer.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenMemorySceneViewer failed: {ex}");
                System.Windows.MessageBox.Show($"打开大图模式失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteAddMemoryScene()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*",
                Title = "添加记忆场景图片",
                Multiselect = true
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var added = false;
                foreach (var picked in dlg.FileNames)
                {
                    if (string.IsNullOrWhiteSpace(picked) || !File.Exists(picked))
                    {
                        continue;
                    }

                    var scene = new MemorySceneRecord
                    {
                        GameId = Id,
                        ImagePath = picked,
                        SortOrder = _memoryScenes.Count,
                        CreatedAt = DateTime.UtcNow
                    };
                    scene.Id = _db.InsertMemoryScene(scene);
                    _memoryScenes.Add(scene);
                    added = true;
                }

                if (!added)
                {
                    return;
                }

                CurrentMemorySceneIndex = _memoryScenes.Count - 1;
                NotifyMemoryScenePresentationChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddMemoryScene failed: {ex}");
                System.Windows.MessageBox.Show($"添加图片失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteDeleteCurrentMemoryScene()
        {
            if (_memoryScenes.Count == 0 || CurrentMemorySceneIndex < 0 || CurrentMemorySceneIndex >= _memoryScenes.Count)
            {
                return;
            }

            var scene = _memoryScenes[CurrentMemorySceneIndex];
            var result = System.Windows.MessageBox.Show(
                "确认删除当前记忆场景图片？",
                "删除确认",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                if (!_db.DeleteMemoryScene(scene.Id))
                {
                    return;
                }

                _memoryScenes.RemoveAt(CurrentMemorySceneIndex);
                if (_memoryScenes.Count == 0)
                {
                    _currentMemorySceneIndex = 0;
                }
                else if (_currentMemorySceneIndex >= _memoryScenes.Count)
                {
                    _currentMemorySceneIndex = _memoryScenes.Count - 1;
                }

                NotifyMemoryScenePresentationChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteMemoryScene failed: {ex}");
                System.Windows.MessageBox.Show($"删除图片失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void NotifyMemoryScenePresentationChanged()
        {
            OnPropertyChanged(nameof(CurrentMemorySceneIndex));
            OnPropertyChanged(nameof(CurrentMemorySceneImage));
            OnPropertyChanged(nameof(MemorySceneCounterDisplay));
            OnPropertyChanged(nameof(HasMemoryScenes));
            OnPropertyChanged(nameof(CanNavigateMemoryScenes));
            OnPropertyChanged(nameof(MemoryScenes));
            (PrevMemorySceneCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (NextMemorySceneCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (DeleteCurrentMemorySceneCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }

        private static ImageSource? LoadImageSource(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return null;
                }

                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(path);
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryParseRating(object? parameter, out int rating)
        {
            switch (parameter)
            {
                case int value:
                    rating = value;
                    return true;
                case string text when int.TryParse(text, out var parsed):
                    rating = parsed;
                    return true;
                default:
                    rating = 0;
                    return false;
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
