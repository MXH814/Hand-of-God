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

## Git Workflow

项目使用 `main` 作为主分支，功能按小步提交管理。远端仓库计划为 `https://github.com/MXH814/hci-gesture-game`。
