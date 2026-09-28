using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Game_Manager.Data
{
    public record CustomCategoryRecord
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

    public record SortSettings
    {
        public int SelectedSortIndex { get; init; }
        public bool IsAscending { get; init; }
        public bool IsGridView { get; init; } = true;
        public List<CustomCategoryRecord> CustomCategories { get; init; } = new();

        // 卡片尺寸。宽度 0 = 用默认值；高度 0 = 自动（由内容撑开）。
        // 旧版 settings.json 没有这些字段，反序列化会得到 0，由 CardSizeOptions 负责回退。
        public int GridCardWidth { get; init; }
        public int GridCardHeight { get; init; }
        public int ArchiveGridCardWidth { get; init; }
        public int ArchiveGridCardHeight { get; init; }
        public int ListRowHeight { get; init; }
        public int ArchiveListRowHeight { get; init; }
    }

    public static class AppSettingsManager
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameLauncher");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

        public static SortSettings LoadSortSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return new SortSettings { SelectedSortIndex = 0, IsAscending = true };
                }

                var text = File.ReadAllText(SettingsFilePath);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return new SortSettings { SelectedSortIndex = 0, IsAscending = true };
                }

                var settings = JsonSerializer.Deserialize<SortSettings>(text);
                return settings ?? new SortSettings { SelectedSortIndex = 0, IsAscending = true };
            }
            catch
            {
                return new SortSettings { SelectedSortIndex = 0, IsAscending = true };
            }
        }

        public static void SaveSortSettings(SortSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Ignore settings persistence failures.
            }
        }
    }
}
