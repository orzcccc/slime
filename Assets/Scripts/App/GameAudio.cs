using UnityEngine;

namespace SlimeBlindBox.App
{
    /// <summary>
    /// 全局音频：音效与背景音乐两路，音量与开关来自 LocalSettings。
    /// 背景音乐从 Resources/Audio/Bgm 加载，试听音效从 Resources/Audio/SfxPreview 加载；
    /// 正式音频尚未制作，缺失时背景音乐不播放，试听使用程序生成的短提示音（占位）。
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const string BgmPath = "Audio/Bgm";
        private const string PreviewPath = "Audio/SfxPreview";
        private const int PlaceholderSampleRate = 44100;
        private const float PlaceholderSeconds = 0.12f;
        private const float PlaceholderFrequency = 880f;

        public static GameAudio Instance { get; private set; }

        private AudioSource sfxSource;
        private AudioSource bgmSource;
        private AudioClip previewClip;

        private void Awake()
        {
            Instance = this;
            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.clip = Resources.Load<AudioClip>(BgmPath);
            previewClip = Resources.Load<AudioClip>(PreviewPath);
            if (previewClip == null) previewClip = CreatePlaceholderBeep();
            LocalSettings.Changed += ApplySettings;
            ApplySettings();
        }

        private void OnDestroy()
        {
            LocalSettings.Changed -= ApplySettings;
            if (Instance == this) Instance = null;
        }

        /// <summary>拖动滑条时实时预览音量（尚未保存）。</summary>
        public void PreviewVolumes(float sfxVolume, float bgmVolume)
        {
            sfxSource.volume = LocalSettings.SfxEnabled ? Mathf.Clamp01(sfxVolume) : 0f;
            bgmSource.volume = Mathf.Clamp01(bgmVolume);
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null || !LocalSettings.SfxEnabled) return;
            sfxSource.PlayOneShot(clip);
        }

        /// <summary>松开音效滑条时试听一次。</summary>
        public void PlayPreview() => PlaySfx(previewClip);

        private void ApplySettings()
        {
            sfxSource.volume = LocalSettings.SfxEnabled ? LocalSettings.SfxVolume : 0f;
            bgmSource.volume = LocalSettings.BgmVolume;
            var playBgm = LocalSettings.BgmEnabled && bgmSource.clip != null;
            if (playBgm && !bgmSource.isPlaying) bgmSource.Play();
            else if (!playBgm && bgmSource.isPlaying) bgmSource.Stop();
        }

        private static AudioClip CreatePlaceholderBeep()
        {
            var samples = Mathf.RoundToInt(PlaceholderSampleRate * PlaceholderSeconds);
            var data = new float[samples];
            for (var index = 0; index < samples; index++)
            {
                var time = index / (float)PlaceholderSampleRate;
                var envelope = 1f - index / (float)samples;
                data[index] = Mathf.Sin(2f * Mathf.PI * PlaceholderFrequency * time) * envelope * 0.4f;
            }
            var clip = AudioClip.Create("SfxPreviewPlaceholder", samples, 1, PlaceholderSampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

