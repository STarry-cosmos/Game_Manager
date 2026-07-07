namespace Game_Manager.Models
{
    public class GameModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public long TotalPlayTimeSeconds { get; set; }
        public long CurrentSessionTimeSeconds { get; set; }
        public bool IsRunning { get; set; }
        public string? CoverImagePath { get; set; }
        public DateTime? LastPlayedDate { get; set; }
        public int SortOrder { get; set; }
        public string CategoryKey { get; set; } = "uncategorized";

        public GameModel()
        {
        }

        public GameModel(Data.GameRecord record)
        {
            Id = record.Id;
            Name = record.Name;
            ExecutablePath = record.ExecutablePath;
            TotalPlayTimeSeconds = record.TotalPlayTime;
            CurrentSessionTimeSeconds = record.CurrentSessionTime;
            IsRunning = record.IsRunning;
            CoverImagePath = record.CoverImagePath;
            LastPlayedDate = record.LastPlayed;
            SortOrder = record.SortOrder;
            CategoryKey = string.IsNullOrWhiteSpace(record.CategoryKey) ? "uncategorized" : record.CategoryKey;
        }
    }
}
