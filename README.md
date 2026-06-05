# HCI Gesture Game

用户交互技术 HCI 期末项目。当前游戏名推荐并继续使用 **Hand of God**：玩家通过摄像头手势像“神之手”一样改变关卡机关，间接引导小球到达终点。

## Tech Stack

- Vite + TypeScript
- Google MediaPipe Tasks Vision Hand Landmarker
- Three.js
- cannon-es physics
- Lucide icons
- Browser camera via `getUserMedia`
- Local MediaPipe model and wasm assets under `public/vendor/mediapipe`

## Development

```powershell
npm install
npm run dev
```

打开 Vite 输出的本地地址后，点击 `Start`，允许浏览器摄像头权限。也可以双击 `start-project.bat` 启动项目；脚本会自动进入项目目录，缺少依赖时先执行 `npm install`，然后启动本地服务并打开 `http://127.0.0.1:5173/`。

## Implemented Features

### 摄像头启动与本地模型加载

- 功能：启动/停止摄像头，加载本地手部识别模型，进入实时识别循环。
- 技术：`navigator.mediaDevices.getUserMedia`、MediaPipe Tasks Vision wasm、本地 `.task` 模型。
- 规则：先请求摄像头，再加载本地模型；模型加载带超时保护，避免外网资源阻塞。
- 相关文件：`src/handTracker.ts`、`public/vendor/mediapipe/`
- 参考：Google MediaPipe Tasks Vision Hand Landmarker, https://ai.google.dev/edge/mediapipe/solutions/vision/hand_landmarker

### 基础手势分析与事件系统

- 功能：识别左右手、手指伸展、捏合状态、掌心朝向、运动向量，并输出 `pinchStart`、`pinchMove`、`pinchEnd`、双手变换等事件。
- 技术：MediaPipe 21 点 landmarks、TypeScript 几何计算、事件状态机。
- 规则：捏合距离按手掌尺度归一化，并使用校准后的阈值、连续帧、防抖和置信度评分降低误触。
- 相关文件：`src/gestureAnalyzer.ts`、`src/gestureEventEngine.ts`、`src/types.ts`
- 参考：自研规则，基于 MediaPipe landmark 数据。

### 手势校准流程

- 功能：启动摄像头后先完成“张开手掌”和“捏合”校准，也支持 `Skip` 使用默认阈值。
- 技术：TypeScript 状态机、手部 landmark 采样、HUD 进度条。
- 规则：游戏必须在摄像头开启并完成/跳过校准后才开始；未校准时物理主流程等待。
- 相关文件：`src/calibrationManager.ts`、`src/main.ts`
- 参考：自研规则。

### 真实 3D 机关神殿地图

- 功能：将旧的伪 2.5D 平面关卡重构为真实 3D 俯视地图 `Level 01: Temple of the First Hand`。场景包含浮空石质地基、起点广场、分段石桥、墙体、护栏、柱廊、发光符文、终点祭坛和危险掉落区。
- 技术：Three.js 模块化 mesh 生成、cannon-es 静态碰撞体、固定俯视偏斜正交相机、阴影与多光源。
- 规则：使用 `Y` 轴向上，重力为 `(0, -9.8, 0)`；小球在 `X/Z` 地面平面滚动，`Y` 是高度，不再锁定旧的屏幕平面。
- 相关文件：`src/gameLevel.ts`、`src/shapeScene.ts`
- 参考：Three.js, https://github.com/mrdoob/three.js；cannon-es, https://github.com/pmndrs/cannon-es

### 间接机关控制：可拖动倾斜坡道

- 功能：玩家不能再用手直接碰撞小球；主玩法改为捏住坡道的青色控制点并左右拖动，控制神殿坡道倾角，让小球受真实重力间接滚动。
- 技术：MediaPipe 捏合点映射、屏幕拖动估计、Three.js 机关高亮、cannon-es kinematic ramp collider。
- 规则：可交互机关使用青色发光控制区常亮标记；每帧读取当前捏合点并连续驱动机关。控制规则固定为“向右拖动让坡道右端下降，向左拖动让坡道左端下降”，不再混用手腕 roll，避免旋转方向难以理解。当前关卡包含中间桥和终点入口两个可控坡道。
- 相关文件：`src/main.ts`、`src/shapeScene.ts`、`src/interactionMapper.ts`
- 参考：自研规则，基于 landmark-driven control 思路。

### 摄像头显示区域对齐

- 功能：修正摄像头画面、骨架叠加、捏合点光标和 3D 交互命中之间的区域偏移。
- 技术：按 `object-fit: contain` 后的视频真实显示矩形做坐标映射，并在舞台上绘制 `HAND/PINCH` 交互光标。
- 规则：如果摄像头比例和 16:9 舞台不一致，映射会自动扣除上下或左右留白，避免捏合点落在视觉手部之外。
- 相关文件：`src/main.ts`、`src/styles.css`
- 参考：自研布局与坐标映射规则。

### 调试几何体原型工具

- 功能：保留 Cube、Sphere、Cylinder、Cone、Torus 的调试托盘，用于后续关卡道具原型，但不作为主线玩法。
- 技术：Three.js mesh、raycaster、手势捏取移动/旋转/缩放、双食指交叉删除。
- 规则：托盘在主舞台右上角弱化显示，避免遮挡神殿地图；主游戏中手势优先命中机关。
- 相关文件：`src/main.ts`、`src/shapeScene.ts`、`src/shapeLibrary.ts`
- 参考：Three.js；Codrops Webcam 3D HandControls, https://tympanus.net/Tutorials/webcam-3D-handcontrols/

## Design Direction

当前阶段继续使用网页技术栈，而不是迁移 Unity 或虚幻。原因是 MediaPipe、摄像头权限、手势校准、Three.js 渲染和 cannon-es 物理已经在浏览器里打通，适合期末项目演示和快速迭代。

网页模块化美术可以完成课程展示级别的真实 3D 俯视地图，但它仍不是专业模型资产管线。如果后续视觉质量仍无法达到预期，应转向 glTF/Blender 模型资产，或再评估 Unity/虚幻。

## Verification

```powershell
npm run build
```

手动演示建议依次验证：

- 首屏能看到完整 3D 机关神殿地图，而不是旧的平面几何堆叠。
- 未开启摄像头或未完成校准时，`Game` 面板显示 `Waiting for calibration`。
- 点击 `Start` 后进入校准；完成或点击 `Skip` 后游戏才开始。
- 小球在 X/Z 地面上滚动，Y 轴为高度，掉出地图后自动重置。
- 手不能直接推动小球。
- 地图上能看到青色发光控制区，表示可以交互的机关位置。
- 摄像头开启后，舞台上能看到 `HAND/PINCH` 光标，位置应贴近视频中的捏合点。
- 捏住坡道的发光控制区或手柄并左右拖动，坡道倾角改变；向右拖动让坡道右端下降，向左拖动让左端下降。
- 小球进入终点祭坛并减速后，`Game` 面板显示 `Goal reached`。
- 浏览器控制台无 runtime error。

## Git Workflow

项目使用 `main` 作为主分支，远端仓库为 `https://github.com/MXH814/hci-gesture-game`。

本地资料文件不推送到 GitHub，例如：

- `Doc.pdf`
- `1.png`
- `课件/`
- 本地 `.docx` 资料

以后每次新增功能，都必须同步更新 README 的 `Implemented Features` 或相关验证说明。
