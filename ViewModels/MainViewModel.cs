using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Game_Manager.Data;
using Game_Manager.Models;
using Microsoft.Win32;
using Game_Manager.Services;

namespace Game_Manager.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public ObservableCollection<GameItemViewModel> Games { get; } = new();

        public IRelayCommand AddGameCommand { get; }

        private readonly ProcessMonitorService _monitorService;
        private readonly IDatabaseManager _dbAdapter;

        public MainViewModel()
        {
            _dbAdapter = new DatabaseManagerAdapter();
            _monitorService = new ProcessMonitorService(_dbAdapter);

            AddGameCommand = new RelayCommand(AddGame);
            LoadGames();
        }

        private void LoadGames()
        {
            Games.Clear();
            foreach (var record in _dbAdapter.GetAllGames())
            {
                var model = new GameModel(record);
                var item = new GameItemViewModel(model, _monitorService, _dbAdapter);
                item.Deleted += OnItemDeleted;
                Games.Add(item);
            }
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
                IsRunning = false
            };

            DatabaseManager.InsertGame(game);
            LoadGames();
        }

        private void OnItemDeleted(GameItemViewModel item)
        {
            try
            {
                item.Deleted -= OnItemDeleted;
                Games.Remove(item);
            }
            catch { }
        }
    }
}
