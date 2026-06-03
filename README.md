# HCI Gesture Game

用户交互技术 HCI 期末项目的手势识别基础工程。当前版本先实现摄像头中的手部关节点、手指状态、手掌方向、捏合状态和运动向量识别，为后续游戏玩法接入做准备。

## Tech Stack

- Vite + TypeScript
- Google MediaPipe Tasks Vision Hand Landmarker
- Browser camera via `getUserMedia`

## Development

```powershell
npm install
npm run dev
```

打开 Vite 输出的本地地址后，允许浏览器摄像头权限即可开始识别。

如果 Windows 环境里 `vite` shim 被权限拦截，当前 `npm run dev` 已改为显式调用 `node ./node_modules/vite/bin/vite.js`，不需要额外处理。

## Debug Panel

第一版只实现基础手势识别调试能力：

- 摄像头画面叠加 MediaPipe Hands 21 个关节点和骨架连接。
- 最多同时识别两只手，并显示左右手标签与置信度。
- 显示拇指食指捏合状态、归一化捏合距离、手掌朝向、手部运动向量和伸展手指列表。
- 支持镜像画面和一阶低通平滑强度调节。

## Verification

```powershell
npm run build
```

手动演示时建议依次验证：

- 单手进入画面后出现 21 点骨架。
- 双手进入画面后出现两组数据。
- 拇指和食指靠近时 `Pinch` 从 `open` 变为 `active`。
- 翻转手掌时 `Palm` 在 `camera`、`away`、`side` 之间变化。
- 移动手掌时 `Motion` 数值变化，调高 `Smoothing` 后抖动降低。

## Git Workflow

项目使用 `main` 作为主分支，功能按小步提交管理。远端仓库计划为 `https://github.com/MXH814/hci-gesture-game`。

推荐提交节奏：

1. `chore: initialize project`
2. `feat: add mediapipe hand tracking`
3. `feat: add gesture debug panel`
4. `docs: add usage and project notes`
