using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Game_Manager.Data;
using Game_Manager.Helpers;
using Game_Manager.Models;
using Microsoft.Win32;
using Game_Manager.Services;

namespace Game_Manager.ViewModels
{
    public enum GameSortMode
    {
        Name = 0,
        LastPlayed = 1,
        TotalPlayTime = 2,
        Custom = 3
    }

    public class GameCategoryViewModel : ObservableObject
    {
        public string Key { get; }
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public string? IconPath { get; }
        public bool IsCustom => Key.StartsWith("custom_", StringComparison.Ordinal);

        private string _countDisplay = "0";
        public string CountDisplay
        {
            get => _countDisplay;
            set => SetProperty(ref _countDisplay, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public GameCategoryViewModel(string key, string name, string? iconPath = null)
        {
            Key = key;
            _name = name;
            IconPath = iconPath;
        }
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly List<GameItemViewModel> _allGames = new();

        public ObservableCollection<GameItemViewModel> Games { get; } = new();
        public ObservableCollection<GameCategoryViewModel> Categories { get; } = new();
        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "按名称",
            "按最后运行时间",
            "按时长",
            "自定义排序"
        };

        public IRelayCommand AddGameCommand { get; }
        public IRelayCommand AddCategoryCommand { get; }
        public IRelayCommand OpenSettingsCommand { get; }
        public IRelayCommand OpenDatabaseFolderCommand { get; }
        public IRelayCommand OpenProjectUrlCommand { get; }
        public IRelayCommand<GameCategoryViewModel> RenameCategoryCommand { get; }
        public IRelayCommand<GameCategoryViewModel> DeleteCategoryCommand { get; }
        public IRelayCommand<GameCategoryViewModel> SelectCategoryCommand { get; }
        public IRelayCommand<GameItemViewModel> SelectGameCommand { get; }
        public IRelayCommand ClearSelectionCommand { get; }
        public IRelayCommand ToggleSortDirectionCommand { get; }
        public IRelayCommand ShowGridViewCommand { get; }
        public IRelayCommand ShowListViewCommand { get; }

        public string DatabaseDirectoryDisplay => DatabaseManager.DatabaseDirectoryPath;

        /// <summary>设置窗口「关于」区展示的版本号，例如 "v0.20.3"。</summary>
        public string AppVersionDisplay => AppInfo.VersionDisplay;

        /// <summary>四段式程序集版本，作为版本号的补充说明（鼠标悬停可见）。</summary>
        public string AppBuildDisplay => $"程序集版本 {AppInfo.AssemblyVersion}";

        public string AppLicenseDisplay => AppInfo.LicenseName;

        public string ProjectUrl => AppInfo.ProjectUrl;

        private GameCategoryViewModel? _selectedCategory;
        public GameCategoryViewModel? SelectedCategory
        {
            get => _selectedCategory;
            private set
            {
                if (_selectedCategory == value) return;

                if (_selectedCategory != null)
                {
                    _selectedCategory.IsSelected = false;
                }

                _selectedCategory = value;

                if (_selectedCategory != null)
                {
                    _selectedCategory.IsSelected = true;
                }

                OnPropertyChanged(nameof(SelectedCategory));
                OnPropertyChanged(nameof(CurrentCategoryName));
                OnPropertyChanged(nameof(CurrentCategoryGameCountDisplay));
                OnPropertyChanged(nameof(IsArchiveView));
                OnPropertyChanged(nameof(TotalGameCountDisplay));
                OnPropertyChanged(nameof(TotalGameTimeDisplay));
                OnPropertyChanged(nameof(ArchiveGameCount));
                OnPropertyChanged(nameof(ArchiveTotalHoursDisplay));
            }
        }

        private GameItemViewModel? _selectedGame;
        public GameItemViewModel? SelectedGame
        {
            get => _selectedGame;
            private set
            {
                if (_selectedGame == value) return;
                _selectedGame = value;
                OnPropertyChanged(nameof(SelectedGame));
                OnPropertyChanged(nameof(IsGameSelected));
            }
        }

        public bool IsGameSelected => SelectedGame != null;

        private int _selectedSortIndex;
        public int SelectedSortIndex
        {
            get => _selectedSortIndex;
            set
            {
                if (_selectedSortIndex == value) return;
                _selectedSortIndex = value;
                OnPropertyChanged(nameof(SelectedSortIndex));
                OnPropertyChanged(nameof(IsCustomSortMode));
                OnPropertyChanged(nameof(IsSortDirectionEnabled));
                OnPropertyChanged(nameof(SortDirectionLabel));
                if (!IsCustomSortMode)
                {
                    IsCustomDragEnabled = false;
                }
                ApplyCurrentSort();
                SaveSortSettings();
            }
        }

        private bool _isAscending = true;
        public bool IsAscending
        {
            get => _isAscending;
            set
            {
                if (_isAscending == value) return;
                _isAscending = value;
                OnPropertyChanged(nameof(IsAscending));
                OnPropertyChanged(nameof(IsSortDirectionEnabled));
                OnPropertyChanged(nameof(SortDirectionLabel));
                ApplyCurrentSort();
                SaveSortSettings();
            }
        }

        private bool _isCustomDragEnabled;
        public bool IsCustomDragEnabled
        {
            get => _isCustomDragEnabled;
            set
            {
                if (_isCustomDragEnabled == value) return;
                _isCustomDragEnabled = value;
                OnPropertyChanged(nameof(IsCustomDragEnabled));
                OnPropertyChanged(nameof(IsSortDirectionEnabled));
                OnPropertyChanged(nameof(SortDirectionLabel));
            }
        }

        public bool IsCustomSortMode => SelectedSortIndex == (int)GameSortMode.Custom;
        public bool IsSortDirectionEnabled => true;
        public string SortDirectionLabel => IsCustomSortMode ? (IsCustomDragEnabled ? "手动中" : "手动") : (IsAscending ? "升序" : "降序");
        public string TotalGameCountDisplay => IsArchiveView
            ? $"共 {_allGames.Count(game => game.IsArchived)} 个已归档游戏"
            : $"共 {_allGames.Count(game => !game.IsArchived)} 个游戏";
        public string TotalGameTimeDisplay => IsArchiveView
            ? $"归档游戏时长 {FormatDuration(_allGames.Where(game => game.IsArchived).Sum(game => game.TotalPlayTimeSeconds))}"
            : $"总游戏时长 {FormatDuration(_allGames.Where(game => !game.IsArchived).Sum(game => game.TotalPlayTimeSeconds))}";
        public string CurrentCategoryName => SelectedCategory?.Name ?? "全部游戏";
        public string CurrentCategoryGameCountDisplay => $"({Games.Count})";
        public bool IsArchiveView => SelectedCategory?.Key == GameCategories.Archived;
        public int ArchiveGameCount => _allGames.Count(game => game.IsArchived);
        public string ArchiveTotalHoursDisplay
        {
            get
            {
                var totalSeconds = _allGames.Where(game => game.IsArchived).Sum(game => game.TotalPlayTimeSeconds);
                var hours = (long)TimeSpan.FromSeconds(Math.Max(0, totalSeconds)).TotalHours;
                return hours.ToString("N0");
            }
        }

        private bool _isCategorySidebarCollapsed;
        public bool IsCategorySidebarCollapsed
        {
            get => _isCategorySidebarCollapsed;
            set => SetProperty(ref _isCategorySidebarCollapsed, value);
        }

        private bool _isGridView = true;
        public bool IsGridView
        {
            get => _isGridView;
            private set
            {
                if (_isGridView == value) return;
                _isGridView = value;
                OnPropertyChanged(nameof(IsGridView));
                OnPropertyChanged(nameof(IsListView));
            }
        }

        public bool IsListView => !IsGridView;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                ApplyCurrentSort();
            }
        }

