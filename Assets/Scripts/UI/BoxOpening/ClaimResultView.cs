using System;
using System.Collections;
using System.Collections.Generic;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    /// <summary>M03 领取结果：最多 8 个槽位依次从左到右翻面揭示品质，紫色/红色停顿更久并有闪光。</summary>
    public sealed class ClaimResultView : MonoBehaviour, IPointerClickHandler
    {

        public Button closeButton;
        public TMP_Text subtitleText;
        public Button tuckButton;
        public Button openButton;
        public TMP_Text skipText;
        public GameObject flash;

        private readonly List<ClaimResultTile> tiles = new List<ClaimResultTile>();
        private Action<ClaimResultExit> closed;
        [SerializeField] private ClaimResultTile tilePrefab;
        [SerializeField] private Transform tileGrid;
        private TMP_FontAsset font;
        private Corpus<ClaimResultTile> pool;
        private Coroutine playing;
        private bool shown;
        private string highestKey;

        public void Initialize(TMP_FontAsset runtimeFont, Action<ClaimResultExit> onClosed)
        {
            font = runtimeFont;
            closed = onClosed;
            pool = new Corpus<ClaimResultTile>(tilePrefab, tileGrid);
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = font;
            closeButton.onClick.AddListener(() => Close(ClaimResultExit.Home));
            tuckButton.onClick.AddListener(() => Close(ClaimResultExit.Home));
            openButton.onClick.AddListener(() => Close(ClaimResultExit.OpenHighest));
        }

        public void Show(ClaimBoxesResponse response)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            shown = false;
            subtitleText.text = string.Format(BoxOpeningUiText.ClaimSubtitleFormat, response.claimed);
            var keys = response.claimed_boxes ?? Array.Empty<string>();
            tiles.Clear();
            pool.Begin();
            highestKey = "WHITE";
            var highestRank = -1;
            for (var index = 0; index < keys.Length && index < 8; index++)
            {
                var tile = pool.Get();
                tile.Bind(BoxVisuals.Sprite(keys[index]), BoxVisuals.Color(keys[index]), keys[index], Skip);
                var rank = BoxVisuals.Rank(keys[index]);
                if (rank > highestRank)
                {
                    highestRank = rank;
                    highestKey = keys[index];
                }
                tiles.Add(tile);
            }
            pool.End();
            skipText.gameObject.SetActive(keys.Length > 0);
            openButton.interactable = false;
            flash.SetActive(false);
            playing = StartCoroutine(PlayReveal());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!shown) Skip();
        }

        private IEnumerator PlayReveal()
        {
            yield return null;
            for (var index = 0; index < tiles.Count; index++)
            {
                var tile = tiles[index];
                tile.Reveal();
                var key = tile.RarityKey;
                var rank = BoxVisuals.Rank(key);
                if (rank >= BoxVisuals.Rank("PURPLE"))
                {
                    flash.SetActive(true);
                    yield return new WaitForSecondsRealtime(rank >= BoxVisuals.Rank("RED") ? .32f : .22f);
                    flash.SetActive(false);
                }
                else
                {
                    yield return new WaitForSecondsRealtime(.15f);
                }
            }
            Finish();
        }

        private void Skip()
        {
            if (playing != null) StopCoroutine(playing);
            foreach (var tile in tiles) tile.RevealInstant();
            Finish();
        }

        private void Finish()
        {
            playing = null;
            shown = true;
            flash.SetActive(false);
            skipText.gameObject.SetActive(false);
            openButton.interactable = tiles.Count > 0;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Close(ClaimResultExit.Home);
        }

        private void OnDisable()
        {
            playing = null;
        }

        private void Close(ClaimResultExit exit)
        {
            if (playing != null) StopCoroutine(playing);
            gameObject.SetActive(false);
            closed?.Invoke(exit);
        }

        /// <summary>开箱页要用到领取结果中的最高品质；返回 null 表示没有可开盲盒。</summary>
        public string HighestBoxKey => tiles.Count > 0 ? highestKey : null;
    }

    public enum ClaimResultExit
    {
        Home,
        OpenHighest,
    }
}
