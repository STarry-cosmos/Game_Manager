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
