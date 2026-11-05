# 协作约定

## Git 提交

- 每完成一项修改任务后，由助手自动提交并推送到 `origin main`（https://github.com/orzcccc/slime.git），无需再次询问。
- 提交前检查 `git status`，确认没有提交 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`server/node_modules/`、`server/.env`、`.codex_tmp/` 等不该入库的文件，也不提交密码或密钥。
- 提交信息用中文，简短说明改了什么，例如“背包界面：修复物品图标错位”。中文提交信息通过 `git commit -F <文件>` 传入，避免命令行编码问题。
- 一次提交只包含一件事；不相关的改动分开提交。
- 推送失败（例如认证失败或远程有新提交）时，不要强制推送（`--force`），先告诉用户原因。
