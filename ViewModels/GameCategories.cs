using System.Collections.Generic;
using System.Linq;

namespace Game_Manager.ViewModels
{
    public static class GameCategories
    {
        public const string All = "all";
        public const string Uncategorized = "uncategorized";
        public const string CustomCategoryIconPath = "/img/未分类.png";

        public static IReadOnlyList<(string Key, string Name, string? IconPath)> Definitions { get; } = new List<(string, string, string?)>
        {
            (All, "全部游戏", "/img/全部.png"),
            (Uncategorized, "未分类", "/img/未分类.png"),
            ("action", "动作游戏", "/img/动作游戏.png"),
            ("rpg", "角色扮演", "/img/角色扮演.png"),
            ("strategy", "策略游戏", "/img/策略游戏.png"),
            ("shooter", "射击游戏", "/img/射击游戏.png")
        };

        public static string GetDisplayName(string? categoryKey)
        {
            var key = string.IsNullOrWhiteSpace(categoryKey) ? Uncategorized : categoryKey;
            var match = Definitions.FirstOrDefault(category => category.Key == key);
            return string.IsNullOrEmpty(match.Name) ? "未分类" : match.Name;
        }

        public static IEnumerable<(string Key, string Name, string? IconPath)> GetAssignable()
        {
            return Definitions.Where(category => category.Key != All);
        }
    }
}
