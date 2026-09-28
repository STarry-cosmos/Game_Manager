using System.Collections.Generic;

namespace Game_Manager.Data
{
    public interface IDatabaseManager
    {
        void InitializeDatabase();
        int InsertGame(GameRecord game);
        List<GameRecord> GetAllGames();
        List<GameRecord> GetRunningGames();
        GameRecord? GetGameById(int id);
        bool UpdateGame(GameRecord game);
        bool DeleteGame(int id);
        List<MemorySceneRecord> GetMemoryScenes(int gameId);
        int InsertMemoryScene(MemorySceneRecord scene);
        bool UpdateMemoryScenePath(int id, string imagePath);
        bool DeleteMemoryScene(int id);
    }
}
