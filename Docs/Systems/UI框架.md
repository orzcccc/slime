# 最小 UI 框架

> 状态：规则已确认；客户端尚未迁移（见“九、迁移待办”）  
> 最后更新：2026-11-05  
> 主策划入口：[《史莱姆盲盒》项目策划案](../史莱姆盲盒-项目策划案.md)  
> 关联代码：`Assets/Scripts/UI/SafeAreaFitter.cs`、`Assets/Scripts/UI/MainUiReferences.cs`、`Assets/Scripts/UI/GameSettingsView.cs`、`Assets/Scripts/UI/ResponsiveMainUi.cs`  
> 关联文档：[预览图生成 UI 预制规则](预览图生成UI预制规则.md)、[桌面岛屿窗口系统](桌面岛屿窗口系统.md)

## 一、游戏前提

| 项目 | 值 |
| --- | --- |
| 参考分辨率 | `800×800`（固定桌面窗口，见桌面岛屿窗口系统） |
| 方向 | 正方形，窗口不可缩放 |
| CanvasScaler match | `0.5` |
| 第二种比例检查 | 不适用；高 DPI 缩放规则确定后补充（待确认） |
| 预览图性质 | 高保真效果图 |
| 切图 | 部分已有（`Assets/Resources/icon/`、Sweet Land GUI）；缺图处用纯色 Image |
| 文字组件 | `TextMeshProUGUI`；前置任务：用项目字体生成中文 TMP 字体资产 |
| 字体 | 荆南麦圆体 `KeinannMaruPOP`，文件 `Assets/Resources/Fonts/KeinannMaruPOP.ttf`（已导入；授权待核验） |

与通用自适应规则的差异（用户已确认）：

- 窗口背景透明，岛屿以外点击穿透到桌面。**不放全屏背景**；岛屿是中心锚点、固定尺寸的主视觉。
- 桌面窗口没有刘海和圆角，`Screen.safeArea` 恒等于整个窗口。安全区组件保留，桌面端计算结果为 0。

## 二、组件边界

只用以下原生组件：

- 画布：`Canvas`、`CanvasScaler`、`GraphicRaycaster`
- 布局：`RectTransform`、`HorizontalLayoutGroup`、`VerticalLayoutGroup`、`GridLayoutGroup`、`ContentSizeFitter`、`LayoutElement`
- 显示：`Image`、`RawImage`、`TextMeshProUGUI`
- 交互：`Button`、`Toggle`、`Slider`、`TMP_InputField`、`ScrollRect`、`RectMask2D`（需要按图形裁切时用 `Mask`）
- 项目脚本只有一个：`SafeAreaFitter`

## 三、Canvas

- 启动场景里保存一个 `UiRoot` 物体，是场景中唯一的 Canvas。界面预制自身不带 Canvas。
- `Canvas`：Render Mode = `Screen Space - Overlay`，Pixel Perfect 关闭，Sort Order = 0。
- `CanvasScaler`：UI Scale Mode = `Scale With Screen Size`，Reference Resolution = `800×800`，Screen Match Mode = `Match Width Or Height`，Match = `0.5`，Reference Pixels Per Unit = `100`。
- `GraphicRaycaster`：默认参数。
- 运行时不修改 `matchWidthOrHeight`。
- `UiRoot` 下固定两个子节点，全部铺满父节点：

```text
UiRoot              Canvas + CanvasScaler + GraphicRaycaster
├─ pageLayer        RectTransform，全屏页的父节点
└─ popupLayer       RectTransform，弹窗的父节点
```

## 四、界面预制的根结构

全屏页预制：

```text
XxxPage                     RectTransform 铺满 + 页面脚本
├─ background               Image 铺满（可选；桌面窗口不放，岛屿作为主视觉放 contentArea 内）
└─ safeArea                 RectTransform 铺满 + SafeAreaFitter
   ├─ topBar                Image + HorizontalLayoutGroup，顶栏锚点
   ├─ contentArea           RectTransform，夹在顶栏和底栏之间
   └─ bottomBar             Image + HorizontalLayoutGroup，底栏锚点
```

弹窗预制：

```text
XxxPopup                    RectTransform 铺满 + 弹窗脚本
├─ mask                     Image 铺满，#000000 alpha 0.35，raycastTarget = true
└─ panel                    Image（Sliced），中心锚点，sizeDelta = 设计宽高
   ├─ titleText             TextMeshProUGUI
   ├─ closeButton           Button + Image
   ├─ bodyArea              RectTransform，放弹窗内容
   └─ actionBar             HorizontalLayoutGroup，放确认/取消按钮
```

## 五、打开、关闭与层级

