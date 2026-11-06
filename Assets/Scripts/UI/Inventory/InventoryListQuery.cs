using System;
using System.Collections.Generic;
using SlimeBlindBox.Backend;

namespace SlimeBlindBox.UI
{
    public enum InventorySortMode
    {
        Quality,
        Cp,
        Level,
        Recent,
        Quantity,
    }

    /// <summary>背包列表的显示顺序与品质筛选。只决定展示顺序，不修改任何玩家数据。</summary>
    public static class InventoryListQuery
    {
        public static readonly InventorySortMode[] SortModes =
        {
            InventorySortMode.Quality, InventorySortMode.Cp, InventorySortMode.Level, InventorySortMode.Recent, InventorySortMode.Quantity,
        };

        private static readonly Comparison<InventoryItemDto>[] Comparers =
        {
            (a, b) => Chain(RarityRank(b.rarity_key).CompareTo(RarityRank(a.rarity_key)), b.unit_effective_cp.CompareTo(a.unit_effective_cp), a, b),
            (a, b) => Chain(b.unit_effective_cp.CompareTo(a.unit_effective_cp), RarityRank(b.rarity_key).CompareTo(RarityRank(a.rarity_key)), a, b),
            (a, b) => Chain(b.level.CompareTo(a.level), b.unit_effective_cp.CompareTo(a.unit_effective_cp), a, b),
            // 首次获得时间是同一格式的 UTC ISO 8601 字符串，按字典序比较即按时间比较。
            (a, b) => Chain(string.CompareOrdinal(b.first_obtained_at, a.first_obtained_at), 0, a, b),
            (a, b) => Chain(b.quantity.CompareTo(a.quantity), RarityRank(b.rarity_key).CompareTo(RarityRank(a.rarity_key)), a, b),
        };

        public static string SortLabel(InventorySortMode mode)
        {
            switch (mode)
            {
                case InventorySortMode.Cp: return InventoryUiText.SortCp;
                case InventorySortMode.Level: return InventoryUiText.SortLevel;
                case InventorySortMode.Recent: return InventoryUiText.SortRecent;
                case InventorySortMode.Quantity: return InventoryUiText.SortQuantity;
                default: return InventoryUiText.SortQuality;
            }
        }

        /// <summary>把 source 中符合品质筛选的条目按排序方式写入 result。筛选集合为空表示全部品质。</summary>
        public static void Apply(IReadOnlyList<InventoryItemDto> source, InventorySortMode mode, ICollection<string> rarityFilter, List<InventoryItemDto> result)
        {
            result.Clear();
            if (source != null)
            {
                var filtered = rarityFilter != null && rarityFilter.Count > 0;
                foreach (var item in source)
                    if (item != null && (!filtered || rarityFilter.Contains(item.rarity_key))) result.Add(item);
            }
            result.Sort(Comparers[(int)mode]);
        }

        public static int RarityRank(string rarityKey) => GameBalanceConfig.FindSlimeRarity(rarityKey)?.Rank ?? -1;

        private static int Chain(int primary, int secondary, InventoryItemDto a, InventoryItemDto b)
        {
            if (primary != 0) return primary;
            if (secondary != 0) return secondary;
            return a.slime_id.CompareTo(b.slime_id);
        }
    }
}