        private readonly ProcessMonitorService _monitorService;
        private readonly IDatabaseManager _dbAdapter;
        private SortSettings _settings = new();

        public MainViewModel()
        {
            _dbAdapter = new DatabaseManagerAdapter();
            _monitorService = new ProcessMonitorService(_dbAdapter);

            LoadSortSettings();
            RecoverRunningGameStates();

            AddGameCommand = new RelayCommand(AddGame);
            AddCategoryCommand = new RelayCommand(AddCategory);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenDatabaseFolderCommand = new RelayCommand(OpenDatabaseFolder);
            OpenProjectUrlCommand = new RelayCommand(OpenProjectUrl);
            RenameCategoryCommand = new RelayCommand<GameCategoryViewModel>(RenameCategory);
            DeleteCategoryCommand = new RelayCommand<GameCategoryViewModel>(DeleteCategory);
            SelectCategoryCommand = new RelayCommand<GameCategoryViewModel>(SelectCategory);
            SelectGameCommand = new RelayCommand<GameItemViewModel>(SelectGame);
            ClearSelectionCommand = new RelayCommand(ClearSelection);
            ToggleSortDirectionCommand = new RelayCommand(ToggleSortDirection);
            ShowGridViewCommand = new RelayCommand(() => SetViewMode(true));
            ShowListViewCommand = new RelayCommand(() => SetViewMode(false));
            InitializeCategories();
            LoadGames();
        }