- 打开者持有预制引用（`[SerializeField]`）和实例字段。第一次打开时 `Instantiate(prefab, layer)`，以后只 `SetActive(true)`。
- 关闭只 `SetActive(false)`，不销毁，不重复加载资源。
- 全屏页放 `pageLayer`，同一时间只显示一个；切页用 `SetActive`。
- 弹窗放 `popupLayer`，打开时调用 `transform.SetAsLastSibling()`。最后打开的弹窗在最上层，接收点击。
- `mask` 拦截下层所有点击。默认点遮罩**不关闭**；纯信息弹窗可在 `mask` 上加 `Button` 并绑定关闭。
- 桌面窗口下遮罩覆盖整个 `800×800` 窗口，弹窗打开期间整个窗口都不穿透点击。

## 六、安全区

- `SafeAreaFitter` 挂在全屏页的 `safeArea` 节点上；锚点固定为 `(0,0)`—`(1,1)`。
- 计算方式：`scale = canvas.scaleFactor`；
  - `offsetMin = (safeArea.xMin, safeArea.yMin) / scale`
  - `offsetMax = -(Screen.width - safeArea.xMax, Screen.height - safeArea.yMax) / scale`
- 只在 `Screen.safeArea` 或屏幕尺寸变化时重新计算。
- 背景、遮罩放在 `safeArea` 外，不受影响；顶栏、底栏、按钮和文字放在 `safeArea` 内。

## 七、列表

- 数量固定且不超过一屏：在预制里直接摆放，父节点用 Horizontal/Vertical/GridLayoutGroup。
- 数量会变或超出一屏：用 `ScrollRect`，结构固定为：

```text
xxxScroll        ScrollRect（movementType = Clamped）
└─ Viewport      Image 铺满 + RectMask2D
   └─ Content    LayoutGroup + ContentSizeFitter，条目的父节点
```

- 纵向列表的 `Content`：锚点 `(0,1)`—`(1,1)`，pivot `(0.5,1)`，`ContentSizeFitter.verticalFit = PreferredSize`。横向列表锚点放在起始端，pivot `(0,0.5)`。
- 条目是单独预制 `XxxItem.prefab`，尺寸为条目自身设计尺寸。界面预制里 `Content` 下保持为空。
- 刷新：复用 `Content` 下已有条目；不够时 `Instantiate(itemPrefab, content)`；多余条目 `SetActive(false)`。不建对象池和列表框架。
- 超过约 200 条需要虚拟滚动时，另立文档再定（待办）。

## 八、按钮反馈与节点访问

- 统一用 `Button` 的 `Color Tint`，参数：

| 状态 | 颜色 |
| --- | --- |
| Normal | `(1, 1, 1, 1)` |
| Highlighted | `(0.96, 0.96, 0.96, 1)` |
| Pressed | `(0.78, 0.78, 0.78, 1)` |
| Selected | `(1, 1, 1, 1)` |
| Disabled | `(0.78, 0.78, 0.78, 0.5)` |
| Fade Duration | `0.1` |
| Navigation | `None` |

- 界面脚本挂在预制根节点上，用 `[SerializeField]` 字段保存引用，引用直接写在预制文件里。
- 禁止用 `Find`、`GetChild`、路径字符串查找节点；不生成绑定代码。
- 纯装饰节点不需要脚本字段。

## 九、不负责的事

本框架不提供，也不准备扩展为：界面管理器、界面注册表、红点系统、多语言框架、资源加载框架、对象池、动画框架、自定义按钮或滚动列表、自动绑定代码生成、PSD 导出流程。需要时另立系统文档讨论。

## 十、迁移待办

现有 `MainUI.prefab` 和 `GameSettings.prefab` 按旧方案制作，与本框架的差异如下，直接修改预制文件逐项迁移：

- [x] 导入项目字体 `KeinannMaruPOP.ttf`；现有 uGUI `Text` 已全部改用该字体，运行时不再取系统微软雅黑（缺失时才回退并报错）。
- [x] 用 `KeinannMaruPOP.ttf` 生成动态 SDF TMP 字体资产 `Assets/Resources/Fonts/KeinannMaruPOP SDF.asset`，并将主界面、设置弹窗、背包项、盲盒品质项及运行时动态文本迁移为 `TextMeshProUGUI`。
- [ ] 核验荆南麦圆体的商用与再分发授权，保存作者授权声明并记入授权清单。
- [ ] Canvas 从 `MainUI.prefab` 根节点移到场景 `UiRoot`；CanvasScaler 改为 `800×800`、match `0.5`。
- [ ] 移除 `ResponsiveMainUi` 在运行时修改 match 和锚点的逻辑。
- [ ] `SafeAreaFitter` 改为按第六节修改 offset。
- [ ] 节点名改为英文驼峰（例如 `开启盲盒` → `openBoxButton`），同步更新引用。
- [ ] 设置弹窗补齐 `mask` / `panel` 结构。
