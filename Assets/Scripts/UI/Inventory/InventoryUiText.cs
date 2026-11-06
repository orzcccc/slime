namespace SlimeBlindBox.UI
{
    /// <summary>背包、筛选层与史莱姆详情面向玩家的文案。集中放在这里，后续接入本地化时统一替换。</summary>
    public static class InventoryUiText
    {
        public const string Title = "背包";
        public const string SummaryFormat = "持有 {0} 种 · 共 {1} 只 · 总CP {2:N0}";
        public const string SortFormat = "排序：{0}";
        public const string FilterAll = "筛选：全部品质";
        public const string FilterOneFormat = "筛选：{0}";
        public const string FilterManyFormat = "筛选：已选 {0} 种品质";
        public const string SortQuality = "品质 高→低";
        public const string SortCp = "CP 高→低";
        public const string SortLevel = "等级 高→低";
        public const string SortRecent = "最近获得";
        public const string SortQuantity = "数量 多→少";
        public const string SortTitle = "排序方式";
        public const string FilterTitle = "按品质筛选（可多选）";
        public const string FilterReset = "重置";
        public const string FilterConfirm = "确定";
        public const string PageFormat = "{0} / {1}";
        public const string CountFormat = "×{0}";
        public const string IslandBadge = "岛";
        public const string CardStatsFormat = "Lv.{0} · CP {1:N0}";
        public const string EmptyBag = "还没有史莱姆，去开个盲盒吧";
        public const string EmptyBagAction = "去开盲盒";
        public const string NoMatch = "没有符合条件的史莱姆";
        public const string NoMatchAction = "重置筛选";
        public const string Loading = "正在读取背包……";
        public const string LoadFailed = "背包读取失败，请稍后重试";
        public const string Retry = "重试";
        public const string Back = "< 返回";
        public const string OwnedFormat = "持有 ×{0} · 首次获得 {1}";
        public const string UnknownDate = "--";
        public const string DateFormat = "yyyy-MM-dd";
        public const string LevelFormat = "等级 Lv.{0}";
        public const string LevelLocked = "升级方式待开放";
        public const string CpFormat = "基础CP {0:N0}    有效CP {1:N0}（Lv.{2} 加成 ×{3:0.##}）";
        public const string TotalFormat = "本种合计 CP {0:N0}（×{1}）";
        public const string ViewInDictionary = "在图鉴中查看";
        public const string ShowOnIsland = "放到岛上展示";
    }
}