        private void InitializeCategories()
        {
            var selectedKey = SelectedCategory?.Key;
            Categories.Clear();

            foreach (var category in GameCategories.Definitions.Where(item => item.Key != GameCategories.Archived))
            {
                Categories.Add(new GameCategoryViewModel(category.Key, category.Name, category.IconPath));
            }

            foreach (var customCategory in _settings.CustomCategories)
            {
                if (string.IsNullOrWhiteSpace(customCategory.Key) || string.IsNullOrWhiteSpace(customCategory.Name))
                {
                    continue;
                }

                if (Categories.Any(category => category.Key == customCategory.Key))
                {
                    continue;
                }

                Categories.Add(new GameCategoryViewModel(
                    customCategory.Key,
                    customCategory.Name,
                    GameCategories.CustomCategoryIconPath));
            }

            var archivedCategory = GameCategories.Definitions.First(item => item.Key == GameCategories.Archived);
            Categories.Add(new GameCategoryViewModel(
                archivedCategory.Key,
                archivedCategory.Name,
                archivedCategory.IconPath));

            SelectedCategory = Categories.FirstOrDefault(category => category.Key == selectedKey) ?? Categories[0];
            NotifyCategoryDisplayChanged();
        }

        private int GetArchivedCategoryIndex()
        {
            for (var index = 0; index < Categories.Count; index++)
            {
                if (Categories[index].Key == GameCategories.Archived)
                {
                    return index;
                }
            }

            return Categories.Count;
        }

        private string GetCategoryDisplayName(string? categoryKey)
        {
            var key = string.IsNullOrWhiteSpace(categoryKey) ? GameCategories.Uncategorized : categoryKey;
            return Categories.FirstOrDefault(category => category.Key == key)?.Name
                ?? GameCategories.GetDisplayName(key);
        }

        private IReadOnlyList<GameCategoryViewModel> GetAssignableCategories()
        {
            return Categories
                .Where(category => category.Key != GameCategories.All && category.Key != GameCategories.Archived)
                .ToList();
        }

        private void OnGameCategoryChanged()
        {
            ApplyCurrentSort();
            NotifyStatsChanged();
        }

        private void RecoverRunningGameStates()
        {
            try
            {
                var runningGames = _dbAdapter.GetRunningGames();
                _monitorService.RecoverRunningGames(runningGames);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecoverRunningGameStates failed: {ex}");
            }
        }

        private void LoadSortSettings()
        {
            try
            {
                _settings = AppSettingsManager.LoadSortSettings();
                _selectedSortIndex = _settings.SelectedSortIndex;
                _isAscending = _settings.IsAscending;
                _isGridView = _settings.IsGridView;
                OnPropertyChanged(nameof(SelectedSortIndex));
                OnPropertyChanged(nameof(IsAscending));
                OnPropertyChanged(nameof(IsCustomSortMode));
                OnPropertyChanged(nameof(SortDirectionLabel));
                OnPropertyChanged(nameof(IsSortDirectionEnabled));
                OnPropertyChanged(nameof(IsGridView));
                OnPropertyChanged(nameof(IsListView));
            }
            catch { }
        }

        private void SaveSortSettings()
        {
            try
            {
                _settings = _settings with
                {
                    SelectedSortIndex = SelectedSortIndex,
                    IsAscending = IsAscending,
                    IsGridView = IsGridView
                };
                AppSettingsManager.SaveSortSettings(_settings);
            }
            catch { }
        }

        private void SaveCustomCategories()
        {
            try
            {
                AppSettingsManager.SaveSortSettings(_settings);
            }
            catch { }
        }

        private void SetViewMode(bool isGridView)
        {
            IsGridView = isGridView;
            SaveSortSettings();
        }

