using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace Game_Manager.Data
{
    public class GameRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string? CoverImagePath { get; set; }
        public long TotalPlayTime { get; set; }
        public long CurrentSessionTime { get; set; }
        public DateTime? LastPlayed { get; set; }
        public bool IsRunning { get; set; }
        public int? ProcessId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class DatabaseManager
    {
        private static readonly object DbLock = new();
        private static readonly string DbFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameLauncher.db");
        private static readonly string ConnectionString = new SQLiteConnectionStringBuilder
        {
            DataSource = DbFilePath,
            ForeignKeys = true,
            JournalMode = SQLiteJournalModeEnum.Wal,
            SyncMode = SynchronizationModes.Normal
        }.ToString();

        public static void InitializeDatabase()
        {
            string? directory = Path.GetDirectoryName(DbFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var connection = new SQLiteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS Games (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    ExecutablePath TEXT NOT NULL,
    CoverImagePath TEXT,
    TotalPlayTime INTEGER NOT NULL DEFAULT 0,
    CurrentSessionTime INTEGER NOT NULL DEFAULT 0,
    LastPlayed TEXT,
    IsRunning INTEGER NOT NULL DEFAULT 0,
    ProcessId INTEGER,
    CreatedAt TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }

        public static int InsertGame(GameRecord game)
        {
            lock (DbLock)
            {
                using var connection = new SQLiteConnection(ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
INSERT INTO Games (Name, ExecutablePath, CoverImagePath, TotalPlayTime, CurrentSessionTime, LastPlayed, IsRunning, ProcessId, CreatedAt)
VALUES (@Name, @ExecutablePath, @CoverImagePath, @TotalPlayTime, @CurrentSessionTime, @LastPlayed, @IsRunning, @ProcessId, @CreatedAt);";
                command.Parameters.AddWithValue("@Name", game.Name);
                command.Parameters.AddWithValue("@ExecutablePath", game.ExecutablePath);
                command.Parameters.AddWithValue("@CoverImagePath", (object?)game.CoverImagePath ?? DBNull.Value);
                command.Parameters.AddWithValue("@TotalPlayTime", game.TotalPlayTime);
                command.Parameters.AddWithValue("@CurrentSessionTime", game.CurrentSessionTime);
                command.Parameters.AddWithValue("@LastPlayed", game.LastPlayed?.ToString("o", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@IsRunning", game.IsRunning ? 1 : 0);
                command.Parameters.AddWithValue("@ProcessId", (object?)game.ProcessId ?? DBNull.Value);
                command.Parameters.AddWithValue("@CreatedAt", game.CreatedAt.ToString("o", CultureInfo.InvariantCulture));

                command.ExecuteNonQuery();
                return (int)connection.LastInsertRowId;
            }
        }

        public static List<GameRecord> GetAllGames()
        {
            var games = new List<GameRecord>();
            using var connection = new SQLiteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT Id, Name, ExecutablePath, CoverImagePath, TotalPlayTime, CurrentSessionTime, LastPlayed, IsRunning, ProcessId, CreatedAt FROM Games ORDER BY Name;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                games.Add(ReadGame(reader));
            }

            return games;
        }

        public static GameRecord? GetGameById(int id)
        {
            using var connection = new SQLiteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT Id, Name, ExecutablePath, CoverImagePath, TotalPlayTime, CurrentSessionTime, LastPlayed, IsRunning, ProcessId, CreatedAt FROM Games WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadGame(reader) : null;
        }

        public static bool UpdateGame(GameRecord game)
        {
            lock (DbLock)
            {
                using var connection = new SQLiteConnection(ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
UPDATE Games
SET Name = @Name,
    ExecutablePath = @ExecutablePath,
    CoverImagePath = @CoverImagePath,
    TotalPlayTime = @TotalPlayTime,
    CurrentSessionTime = @CurrentSessionTime,
    LastPlayed = @LastPlayed,
    IsRunning = @IsRunning,
    ProcessId = @ProcessId,
    CreatedAt = @CreatedAt
WHERE Id = @Id;";
                command.Parameters.AddWithValue("@Name", game.Name);
                command.Parameters.AddWithValue("@ExecutablePath", game.ExecutablePath);
                command.Parameters.AddWithValue("@CoverImagePath", (object?)game.CoverImagePath ?? DBNull.Value);
                command.Parameters.AddWithValue("@TotalPlayTime", game.TotalPlayTime);
                command.Parameters.AddWithValue("@CurrentSessionTime", game.CurrentSessionTime);
                command.Parameters.AddWithValue("@LastPlayed", game.LastPlayed?.ToString("o", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@IsRunning", game.IsRunning ? 1 : 0);
                command.Parameters.AddWithValue("@ProcessId", (object?)game.ProcessId ?? DBNull.Value);
                command.Parameters.AddWithValue("@CreatedAt", game.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("@Id", game.Id);

                return command.ExecuteNonQuery() > 0;
            }
        }

        public static bool DeleteGame(int id)
        {
            lock (DbLock)
            {
                using var connection = new SQLiteConnection(ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"DELETE FROM Games WHERE Id = @Id;";
                command.Parameters.AddWithValue("@Id", id);

                return command.ExecuteNonQuery() > 0;
            }
        }

        private static GameRecord ReadGame(SQLiteDataReader reader)
        {
            var game = new GameRecord
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                ExecutablePath = reader.GetString(2),
                CoverImagePath = reader.IsDBNull(3) ? null : reader.GetString(3),
                TotalPlayTime = reader.GetInt64(4),
                CurrentSessionTime = reader.GetInt64(5),
                LastPlayed = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6), null, DateTimeStyles.RoundtripKind),
                IsRunning = reader.GetInt32(7) == 1,
                ProcessId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                CreatedAt = DateTime.Parse(reader.GetString(9), null, DateTimeStyles.RoundtripKind)
            };

            return game;
        }
    }
}
