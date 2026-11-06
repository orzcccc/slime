---
name: preview-to-prefab
description: 《史莱姆盲盒》项目内工作流：用户发来 UI 预览图、效果图或线框图，要求切图、抠透明图、补全被遮挡的图块，或据此拼装/修改 Unity UI 预制（Prefab）时使用。提供拆图、AI 并行补全、切图入库、声明式拼预制、预制校验和离线编译检查脚本。
---

# 预览图 → 切图 → 预制

脚本在 `scripts/`，从项目根目录运行，只依赖 Pillow 和 NumPy。下文 `S` 指本 Skill 的 `scripts` 目录（项目内为 `.agents/skills/preview-to-prefab/scripts`）。中间产物统一放在 `.codex_tmp/slice/<page>/`（已被 git 忽略）。

项目规则照常适用，本 Skill 只提供更快的做法：`.cursor/rules/` 的三个规则（Prefab 直接改 YAML、文档同步、商业标准）、`Docs/Systems/预览图生成UI预制规则.md`（看图六项分析、锚点表、节点命名、失败清单）和 `AGENTS.md` 的提交约定。

## 流程

1. **读图规划。** `view_image` 看每张图：线框/功能图决定布局和交互，效果图决定视觉。按《预览图生成UI预制规则》写出六项分析，并列出切图清单：每个独立视觉物件一张图；按钮拆成底板 + 图标；图里的文字一律擦掉，改用 TMP 节点。
   运行 `python S/slice_preview.py candidates --src <图> --out .codex_tmp/slice/<page>`，看 `candidates.png`（50px 坐标网格 + 编号区域及其种子点）读出各部件的框和种子点。

2. **拆图。** 按 [references/slice-spec.md](references/slice-spec.md) 写 `.codex_tmp/slice/<page>/spec.json`，运行 `python S/slice_preview.py run --spec <spec>`（约 10–20 秒，部件并行处理）。看 `recompose.png`（左原图、右切图拼回）和 `sheet.png`。不对就改配置重跑，不要手工改像素。

3. **AI 补全。** 被遮挡面积大、且补出来的内容会被玩家看到的部件（场景底图、道具背后的底座等），在配置里写 `"ai": "<要补成什么>"`，然后一次性并行补全：
   `python S/ai_fill.py --slice-dir .codex_tmp/slice/<page> --workers 4`
   每个任务约 1 分钟，所有任务同时进行。只有遮罩内的像素会被替换，可见部分保持原样。看 `ai/<part>_compare.png`，个别不理想就 `--parts <名字>` 单独重跑。
   - 需要已安装并启用的 `aigw-imagegen` Skill，脚本会先探测环境；不可用时保留算法补全结果，并在交付中说明。
   - 被其他 UI 盖住、玩家看不到的补全区（如按钮背后的页签底）用算法补全即可。
   - 重新执行第 2 步会覆盖 AI 结果，所以先把拆图定稿再跑 AI。等待 AI 时可以并行写第 5 步的拼装配置。

4. **入库。** `python S/install_sprites.py --slice-dir .codex_tmp/slice/<page> --dest Assets/Resources/Sprites/<Page>`。已有 `.meta` 保留 GUID，引用它的预制不会断。输出 `sprites.json`（每张图在 800×800 下的尺寸和中心坐标）和一张 Markdown 表，表格直接贴进切图资源文档。需要按 Alpha 命中的图（如岛屿）在配置里设 `"readable": true`。

5. **拼预制。** 先看现有预制和引用组件：`python S/prefab_check.py <prefab> --tree`。按 [references/prefab-spec.md](references/prefab-spec.md) 写拼装配置，运行 `python S/prefab_build.py --spec <build.json>`。同名节点就地更新（保留 fileID），新节点自动生成；引用组件字段用 `refs` 填写。大规模重组旧预制（移动、删除、改名）时，在一个小 Python 脚本里直接用 `S/prefab_lib.py` 的 `Prefab` 类。
   预览图里没有、但界面需要的图（通用按钮底板、角标等）可用 `aigw-imagegen` 并行生成，在文档中标注“占位”。

6. **校验。**
   - `python S/prefab_check.py <所有改过的预制>` 必须 0 问题（引用、父子关系、RootOrder、外部 GUID）。
   - `python S/unity_compile_check.py` 按 Unity 的程序集划分离线编译，必须 0 错误且没有新警告（需要 Unity 至少编译过一次项目）。
   - Unity 内的实际运行由用户完成，交付时写明未验证项。

7. **文档与提交。** 更新 `Docs/Systems/` 下对应页面文档和切图资源文档。用 `python S/git_commit.py --message <msg.txt> <路径...>` 只提交本次涉及的路径；工作区里可能有别人未提交的改动。

## 限制

- 自动拆图要求预览图背景是纯色（效果图常见）；渐变或照片背景只能用 `box` 模式框选后交给 AI 处理。
- 抠图边缘会补 1px 描边色羽化，适合本项目带深色描边的卡通画风；描边颜色可在配置里用 `outline` 指定。
- 文字识别只做“擦除”，文案需要人工读图后写进 TMP 节点。
