# 拆图配置（slice_preview.py run）

坐标一律是**预览图像素**（x 向右、y 向下）。用 `candidates.png` 的网格和编号区域读坐标与种子点。完整示例见 [example-mainpage.slice.json](example-mainpage.slice.json)（主界面：岛屿、宝箱、史莱姆、蛋、倒计时气泡、页签）。

```json
{
  "source": "ArtSource/Page/Preview.png",
  "out": ".codex_tmp/slice/page",
  "background": {"tol": 14, "pocket_min": 150},
  "outline": [24, 24, 34],
  "order": ["island", "tabbar_bg", "chest"],
  "parts": [ { "name": "chest", "box": [236, 488, 500, 705], "seed": [360, 560] } ]
}
```

- `background`：背景色取图片边缘的众数，`tol` 为色差容差；被物体围住、颜色与背景相同且面积 ≥ `pocket_min` 的空隙也算背景。
- `outline`：边缘羽化用的描边颜色，缺省时取图中最暗像素的中位数。
- `order`：拼回对比图（`recompose.png`）时从下到上的图层顺序，缺省按 `parts` 顺序。

## 部件字段

| 字段 | 说明 |
| --- | --- |
| `name` | 部件名，输出 `<name>.png` |
| `mode` | `enclosed`（默认）、`region`、`inner`、`rest`、`box`，见下 |
| `box` | 搜索范围 `[x0, y0, x1, y1]` |
| `seed` / `seeds` | 种子点 `[x, y]`（`region` 用 `seeds` 列表） |
| `dark` | 描边亮度阈值，低于它视为描边，默认 80；底色较暗的框用 45 左右 |
| `smooth` | 开运算半径，去掉粘连的细条（例如旁边的蘑菇枝），常用 2–10 |
| `grow` | 向外扩张像素后再填洞（`region` 用来补回外描边） |
| `erase` | 要擦掉并补全的内容，见下 |
| `keep_edge` | 擦除区距离部件边缘至少保留的像素，默认 6（保护外框）；整块底板用 0 |
| `fill` | 补全方式：`smooth`（平滑色）、`gradient`（拟合渐变，适合按钮/面板）、`texture`（底色 + 采样纹理，适合草地等场景）、`none` |
| `ai` | `true` 或一句补全说明（英文效果更稳），供 `ai_fill.py` 使用 |
| `folder` / `file` / `border` / `readable` | 入库目录、文件名、九宫格边框（数字或 `[l, b, r, t]`）、是否开启 Read/Write |

### 模式

- `enclosed`：在 `box` 内找被描边围住、包含 `seed` 的形状，并填满内部的洞。适用于独立物件、按钮底板、气泡。
- `region`：在 `box` 内从多个 `seeds` 泛洪亮于 `dark` 的像素后填洞。适用于镂空的长条框（如整个页签底），常配合 `grow` 使用。
- `inner`：父部件 `of` 内部的独立图形（按钮图标）。会排除碰到 `box` 左、上、右边的连通块（用 `exclude_edges` 改，如 `"lt"`）；`min_area` 默认 60。`box` 下沿放在文字上方，就能把图标和文字分开。
- `rest`：去掉背景和所有前景部件后剩下的部分（场景底图）。`under` 列出压在它上面的部件，它们遮住的区域会被重建；写成 `{"part": "tabbar_bg", "extend": false}` 表示不向该部件下方延伸轮廓，只填封闭的洞。`exclude` 列出与它相邻、不需要重建的部件。
- `box`：直接框选（去掉背景色）。

### erase 条目

- `{"part": "icon_gallery", "grow": 6}`：擦掉另一个部件覆盖的区域（按钮上的图标）。
- `{"text": [x0, y0, x1, y1], "pad": 4}`：在框内找文字像素（很暗或很亮），擦掉它们的外接矩形。
- `{"box": [x0, y0, x1, y1]}`：直接擦掉一块矩形。

## 输出

`<out>/<name>.png`（透明背景，裁到内容 + 2px）、`layout.json`（每个部件在原图中的位置和尺寸，以及入库字段）、`fill/<name>.png`（被补全区域的遮罩，供 AI 使用）、`recompose.png`、`sheet.png`。
