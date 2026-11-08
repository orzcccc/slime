using System;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using SlimeBlindBox.Desktop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// 设置弹窗（S01 窗口、S02 声音与提醒、S03 账号与关于）与退出确认层（S04）。
    /// 所有修改即时生效并自动保存；系统调用或保存失败时开关回到原状态并给出轻提示（G05）。
    /// 同时处理托盘菜单命令和系统关闭请求（Alt+F4 等），保证与设置开关同步、退出都走确认层。
    /// </summary>
    public sealed class GameSettingsView : MonoBehaviour
    {
        public const int WindowTab = 0;
        public const int SoundTab = 1;
        public const int AccountTab = 2;
        private const float ToastSeconds = 2f;

        [Header("通用")]
        public Button closeButton;
        public SegmentedControlView tabs;
        public GameObject windowPage;
        public GameObject soundPage;
        public GameObject accountPage;
        public GameObject toastRoot;
        public TMP_Text toastText;

        [Header("S01 窗口")]
        public ToggleSwitchView alwaysOnTopToggle;
        public ToggleSwitchView autoStartToggle;
        public ToggleSwitchView lockPositionToggle;
        public SegmentedControlView scaleSelector;
        public Button resetPositionButton;

        [Header("S02 声音与提醒")]
        public SettingsSliderView sfxSlider;
        public ToggleSwitchView sfxToggle;
        public SettingsSliderView bgmSlider;
        public ToggleSwitchView bgmToggle;
        public ToggleSwitchView boxFullToggle;
        public TMP_Text boxFullDescText;
        public GameObject notificationsDisabledText;
        public SegmentedControlView openAnimationSelector;

        [Header("S03 账号与关于")]
        public Image avatarImage;
        public TMP_Text nicknameText;
        public TMP_Text accountStatusText;
        public TMP_Text versionText;
        public Button userAgreementButton;
        public Button privacyPolicyButton;
        public Button quitButton;

        [Header("S04 退出确认")]
        public GameObject quitLayer;
        public TMP_Text quitDescText;
        public GameObject quitBlockedText;
        public Button quitCancelButton;
        public Button quitConfirmButton;

        private SlimeGameController controller;
        private Func<bool> isOpeningBox;
        private Action onOpened;
        private Action onClosed;
        private DesktopTrayController tray;
        private float toastHideAt;
        private bool quitConfirmed;
        private bool initialized;

        public void Initialize(SlimeGameController gameController, TMP_FontAsset font, Func<bool> openingBox,
            Action opened, Action closed, DesktopTrayController trayController)
        {
            controller = gameController;
            isOpeningBox = openingBox;
            onOpened = opened;
            onClosed = closed;
            tray = trayController;

            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                if (font != null) text.font = font;

            closeButton.onClick.AddListener(Hide);
            tabs.SelectionChanged += ShowTab;

            alwaysOnTopToggle.ValueChanged += OnAlwaysOnTopChanged;
            autoStartToggle.ValueChanged += OnAutoStartChanged;
            lockPositionToggle.ValueChanged += OnLockPositionChanged;
            scaleSelector.SelectionChanged += OnScaleSelected;
            resetPositionButton.onClick.AddListener(ResetWindowPosition);
            for (var index = 0; index < LocalSettings.ScalePercents.Length && index < scaleSelector.Count; index++)
                scaleSelector.SetLabel(index, string.Format(SettingsUiText.ScaleFormat, LocalSettings.ScalePercents[index]));

            sfxToggle.ValueChanged += OnSfxToggled;
            bgmToggle.ValueChanged += OnBgmToggled;
            sfxSlider.ValueChanged += _ => PreviewVolumes();
            bgmSlider.ValueChanged += _ => PreviewVolumes();
            sfxSlider.Released += OnSfxVolumeReleased;
            bgmSlider.Released += OnBgmVolumeReleased;
            boxFullToggle.ValueChanged += OnBoxFullReminderChanged;
            openAnimationSelector.SelectionChanged += OnOpenAnimationSelected;

            userAgreementButton.onClick.AddListener(() => OpenLink(ExternalLinks.UserAgreementUrl));
            privacyPolicyButton.onClick.AddListener(() => OpenLink(ExternalLinks.PrivacyPolicyUrl));
            quitButton.onClick.AddListener(ShowQuitConfirm);
            quitCancelButton.onClick.AddListener(HideQuitConfirm);
            quitConfirmButton.onClick.AddListener(ConfirmQuit);

            if (controller != null) controller.StateChanged += RefreshAccount;
            if (tray != null) tray.CommandRequested += OnTrayCommand;
            Application.wantsToQuit += OnWantsToQuit;

            quitLayer.SetActive(false);
            toastRoot.SetActive(false);
            initialized = true;
            RefreshAll();
            ShowTab(WindowTab);
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            if (controller != null) controller.StateChanged -= RefreshAccount;
            if (tray != null) tray.CommandRequested -= OnTrayCommand;
            Application.wantsToQuit -= OnWantsToQuit;
        }

        public void Show() => Show(WindowTab);

        public void Show(int tab)
        {
            var wasVisible = gameObject.activeSelf;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RefreshAll();
            ShowTab(tab);
            if (!wasVisible) onOpened?.Invoke();
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            quitLayer.SetActive(false);
            toastRoot.SetActive(false);
            gameObject.SetActive(false);
            onClosed?.Invoke();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (quitLayer.activeSelf) HideQuitConfirm();
                else Hide();
                return;
            }
            if (quitLayer.activeSelf) UpdateQuitBlocked();
            if (toastRoot.activeSelf && Time.unscaledTime >= toastHideAt) toastRoot.SetActive(false);
        }

        private void ShowTab(int tab)
        {
            tabs.Select(tab);
            windowPage.SetActive(tab == WindowTab);
            soundPage.SetActive(tab == SoundTab);
            accountPage.SetActive(tab == AccountTab);
        }

        private void RefreshAll()
        {
            alwaysOnTopToggle.SetIsOn(LocalSettings.AlwaysOnTop);
            autoStartToggle.Interactable = WindowsShellSettings.AutoStartSupported;
            autoStartToggle.SetIsOn(WindowsShellSettings.IsAutoStartEnabled());
            lockPositionToggle.SetIsOn(LocalSettings.LockPosition);
            scaleSelector.Select(Array.IndexOf(LocalSettings.ScalePercents, DesktopWindowController.ScalePercent));

            sfxToggle.SetIsOn(LocalSettings.SfxEnabled);
            sfxSlider.SetValue(LocalSettings.SfxVolume);
            sfxSlider.SetEnabledState(LocalSettings.SfxEnabled);
            bgmToggle.SetIsOn(LocalSettings.BgmEnabled);
            bgmSlider.SetValue(LocalSettings.BgmVolume);
            bgmSlider.SetEnabledState(LocalSettings.BgmEnabled);
            boxFullToggle.SetIsOn(LocalSettings.BoxFullReminder);
            notificationsDisabledText.SetActive(!WindowsShellSettings.AreNotificationsEnabled());
            openAnimationSelector.Select(LocalSettings.FastOpenAnimation ? 1 : 0);
            RefreshAccount();
        }

        private void RefreshAccount()
        {
            var capacity = BoxCapacity;
            boxFullDescText.text = string.Format(SettingsUiText.BoxFullReminderDescFormat, capacity);
            var nickname = controller?.User?.nickname;
            nicknameText.text = string.IsNullOrEmpty(nickname) ? SettingsUiText.FallbackNickname : nickname;
            var state = controller?.GameState;
            accountStatusText.text = state != null
                ? string.Format(SettingsUiText.LocalLoginStatusFormat, state.total_boxes_opened)
                : SettingsUiText.LocalLoginStatusUnknown;
            versionText.text = string.Format(SettingsUiText.VersionFormat, Application.version);
            if (quitLayer.activeSelf) quitDescText.text = string.Format(SettingsUiText.QuitDescFormat, capacity);
        }

        private int BoxCapacity
        {
            get
            {
                var capacity = controller?.GameState?.box_capacity ?? 0;
                return capacity > 0 ? capacity : GameBalanceConfig.DefaultBoxCapacity;
            }
        }

        // ---------- S01 窗口 ----------

        private void OnAlwaysOnTopChanged(bool value)
        {
            var window = DesktopWindowController.Instance;
            if (window != null && !window.SetAlwaysOnTop(value))
            {
                Revert(alwaysOnTopToggle, !value, SettingsUiText.ApplyFailed);
                return;
            }
            if (LocalSettings.SetAlwaysOnTop(value)) return;
            if (window != null) window.SetAlwaysOnTop(!value);
            Revert(alwaysOnTopToggle, !value, SettingsUiText.SaveFailed);
        }

        private void OnAutoStartChanged(bool value)
        {
            if (!WindowsShellSettings.AutoStartSupported)
            {
                Revert(autoStartToggle, !value, SettingsUiText.AutoStartUnsupported);
                return;
            }
            if (!WindowsShellSettings.SetAutoStart(value)) Revert(autoStartToggle, !value, SettingsUiText.ApplyFailed);
        }

        private void OnLockPositionChanged(bool value)
        {
            if (!LocalSettings.SetLockPosition(value))
            {
                Revert(lockPositionToggle, !value, SettingsUiText.SaveFailed);
                return;
            }
            var window = DesktopWindowController.Instance;
            if (window != null) window.PositionLocked = value;
        }

        private void OnScaleSelected(int index)
        {
            var previous = DesktopWindowController.ScalePercent;
            var previousIndex = Array.IndexOf(LocalSettings.ScalePercents, previous);
            if (index < 0 || index >= LocalSettings.ScalePercents.Length) return;
            var percent = LocalSettings.ScalePercents[index];
            var window = DesktopWindowController.Instance;
            if (window != null && !window.ApplyScale(percent))
            {
                scaleSelector.Select(previousIndex);
                ShowToast(SettingsUiText.ApplyFailed);
                return;
            }
            if (LocalSettings.SetScalePercent(percent)) return;
            if (window != null) window.ApplyScale(previous);
            scaleSelector.Select(previousIndex);
            ShowToast(SettingsUiText.SaveFailed);
        }

        private void ResetWindowPosition()
        {
            var window = DesktopWindowController.Instance;
            ShowToast(window != null && window.ResetPosition() ? SettingsUiText.PositionReset : SettingsUiText.ApplyFailed);
        }

        // ---------- S02 声音与提醒 ----------

        private void OnSfxToggled(bool value)
        {
            if (!LocalSettings.SetSfxEnabled(value))
            {
                Revert(sfxToggle, !value, SettingsUiText.SaveFailed);
                return;
            }
            sfxSlider.SetEnabledState(value);
        }

        private void OnBgmToggled(bool value)
        {
            if (!LocalSettings.SetBgmEnabled(value))
            {
                Revert(bgmToggle, !value, SettingsUiText.SaveFailed);
                return;
            }
            bgmSlider.SetEnabledState(value);
        }

        private void PreviewVolumes()
        {
            if (GameAudio.Instance != null) GameAudio.Instance.PreviewVolumes(sfxSlider.Value, bgmSlider.Value);
        }

        private void OnSfxVolumeReleased(float value)
        {
            if (!LocalSettings.SetSfxVolume(value))
            {
                sfxSlider.SetValue(LocalSettings.SfxVolume);
                PreviewVolumes();
                ShowToast(SettingsUiText.SaveFailed);
                return;
            }
            if (GameAudio.Instance != null) GameAudio.Instance.PlayPreview();
        }

        private void OnBgmVolumeReleased(float value)
        {
            if (LocalSettings.SetBgmVolume(value)) return;
            bgmSlider.SetValue(LocalSettings.BgmVolume);
            PreviewVolumes();
            ShowToast(SettingsUiText.SaveFailed);
        }

        private void OnBoxFullReminderChanged(bool value)
        {
            if (!LocalSettings.SetBoxFullReminder(value)) Revert(boxFullToggle, !value, SettingsUiText.SaveFailed);
            notificationsDisabledText.SetActive(!WindowsShellSettings.AreNotificationsEnabled());
        }

        private void OnOpenAnimationSelected(int index)
        {
            if (LocalSettings.SetFastOpenAnimation(index == 1)) return;
            openAnimationSelector.Select(LocalSettings.FastOpenAnimation ? 1 : 0);
            ShowToast(SettingsUiText.SaveFailed);
        }

        // ---------- S03 账号与关于 ----------

        private void OpenLink(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                ShowToast(SettingsUiText.LinkUnavailable);
                return;
            }
            Application.OpenURL(url);
        }

        // ---------- S04 退出确认 ----------

        /// <summary>托盘“退出”或系统关闭：窗口隐藏时先显示岛屿，再打开账号页与退出确认层。</summary>
        public void RequestQuit()
        {
            var window = DesktopWindowController.Instance;
            if (window != null) window.BringToFront();
            Show(AccountTab);
            ShowQuitConfirm();
        }

        private void ShowQuitConfirm()
        {
            quitLayer.SetActive(true);
            quitLayer.transform.SetAsLastSibling();
            quitDescText.text = string.Format(SettingsUiText.QuitDescFormat, BoxCapacity);
            UpdateQuitBlocked();
        }

        private void HideQuitConfirm() => quitLayer.SetActive(false);

        private void UpdateQuitBlocked()
        {
            var blocked = isOpeningBox != null && isOpeningBox();
            quitConfirmButton.interactable = !blocked;
            if (quitBlockedText.activeSelf != blocked) quitBlockedText.SetActive(blocked);
        }

        private void ConfirmQuit()
        {
            if (isOpeningBox != null && isOpeningBox()) return;
            quitConfirmed = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Alt+F4 等系统关闭请求统一走退出确认层。</summary>
        private bool OnWantsToQuit()
        {
            if (quitConfirmed) return true;
            RequestQuit();
            return false;
        }

        // ---------- 托盘 ----------

        private void OnTrayCommand(TrayCommand command)
        {
            switch (command)
            {
                case TrayCommand.AlwaysOnTop:
                    var topmost = !LocalSettings.AlwaysOnTop;
                    alwaysOnTopToggle.SetIsOn(topmost);
                    OnAlwaysOnTopChanged(topmost);
                    break;
                case TrayCommand.LockPosition:
                    var locked = !LocalSettings.LockPosition;
                    lockPositionToggle.SetIsOn(locked);
                    OnLockPositionChanged(locked);
                    break;
                case TrayCommand.ResetPosition:
                    ResetWindowPosition();
                    break;
                case TrayCommand.OpenSettings:
                    var window = DesktopWindowController.Instance;
                    if (window != null) window.BringToFront();
                    Show(WindowTab);
                    break;
                case TrayCommand.Exit:
                    RequestQuit();
                    break;
            }
        }

        // ---------- 轻提示 ----------

        private void Revert(ToggleSwitchView toggle, bool value, string message)
        {
            toggle.SetIsOn(value);
            ShowToast(message);
        }

        private void ShowToast(string message)
        {
            toastText.text = message;
            toastRoot.SetActive(true);
            toastRoot.transform.SetAsLastSibling();
            toastHideAt = Time.unscaledTime + ToastSeconds;
        }
    }
}

