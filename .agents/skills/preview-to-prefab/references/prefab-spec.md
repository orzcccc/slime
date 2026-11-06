# 拼预制配置（prefab_build.py）

坐标是**参考分辨率**（800×800、中心为原点、y 向上），与 `sprites.json` 的 `ref_center`/`ref_size` 一致。

```json
{
  "prefab": "Assets/Resources/UI/MainUI.prefab",
  "sprites": ".codex_tmp/slice/main/sprites.json",
  "parent": "MainUI/SafeArea/Background/HomePage",
  "nodes": [ { "name": "chestImage", "sprite_rect": "chest", "image": {"sprite": "chest", "preserve": true} } ],
  "refs": { "MainUI:MainUiReferences": { "chestImage": "@MainUI/SafeArea/Background/HomePage/chestImage:Image" } },
  "delete": ["MainUI/SafeArea/Background/oldNode"]
}
```

- 新建预制：写 `"root": {节点}` 代替 `parent`/`nodes`，同时会生成 `.prefab.meta`。
- `parent` 和引用路径都是从根节点开始、按 GameObject 名称组成的路径。
- 节点已存在（同一父节点下同名）时就地更新，只修改配置里写到的属性，并保留原有 fileID。
- `--dry-run` 只写出 `<prefab>.preview`，不改原文件。

## 节点字段

| 字段 | 说明 |
| --- | --- |
| `name` | 节点名（英文驼峰，见命名规则） |
| `rect` | `anchor`（`center`、`stretch`、`top`、`bottom`、`left`、`right`、`top-left`、`top-right`、`bottom-left`、`bottom-right`、`top-center`、`bottom-center`、`middle-left`、`middle-right`），`pos`、`size`、`pivot`；或显式写 `amin`/`amax`。`offset: [l, b, r, t]` 用于拉伸方向的内边距，非拉伸方向则平移位置 |
| `sprite_rect` | 用 `sprites.json` 里某张图的尺寸和中心作为中心锚点矩形；父节点不在画布中心时，在 `rect.parent_center` 写父节点中心坐标 |
| `image` | `sprite`（`sprites.json` 的名字、`guid:<guid>` 或 `Assets/...png` 路径）、`type`（`simple`/`sliced`/`tiled`/`filled`）、`preserve`、`raycast`（按钮默认 true）、`color`（`#rrggbb[aa]`）、`ppu` |
| `text` | `value`、`size`、`color`、`align`（`left`/`center`/`right`）、`valign`（`top`/`middle`/`bottom`）、`bold`、`wrap`（单行文字保持 false） |
| `button` | `true`：添加 Button，目标图形为本节点 Image，状态色按 UI 框架统一 |
| `slider` | `{"fill": "fillArea/fillImage", "value": 0.6}`：只读进度条 |
| `layout` | `type`（`horizontal`/`vertical`/`grid`）、`padding: [l, r, t, b]`、`spacing`、`align`（如 `middle-center`）、`control: [w, h]`、`expand: [w, h]`；grid 用 `cell`、`count` |
| `element` | LayoutElement：`min`、`preferred`、`flexible`，均为 `[w, h]`，-1 表示不设 |
| `components` | `[{"script": "SlimeWander", "fields": {"area": "@HomePage/slimeArea:RectTransform", "speed": 40}}]` |
| `active` | 初始显隐 |
| `children` | 子节点；`"ordered_children": true` 时按数组顺序重排已存在的子节点 |

字段值：`"@路径:类型"` 表示引用，类型可以是 `GameObject`、`RectTransform`、`Image`、`Button`、`TMP`、`Slider` 或项目脚本名；`[x, y]` 写成向量，`"#rrggbb"` 写成颜色，其余原样写入。
