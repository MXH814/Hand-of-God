# HCI Gesture Game

用户交互技术 HCI 期末项目的手势识别基础工程。当前版本先实现摄像头中的手部关节点、手指状态、手掌方向、捏合状态和运动向量识别，为后续游戏玩法接入做准备。

## Tech Stack

- Vite + TypeScript
- Google MediaPipe Tasks Vision Hand Landmarker
- Browser camera via `getUserMedia`
- Local MediaPipe model and wasm assets under `public/vendor/mediapipe`

## Development

```powershell
npm install
npm run dev
```

打开 Vite 输出的本地地址后，点击 `Start`，允许浏览器摄像头权限即可开始识别。

也可以直接双击 `start-project.bat` 启动项目。脚本会自动进入项目目录，缺少依赖时先执行 `npm install`，然后启动本地服务并打开 `http://127.0.0.1:5173/`。

## Implemented Features

当前第一版只实现基础手势识别调试能力，不包含游戏玩法或手势驱动页面缩放。

- 摄像头启动/停止控制。
- 摄像头画面叠加 MediaPipe Hands 21 个关节点和骨架连接。
- 最多同时识别两只手，并显示左右手标签与置信度。
- 显示拇指食指捏合状态和归一化捏合距离。
- 显示手掌朝向：`camera`、`away`、`side`。
- 显示手部运动向量：`Motion x/y`。
- 显示伸展手指列表：`thumb`、`index`、`middle`、`ring`、`pinky`。
- 支持镜像画面开关。
- 支持一阶低通平滑强度调节，降低运动向量抖动。
- MediaPipe 模型和 wasm 已本地托管，避免启动时依赖外网 CDN。

如果浏览器页面在张开手掌时出现缩放，那不是本项目代码实现的功能。本项目没有监听手势去修改页面缩放、浏览器缩放或 CSS scale；这种情况更可能来自系统/浏览器/触控板/扩展的缩放手势。

## Verification

```powershell
npm run build
```

手动演示时建议依次验证：

- 点击 `Start` 后状态从 `Idle` 变为 `Tracking`。
- 摄像头画面启动后，中间的 `Click Start to enable camera` 提示消失。
- 单手进入画面后出现 21 点骨架。
- 双手进入画面后出现两组数据。
- 拇指和食指靠近时 `Pinch` 从 `open` 变为 `active`。
- 翻转手掌时 `Palm` 在 `camera`、`away`、`side` 之间变化。
- 移动手掌时 `Motion` 数值变化，调高 `Smoothing` 后抖动降低。

## Git Workflow

项目使用 `main` 作为主分支，功能按小步提交管理。远端仓库为 `https://github.com/MXH814/hci-gesture-game`。

本地资料文件不会推送到 GitHub：

- `Doc.pdf`
- `1.png`
- `课件/`

推荐提交节奏：

1. `chore: initialize project`
2. `feat: add mediapipe hand tracking`
3. `feat: add gesture debug panel`
4. `docs: add usage and project notes`