        private void LoadGames()
        {
            var items = new List<GameItemViewModel>();
            foreach (var record in _dbAdapter.GetAllGames())
            {
                var model = new GameModel(record);
                var item = new GameItemViewModel(
                    model,
                    _monitorService,
                    _dbAdapter,
                    GetAssignableCategories,
                    GetCategoryDisplayName,
                    OnGameCategoryChanged);
                item.Deleted += OnItemDeleted;
                item.PropertyChanged += OnGamePropertyChanged;
                items.Add(item);
            }

            foreach (var game in _allGames)
            {
                game.Deleted -= OnItemDeleted;
                game.PropertyChanged -= OnGamePropertyChanged;
            }

            _allGames.Clear();
            _allGames.AddRange(items);
            Games.Clear();

            ApplyCurrentSort();
            NotifyStatsChanged();
        }

        private void AddGame()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "选择游戏可执行文件"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var fullPath = dialog.FileName;
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                return;
            }

            var game = new GameRecord
            {
                Name = Path.GetFileNameWithoutExtension(fullPath),
                ExecutablePath = fullPath,
                CreatedAt = DateTime.UtcNow,
                TotalPlayTime = 0,
                CurrentSessionTime = 0,
                IsRunning = false,
                SortOrder = _allGames.Count + 1,
                CategoryKey = GameCategories.Uncategorized,
                IsArchived = false
            };

            _dbAdapter.InsertGame(game);
            LoadGames();
        }

        private void AddCategory()
        {
            var dialog = new Views.AddCategoryWindow
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var categoryName = dialog.CategoryName;
            if (Categories.Any(category => category.Name.Equals(categoryName, StringComparison.CurrentCultureIgnoreCase)))
            {
                System.Windows.MessageBox.Show("已存在同名分类", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var customCategory = new CustomCategoryRecord
            {
                Key = $"custom_{Guid.NewGuid():N}",
                Name = categoryName
            };

            _settings = _settings with
            {
                CustomCategories = _settings.CustomCategories
                    .Append(customCategory)
                    .ToList()
            };
            SaveCustomCategories();

            Categories.Insert(GetArchivedCategoryIndex(), new GameCategoryViewModel(
                customCategory.Key,
                customCategory.Name,
                GameCategories.CustomCategoryIconPath));
            NotifyCategoryDisplayChanged();
        }

        private void OpenSettings()
        {
            var dialog = new Views.SettingsWindow
            {
                Owner = System.Windows.Application.Current?.MainWindow,
                DataContext = this
            };
            dialog.ShowDialog();
        }

        private void OpenDatabaseFolder()
        {
            try
            {
                DatabaseManager.InitializeDatabase();

                var databasePath = DatabaseManager.DatabaseFilePath;
                var directoryPath = DatabaseManager.DatabaseDirectoryPath;
                if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                {
                    System.Windows.MessageBox.Show("无法找到数据库所在目录。", "错误",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                var arguments = File.Exists(databasePath)
                    ? $"/select,\"{databasePath}\""
                    : $"\"{directoryPath}\"";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = arguments,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenDatabaseFolder failed: {ex}");
                System.Windows.MessageBox.Show($"打开数据库目录失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void OpenProjectUrl()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = AppInfo.ProjectUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenProjectUrl failed: {ex}");
                System.Windows.MessageBox.Show($"打开项目主页失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void RenameCategory(GameCategoryViewModel? category)
        {
            if (category == null || !category.IsCustom)
            {
                return;
            }

            var dialog = new Views.RenameCategoryWindow(category.Name)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var newName = dialog.NewName.Trim();
            if (string.IsNullOrWhiteSpace(newName) || newName.Equals(category.Name, StringComparison.CurrentCulture))
            {
                return;
            }

            if (Categories.Any(item => !ReferenceEquals(item, category) &&
                                       item.Name.Equals(newName, StringComparison.CurrentCultureIgnoreCase)))
            {
                System.Windows.MessageBox.Show("已存在同名分类", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            category.Name = newName;
            _settings = _settings with
            {
                CustomCategories = _settings.CustomCategories
                    .Select(item => item.Key == category.Key ? item with { Name = newName } : item)
                    .ToList()
            };
            SaveCustomCategories();
            OnPropertyChanged(nameof(CurrentCategoryName));
        }

        private void DeleteCategory(GameCategoryViewModel? category)
        {
            if (category == null || !category.IsCustom)
            {
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"确认删除分类：{category.Name}？\n该分类下的游戏将自动归为未分类。",
                "删除分类",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            foreach (var game in _allGames.Where(item => item.CategoryKey == category.Key))
            {
                var record = _dbAdapter.GetGameById(game.Id);
                if (record == null)
                {
                    continue;
                }

                record.CategoryKey = GameCategories.Uncategorized;
                if (_dbAdapter.UpdateGame(record))
                {
                    game.SetCategoryKey(GameCategories.Uncategorized);
                }
            }

            _settings = _settings with
            {
                CustomCategories = _settings.CustomCategories
                    .Where(item => item.Key != category.Key)
                    .ToList()
            };
            SaveCustomCategories();

            if (ReferenceEquals(SelectedCategory, category))
            {
                SelectedCategory = Categories.FirstOrDefault(item => item.Key == GameCategories.Uncategorized) ?? Categories.FirstOrDefault();
            }

            Categories.Remove(category);
            ApplyCurrentSort();
            NotifyStatsChanged();
        }

        private void OnItemDeleted(GameItemViewModel item)
        {
            try
            {
                item.Deleted -= OnItemDeleted;
                item.PropertyChanged -= OnGamePropertyChanged;
                _allGames.Remove(item);
                Games.Remove(item);
                if (IsCustomSortMode)
                {
                    PersistCustomOrder();
                }

                if (SelectedGame == item)
                {
                    SelectedGame = null;
                }

                NotifyStatsChanged();
            }
            catch { }
        }

        private void SelectGame(GameItemViewModel? game)
        {
            SelectedGame = game;
        }

        /// <summary>关闭右侧详情栏：取消选中后列表会自动补上这块宽度。</summary>
        private void ClearSelection()
        {
            SelectedGame = null;
        }

        private void SelectCategory(GameCategoryViewModel? category)
        {
            if (category == null || SelectedCategory == category)
            {
                return;
            }

            SelectedCategory = category;
            ApplyCurrentSort();
        }

        private void ToggleSortDirection()
        {
            if (IsCustomSortMode)
            {
                IsCustomDragEnabled = !IsCustomDragEnabled;
                return;
            }

            IsAscending = !IsAscending;
        }

        private void ApplyCurrentSort()
        {
            var filtered = FilterGames(_allGames);
            var sorted = SortGames(filtered);
            Games.Clear();
            foreach (var game in sorted)
            {
                Games.Add(game);
            }
            UpdateDisplayIndexes();
            NotifyCategoryDisplayChanged();

            if (SelectedGame != null && !Games.Contains(SelectedGame))
            {
                SelectedGame = null;
            }

            if (IsCustomSortMode && string.IsNullOrWhiteSpace(SearchText))
            {
                PersistCustomOrder();
            }
        }

        private List<GameItemViewModel> FilterGames(IEnumerable<GameItemViewModel> items)
        {
            var categoryItems = FilterGamesByCategory(items);
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return categoryItems.ToList();
            }

            var keyword = SearchText.Trim();
            return categoryItems.Where(game =>
                    game.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
                    game.ExecutablePath.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
        }

        private IEnumerable<GameItemViewModel> FilterGamesByCategory(IEnumerable<GameItemViewModel> items)
        {
            if (IsArchiveView)
            {
                return items.Where(game => game.IsArchived);
            }

            var activeItems = items.Where(game => !game.IsArchived);
            return SelectedCategory?.Key switch
            {
                GameCategories.All => activeItems,
                GameCategories.Uncategorized => activeItems.Where(game => IsUncategorized(game.CategoryKey)),
                _ => activeItems.Where(game => game.CategoryKey == SelectedCategory!.Key)
            };
        }

        private static bool IsUncategorized(string? categoryKey)
        {
            return string.IsNullOrWhiteSpace(categoryKey) || categoryKey == GameCategories.Uncategorized;
        }

        private List<GameItemViewModel> SortGames(IReadOnlyList<GameItemViewModel> items)
        {
            switch (SelectedSortIndex)
            {
                case (int)GameSortMode.LastPlayed:
                    return ApplyLastPlayedSort(items);
                case (int)GameSortMode.TotalPlayTime:
                    return ApplyPlayTimeSort(items);
                case (int)GameSortMode.Custom:
                    return items.OrderBy(game => game.SortOrder)
                        .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(game => game.Id)
                        .ToList();
                default:
                    return ApplyNameSort(items);
            }
        }

        private List<GameItemViewModel> ApplyNameSort(IReadOnlyList<GameItemViewModel> items)
        {
            return IsAscending
                ? items.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(game => game.Id)
                    .ToList()
                : items.OrderByDescending(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(game => game.Id)
                    .ToList();
        }

        private List<GameItemViewModel> ApplyLastPlayedSort(IReadOnlyList<GameItemViewModel> items)
        {
            return IsAscending
                ? items.OrderBy(game => game.LastPlayedDate.HasValue ? 0 : 1)
                    .ThenBy(game => game.LastPlayedDate ?? DateTime.MaxValue)
                    .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(game => game.Id)
                    .ToList()
                : items.OrderBy(game => game.LastPlayedDate.HasValue ? 0 : 1)
                    .ThenByDescending(game => game.LastPlayedDate ?? DateTime.MaxValue)
                    .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(game => game.Id)
                    .ToList();
        }

        private List<GameItemViewModel> ApplyPlayTimeSort(IReadOnlyList<GameItemViewModel> items)
        {
            return IsAscending
                ? items.OrderBy(game => game.TotalPlayTimeSeconds)
                    .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(game => game.Id)
                    .ToList()
                : items.OrderByDescending(game => game.TotalPlayTimeSeconds)
                    .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(game => game.Id)
                    .ToList();
        }

        private void PersistCustomOrder()
        {
            if (!IsCustomSortMode || !string.IsNullOrWhiteSpace(SearchText))
            {
                return;
            }

            for (var index = 0; index < Games.Count; index++)
            {
                var game = Games[index];
                var sortOrder = index + 1;
                if (game.SortOrder == sortOrder)
                {
                    continue;
                }

                game.SetSortOrder(sortOrder);
                var record = _dbAdapter.GetGameById(game.Id);
                if (record != null)
                {
                    record.SortOrder = sortOrder;
                    _dbAdapter.UpdateGame(record);
                }
            }
        }

        public void ReorderGames(GameItemViewModel source, GameItemViewModel target, bool insertAfterTarget)
        {
            if (!IsCustomSortMode || source == target)
            {
                return;
            }

            var sourceIndex = Games.IndexOf(source);
            var targetIndex = Games.IndexOf(target);
            if (sourceIndex < 0 || targetIndex < 0)
            {
                return;
            }

            var insertIndex = insertAfterTarget ? targetIndex + 1 : targetIndex;
            if (sourceIndex < insertIndex)
            {
                insertIndex--;
            }

            if (sourceIndex == insertIndex)
            {
                return;
            }

            Games.Move(sourceIndex, insertIndex);
            UpdateDisplayIndexes();
            PersistCustomOrder();
        }

        private void UpdateDisplayIndexes()
        {
            for (var index = 0; index < Games.Count; index++)
            {
                Games[index].SetDisplayIndex(index + 1);
            }
        }

        private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(GameItemViewModel.CurrentSessionDisplay)
                or nameof(GameItemViewModel.IsRunning)
                or nameof(GameItemViewModel.TotalPlayTimeCompactDisplay))
            {
                OnPropertyChanged(nameof(TotalGameTimeDisplay));
                OnPropertyChanged(nameof(ArchiveTotalHoursDisplay));
            }
        }

        private void NotifyStatsChanged()
        {
            OnPropertyChanged(nameof(TotalGameCountDisplay));
            OnPropertyChanged(nameof(TotalGameTimeDisplay));
            OnPropertyChanged(nameof(ArchiveGameCount));
            OnPropertyChanged(nameof(ArchiveTotalHoursDisplay));
            NotifyCategoryDisplayChanged();
        }

        private void NotifyCategoryDisplayChanged()
        {
            foreach (var category in Categories)
            {
                category.CountDisplay = category.Key switch
                {
                    GameCategories.All => _allGames.Count(game => !game.IsArchived).ToString(),
                    GameCategories.Archived => _allGames.Count(game => game.IsArchived).ToString(),
                    GameCategories.Uncategorized => _allGames.Count(game => !game.IsArchived && IsUncategorized(game.CategoryKey)).ToString(),
                    _ => _allGames.Count(game => !game.IsArchived && game.CategoryKey == category.Key).ToString()
                };
            }

            OnPropertyChanged(nameof(CurrentCategoryGameCountDisplay));
        }

        private static string FormatDuration(long totalSeconds)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
            return $"{(long)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }
    }
}
