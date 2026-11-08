namespace SlimeBlindBox.UI
{
    /// <summary>设置弹窗、退出确认与轻提示的面向玩家文案。集中管理，便于后续本地化。</summary>
    public static class SettingsUiText
    {
        public const string Title = "设置";
        public const string TabWindow = "窗口";
        public const string TabSound = "声音与提醒";
        public const string TabAccount = "账号与关于";

        public const string AlwaysOnTop = "始终置顶";
        public const string AlwaysOnTopDesc = "盖在其他窗口上方";
        public const string AutoStart = "开机自启";
        public const string AutoStartDesc = "登录 Windows 后自动出现在桌面";
        public const string LockPosition = "锁定位置";
        public const string LockPositionDesc = "锁定后按住岛屿不能拖动";
        public const string WindowScale = "窗口缩放";
        public const string WindowScaleDesc = "按屏幕清晰度整体缩放";
        public const string ScaleFormat = "{0}%";
        public const string ResetPosition = "重置窗口位置";
        public const string ResetPositionDesc = "回到主屏幕右下角";
        public const string ResetButton = "重置";
        public const string AutoSaveHint = "修改即时生效并自动保存";

        public const string Sfx = "音效";
        public const string Bgm = "背景音乐";
        public const string BoxFullReminder = "盲盒存满提醒";
        public const string BoxFullReminderDescFormat = "待领取存满 {0} 个时发送一条系统通知";
        public const string NotificationsDisabled = "系统通知已关闭，请在 Windows 设置中开启";
        public const string OpenAnimation = "开箱动画";
        public const string OpenAnimationDesc = "快速：缩短开箱过程，高稀有特效仍完整播放";
        public const string OpenAnimationFull = "完整";
        public const string OpenAnimationFast = "快速";

        public const string FallbackNickname = "Steam 玩家";
        public const string LocalLoginStatusFormat = "本地档案登录 · 累计开箱 {0:N0} 次";
        public const string LocalLoginStatusUnknown = "本地档案登录 · 累计开箱 -- 次";
        public const string Version = "版本";
        public const string VersionFormat = "v{0}";
        public const string UserAgreement = "用户协议";
        public const string PrivacyPolicy = "隐私政策";
        public const string QuitGame = "退出游戏";

        public const string QuitTitle = "退出史莱姆盲盒？";
        public const string QuitDescFormat = "离线期间盲盒照常凝聚，最多存 {0} 个，\n下次打开就能领取。";
        public const string QuitBlockedWhileOpening = "开箱完成后再退出";
        public const string QuitCancel = "取消";
        public const string QuitConfirm = "退出";

        public const string SaveFailed = "设置保存失败，已恢复原状态";
        public const string ApplyFailed = "系统设置失败，已恢复原状态";
        public const string AutoStartUnsupported = "开机自启仅在 Windows 正式版中可用";
        public const string LinkUnavailable = "链接暂未开放";
        public const string PositionReset = "已回到主屏幕右下角";
    }
}

