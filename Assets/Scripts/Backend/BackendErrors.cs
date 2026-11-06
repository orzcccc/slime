namespace SlimeBlindBox.Backend
{
    /// <summary>后端错误码。与未来 PlayFab CloudScript 返回的错误码保持一致。</summary>
    public static class BackendErrorCodes
    {
        public const string NotLoggedIn = "NOT_LOGGED_IN";
        public const string NoPendingBoxes = "NO_PENDING_BOXES";
        public const string UnknownBoxRarity = "UNKNOWN_BOX_RARITY";
        public const string BoxStockEmpty = "BOX_STOCK_EMPTY";
        public const string InvalidArgument = "INVALID_ARGUMENT";
        public const string DebugToolsDisabled = "DEBUG_TOOLS_DISABLED";
        public const string SaveFailed = "SAVE_FAILED";
        public const string LoadFailed = "LOAD_FAILED";
        public const string SaveVersionTooNew = "SAVE_VERSION_TOO_NEW";
        public const string ConfigInvalid = "CONFIG_INVALID";
        public const string Internal = "INTERNAL";
    }

    /// <summary>后端返回给玩家的提示文案，集中管理以便本地化。</summary>
    public static class BackendMessages
    {
        public const string NotLoggedIn = "尚未登录，请重新启动游戏";
        public const string NoPendingBoxes = "当前没有可领取的盲盒";
        public const string UnknownBoxRarity = "未知的盲盒品质";
        public const string BoxStockEmptyFormat = "仓库中没有{0}盲盒";
        public const string DebugAddRangeFormat = "增加数量范围为 {0}-{1}";
        public const string DebugAdvanceRangeFormat = "推进小时范围为 {0}-{1}";
        public const string DebugToolsDisabled = "当前版本不提供开发者工具";
        public const string SaveFailed = "存档保存失败，请检查磁盘空间或文件权限后重试";
        public const string LoadFailed = "存档读取失败，请检查文件权限后重新启动游戏";
        public const string SaveVersionTooNew = "存档来自更新版本的游戏，请更新游戏后再试";
        public const string ConfigInvalid = "游戏数据配置异常，请重新安装游戏";
        public const string Internal = "发生未知错误，请稍后重试";
    }

    /// <summary>业务规则校验失败时抛出，由后端转换为 <see cref="BackendError"/>。</summary>
    public sealed class BackendException : System.Exception
    {
        public BackendException(string code, string message) : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
