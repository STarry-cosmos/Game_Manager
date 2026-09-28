using System;

namespace Game_Manager.Helpers
{
    /// <summary>
    /// 卡片尺寸设置的默认值、上下限与解析规则。
    /// 约定：宽度字段为 0 表示「用默认值」；高度字段为 0 表示「自动」
    /// （不写死高度、由内容撑开，保持历史观感）。
    /// 之所以要这套解析，是因为旧版 settings.json 里没有这些字段，
    /// 反序列化会得到 0，必须回退而不是当成真实尺寸使用。
    /// </summary>
    public static class CardSizeOptions
    {
        // ---------- 网格卡片 · 普通页面 ----------
        public const int GridCardWidthDefault = 240;
        public const int GridCardWidthMin = 170;
        public const int GridCardWidthMax = 400;

        public const int GridCardHeightMin = 240;
        public const int GridCardHeightMax = 560;
        /// <summary>取消「自动」时的起始高度。</summary>
        public const int GridCardHeightFallback = 340;

        // ---------- 网格卡片 · 归档页面 ----------
        public const int ArchiveGridCardWidthDefault = 220;
        public const int ArchiveGridCardWidthMin = 160;
        public const int ArchiveGridCardWidthMax = 380;

        public const int ArchiveGridCardHeightMin = 220;
        public const int ArchiveGridCardHeightMax = 520;
        public const int ArchiveGridCardHeightFallback = 320;

        // ---------- 列表行 · 普通页面 ----------
        public const int ListRowHeightMin = 60;
        public const int ListRowHeightMax = 200;
        public const int ListRowHeightFallback = 84;

        // ---------- 列表行 · 归档页面 ----------
        public const int ArchiveListRowHeightMin = 92;
        public const int ArchiveListRowHeightMax = 220;
        public const int ArchiveListRowHeightFallback = 92;

        // ---------- 「自动」状态下封面沿用的历史写死高度 ----------
        public const double GridCardCoverHeightWhenAuto = 124;
        public const double ListRowCoverHeightWhenAuto = 64;
        public const double ArchiveListRowCoverHeightWhenAuto = 66;

        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        /// <summary>宽度：0 或越界时回退到默认值，否则夹到范围内。</summary>
        public static int ResolveWidth(int stored, int fallback, int min, int max)
        {
            return stored <= 0 ? fallback : Clamp(stored, min, max);
        }

        /// <summary>高度：0 原样返回（表示自动），否则夹到范围内。</summary>
        public static int ResolveHeight(int stored, int min, int max)
        {
            return stored <= 0 ? 0 : Clamp(stored, min, max);
        }

        /// <summary>把「0 = 自动」的高度转成可直接绑定 Height 的 double（0 → NaN，即 Auto）。</summary>
        public static double ToHeightOrAuto(int stored)
        {
            return stored <= 0 ? double.NaN : stored;
        }

        /// <summary>
        /// 封面高度：自动时沿用历史写死值，手动时返回 NaN 让封面跟随卡片拉伸。
        /// </summary>
        public static double CoverHeightOrStretch(int stored, double autoHeight)
        {
            return stored <= 0 ? autoHeight : double.NaN;
        }
    }
}
