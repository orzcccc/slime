using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// 分段选择（G14）与页签（G09）共用：直接子节点中带 Button 的为选项，按层级顺序编号；
    /// 每个选项下名为 selected 的子节点为选中态底板，文字按选中状态切换颜色。
    /// </summary>
    public sealed class SegmentedControlView : MonoBehaviour
    {
        private const string SelectedChildName = "selected";

        public Color selectedTextColor = Color.white;
        public Color normalTextColor = new Color32(197, 207, 240, 255);

        private readonly List<Button> options = new List<Button>();
        private readonly List<GameObject> highlights = new List<GameObject>();
        private readonly List<TMP_Text> labels = new List<TMP_Text>();
        private bool bound;

        public event Action<int> SelectionChanged;

        public int SelectedIndex { get; private set; } = -1;

        public int Count
        {
            get
            {
                Bind();
                return options.Count;
            }
        }

        private void Awake() => Bind();

        public void SetLabel(int index, string text)
        {
            Bind();
            if (index >= 0 && index < labels.Count && labels[index] != null) labels[index].text = text;
        }

        public void Select(int index)
        {
            Bind();
            SelectedIndex = index;
            for (var i = 0; i < options.Count; i++)
            {
                var selected = i == index;
                if (highlights[i] != null) highlights[i].SetActive(selected);
                if (labels[i] != null) labels[i].color = selected ? selectedTextColor : normalTextColor;
            }
        }

        private void Bind()
        {
            if (bound) return;
            bound = true;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var button = child.GetComponent<Button>();
                if (button == null) continue;
                var index = options.Count;
                options.Add(button);
                var highlight = child.Find(SelectedChildName);
                highlights.Add(highlight != null ? highlight.gameObject : null);
                labels.Add(child.GetComponentInChildren<TMP_Text>(true));
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() =>
                {
                    if (index == SelectedIndex) return;
                    Select(index);
                    SelectionChanged?.Invoke(index);
                });
            }
        }
    }
}

