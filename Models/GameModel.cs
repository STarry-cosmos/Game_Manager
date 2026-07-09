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
        public DateTime AddedAt { get; set; }
        public int SortOrder { get; set; }
        public string CategoryKey { get; set; } = "uncategorized";
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public int UserRating { get; set; }

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
            AddedAt = record.CreatedAt;
            SortOrder = record.SortOrder;
            CategoryKey = string.IsNullOrWhiteSpace(record.CategoryKey) ? "uncategorized" : record.CategoryKey;
            IsArchived = record.IsArchived;
            ArchivedAt = record.ArchivedAt;
            UserRating = Math.Clamp(record.UserRating, 0, 5);
        }
    }
}
