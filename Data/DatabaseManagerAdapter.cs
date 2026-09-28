using System.Collections.Generic;

namespace Game_Manager.Data
{
    // Adapter that wraps the existing static DatabaseManager to satisfy IDatabaseManager
    public class DatabaseManagerAdapter : IDatabaseManager
    {
        public void InitializeDatabase() => DatabaseManager.InitializeDatabase();

        public int InsertGame(GameRecord game) => DatabaseManager.InsertGame(game);

        public List<GameRecord> GetAllGames() => DatabaseManager.GetAllGames();

        public List<GameRecord> GetRunningGames() => DatabaseManager.GetRunningGames();

        public GameRecord? GetGameById(int id) => DatabaseManager.GetGameById(id);

        public bool UpdateGame(GameRecord game) => DatabaseManager.UpdateGame(game);

        public bool DeleteGame(int id) => DatabaseManager.DeleteGame(id);

        public List<MemorySceneRecord> GetMemoryScenes(int gameId) => DatabaseManager.GetMemoryScenes(gameId);

        public int InsertMemoryScene(MemorySceneRecord scene) => DatabaseManager.InsertMemoryScene(scene);

        public bool UpdateMemoryScenePath(int id, string imagePath) => DatabaseManager.UpdateMemoryScenePath(id, imagePath);

        public bool DeleteMemoryScene(int id) => DatabaseManager.DeleteMemoryScene(id);
    }
}
