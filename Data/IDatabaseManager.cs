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
    }
}
