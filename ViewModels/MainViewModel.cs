using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Game_Manager.Data;
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

    public partial class MainViewModel : ObservableObject
    {
        private readonly List<GameItemViewModel> _allGames = new();

        public ObservableCollection<GameItemViewModel> Games { get; } = new();
        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "按名称",
            "按最后运行时间",
            "按时长",
            "自定义排序"
        };

        public IRelayCommand AddGameCommand { get; }
        public IRelayCommand<GameItemViewModel> SelectGameCommand { get; }
        public IRelayCommand ToggleSortDirectionCommand { get; }

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
        public string TotalGameCountDisplay => $"共 {_allGames.Count} 个游戏";
        public string TotalGameTimeDisplay => $"总游戏时长 {FormatDuration(_allGames.Sum(game => game.TotalPlayTimeSeconds))}";

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

        public MainViewModel()
        {
            _dbAdapter = new DatabaseManagerAdapter();
            _monitorService = new ProcessMonitorService(_dbAdapter);

            LoadSortSettings();
            RecoverRunningGameStates();

            AddGameCommand = new RelayCommand(AddGame);
            SelectGameCommand = new RelayCommand<GameItemViewModel>(SelectGame);
            ToggleSortDirectionCommand = new RelayCommand(ToggleSortDirection);
            LoadGames();
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
                var settings = AppSettingsManager.LoadSortSettings();
                _selectedSortIndex = settings.SelectedSortIndex;
                _isAscending = settings.IsAscending;
                OnPropertyChanged(nameof(SelectedSortIndex));
                OnPropertyChanged(nameof(IsAscending));
                OnPropertyChanged(nameof(IsCustomSortMode));
                OnPropertyChanged(nameof(SortDirectionLabel));
                OnPropertyChanged(nameof(IsSortDirectionEnabled));
            }
            catch { }
        }

        private void SaveSortSettings()
        {
            try
            {
                var settings = new SortSettings
                {
                    SelectedSortIndex = SelectedSortIndex,
                    IsAscending = IsAscending
                };
                AppSettingsManager.SaveSortSettings(settings);
            }
            catch { }
        }

        private void LoadGames()
        {
            var items = new List<GameItemViewModel>();
            foreach (var record in _dbAdapter.GetAllGames())
            {
                var model = new GameModel(record);
                var item = new GameItemViewModel(model, _monitorService, _dbAdapter);
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
                SortOrder = _allGames.Count + 1
            };

            _dbAdapter.InsertGame(game);
            LoadGames();
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

            if (IsCustomSortMode && string.IsNullOrWhiteSpace(SearchText))
            {
                PersistCustomOrder();
            }
        }

        private List<GameItemViewModel> FilterGames(IEnumerable<GameItemViewModel> items)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return items.ToList();
            }

            var keyword = SearchText.Trim();
            return items.Where(game =>
                    game.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
                    game.ExecutablePath.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
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
            PersistCustomOrder();
        }

        private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(GameItemViewModel.CurrentSessionDisplay) or nameof(GameItemViewModel.IsRunning))
            {
                OnPropertyChanged(nameof(TotalGameTimeDisplay));
            }
        }

        private void NotifyStatsChanged()
        {
            OnPropertyChanged(nameof(TotalGameCountDisplay));
            OnPropertyChanged(nameof(TotalGameTimeDisplay));
        }

        private static string FormatDuration(long totalSeconds)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
            return duration.TotalHours >= 100
                ? $"{(long)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
                : duration.ToString(@"hh\:mm\:ss");
        }
    }
}
