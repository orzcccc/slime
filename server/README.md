# 史莱姆盲盒后端

## 本地启动

1. 安装 Node.js 18 或更高版本。
2. 复制 `.env.example` 为 `.env`。
3. 执行 `npm install`。
4. 执行 `npm run dev`。

服务默认监听 `http://localhost:3000`。

Windows 下可直接双击项目根目录的 `启动本地后端.bat`。也可以执行 `server/start-local.ps1` 启动本地后端。

## 本地开发登录

仅在非生产环境且 `ALLOW_DEV_AUTH=true` 时，可向 `/api/auth/login` 提交：

```json
{ "steamTicket": "dev:tester" }
```

正式环境必须配置 `STEAM_APP_ID` 和 `STEAM_WEB_API_KEY`，并提交 Unity 从 Steam 获取的真实 Ticket。

## 当前 API

- `POST /api/auth/login`：开发账号登录并获取 JWT。
- `GET /api/game/state`：结算离线时间并读取盲盒、容量、残渣、CP 和下一次产出时间。
- `POST /api/game/open-box`：消耗一个盲盒并由服务端抽取史莱姆。
- `POST /api/game/claim-boxes`：将挂机区全部待领取盲盒随机品质后转入无上限仓库。
- `POST /api/game/merge`：恰好消耗五只史莱姆进行融合。
- `GET /api/user/inventory`：读取库存。
- `GET /api/social/leaderboard`：读取前 50 名排行。
- `GET /api/user/dictionary`：读取完整图鉴与发现进度。
- `/api/guild/*`：个人公会、捐献、科技升级与每周 Boss。
- `POST /api/dev/gm/boxes`：仅开发环境可用的盲盒 GM 操作。

## Unity 联调

1. 启动本地后端。
2. 用 Unity 2021.3 打开项目并进入 `Assets/Scenes/SampleScene.unity`。
3. 点击 Play。运行时启动器会自动创建客户端、使用 `dev:local-player` 登录并生成 uGUI 原型界面。
4. 新账号初始拥有一个盲盒，可直接执行开箱；同种史莱姆达到五只后可点击融合。
5. “培养皿/GM”页可直接增加、填满、清空盲盒或推进三小时；该接口在生产环境不存在。

界面为运行时生成的占位原型，不依赖正式美术。后端未运行时，界面会显示连接错误。

## 测试

```bash
npm test
```
