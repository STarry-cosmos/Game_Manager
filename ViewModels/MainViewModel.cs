using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
                ApplyCurrentSort();
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
            }
        }

        public bool IsCustomSortMode => SelectedSortIndex == (int)GameSortMode.Custom;
        public bool IsSortDirectionEnabled => !IsCustomSortMode;
        public string SortDirectionLabel => IsAscending ? "升序" : "降序";

        private readonly ProcessMonitorService _monitorService;
        private readonly IDatabaseManager _dbAdapter;

        public MainViewModel()
        {
            _dbAdapter = new DatabaseManagerAdapter();
            _monitorService = new ProcessMonitorService(_dbAdapter);

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

        private void LoadGames()
        {
            var items = new List<GameItemViewModel>();
            foreach (var record in _dbAdapter.GetAllGames())
            {
                var model = new GameModel(record);
                var item = new GameItemViewModel(model, _monitorService, _dbAdapter);
                item.Deleted += OnItemDeleted;
                items.Add(item);
            }

            Games.Clear();
            foreach (var item in items)
            {
                Games.Add(item);
            }

            ApplyCurrentSort();
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
                SortOrder = Games.Count + 1
            };

            _dbAdapter.InsertGame(game);
            LoadGames();
        }

        private void OnItemDeleted(GameItemViewModel item)
        {
            try
            {
                item.Deleted -= OnItemDeleted;
                Games.Remove(item);
                if (IsCustomSortMode)
                {
                    PersistCustomOrder();
                }

                if (SelectedGame == item)
                {
                    SelectedGame = null;
                }
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
                return;
            }

            IsAscending = !IsAscending;
        }

        private void ApplyCurrentSort()
        {
            if (Games.Count == 0)
            {
                return;
            }

            var sorted = SortGames(Games.ToList());
            Games.Clear();
            foreach (var game in sorted)
            {
                Games.Add(game);
            }

            if (IsCustomSortMode)
            {
                PersistCustomOrder();
            }
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
            if (!IsCustomSortMode)
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

        public void ReorderGames(GameItemViewModel source, GameItemViewModel target)
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

            if (sourceIndex < targetIndex)
            {
                targetIndex--;
            }

            Games.Move(sourceIndex, targetIndex);
            PersistCustomOrder();
        }
    }
}
