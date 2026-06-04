# HCI Gesture Game

用户交互技术 HCI 期末项目。当前版本实现了浏览器摄像头手部识别、手势事件底座、校准流程，以及 AR 叠加式几何体交互原型：摄像头画面是唯一主舞台，手部骨架、3D 几何体和底部几何体托盘都叠加在同一个画面坐标系中。

## Tech Stack

- Vite + TypeScript
- Google MediaPipe Tasks Vision Hand Landmarker
- Three.js
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
- 算法/规则：先请求摄像头，再加载本地模型；模型加载设置超时保护，避免外网资源阻塞。
- 相关文件：`src/handTracker.ts`、`public/vendor/mediapipe/`
- 参考：Google MediaPipe Tasks Vision Hand Landmarker, https://ai.google.dev/edge/mediapipe/solutions/vision/hand_landmarker

### 21 点手部关节点与骨架叠加

- 功能：在摄像头画面上叠加 MediaPipe Hands 21 个关节点和骨架连接。
- 技术：MediaPipe `HandLandmarker.detectForVideo`、HTML Canvas 2D。
- 算法/规则：每帧读取 `landmarks`，按 `HandLandmarker.HAND_CONNECTIONS` 绘制骨架；拇指和食指指尖使用高亮色，方便观察 Pinch。
- 相关文件：`src/main.ts`
- 参考：Google MediaPipe, https://github.com/google-ai-edge/mediapipe

### 基础手势分析

- 功能：识别左右手、手指伸展状态、捏合状态、捏合距离、掌心朝向和手部运动向量。
- 技术：MediaPipe normalized landmarks、TypeScript 几何计算。
- 算法/规则：拇指食指距离除以掌宽得到归一化捏合距离；指尖到手腕距离判断伸展；掌根、食指根、小指根叉乘估计掌心法向；相邻帧掌心中心差值得到运动向量，并用一阶低通平滑降低抖动。
- 相关文件：`src/gestureAnalyzer.ts`、`src/types.ts`
- 参考：自研规则，基于 MediaPipe landmark 数据。

### 手势校准流程

- 功能：启动后显示校准提示，也可随时点击 `Calibrate` 重新校准；支持 `Skip` 使用默认阈值。
- 技术：TypeScript 状态机、手部 landmark 采样、HUD 进度条。
- 算法/规则：校准阶段依次要求“张开手掌保持 1 秒”和“拇指食指捏合保持 1 秒”；采集掌宽和个人捏合距离，生成 `pinchThreshold`，后续 Pinch 识别和置信度评分使用该阈值。
- 相关文件：`src/calibrationManager.ts`、`src/gestureAnalyzer.ts`、`src/gestureEventEngine.ts`、`src/main.ts`
- 参考：自研规则。

### 手势事件系统

- 功能：把连续帧状态转换成 `pinchStart`、`pinchMove`、`pinchEnd`、`twoHandTransformStart`、`twoHandTransformMove`、`twoHandTransformEnd`。
- 技术：TypeScript 状态机。
- 算法/规则：使用持续帧阈值、防抖和置信度评分；Pinch 置信度由校准后的捏合阈值、当前捏合距离和检测分数共同决定；双手变换只在两只手都处于捏合状态时启动，避免张手或路过画面时误触。
- 相关文件：`src/gestureEventEngine.ts`、`src/types.ts`
- 参考：自研规则；参考 landmark-driven 控制思路：Codrops Webcam 3D HandControls, https://tympanus.net/Tutorials/webcam-3D-handcontrols/

### AR 统一坐标映射

- 功能：把摄像头归一化手部坐标映射到同一个 `ar-stage` 页面坐标系，用于命中底部托盘、拖拽几何体和控制 3D 对象。
- 技术：`DOMRect`、镜像坐标转换、Three.js orthographic camera。
- 算法/规则：交互点使用 MediaPipe 21 点中的拇指尖 4 和食指尖 8 的中点，而不是手掌中心；镜像模式下水平坐标取 `1 - x`；手势点按 `ar-stage` 宽高映射到屏幕坐标；Three.js 使用透明正交相机把屏幕点转换到摄像头画面平面。
- 相关文件：`src/interactionMapper.ts`、`src/shapeScene.ts`、`src/main.ts`
- 参考：自研规则；参考开源项目 Colliding Scopes threejs-handtracking-101, https://github.com/collidingScopes/threejs-handtracking-101

### AR 叠加式几何体交互

- 功能：摄像头画面作为底层，3D 几何体直接叠加显示在画面上；底部托盘提供 Cube、Sphere、Cylinder、Cone、Torus。
- 技术：Three.js transparent WebGL renderer、orthographic camera、geometry、raycaster。
- 算法/规则：单手 Pinch 命中底部托盘按钮后必须保持 1 秒，按钮进度条填满并高亮后才进入 armed 状态；保持捏合并离开托盘后出现拖拽预览，松开后在手部所在画面位置生成几何体；Pinch 已生成对象可选中并移动；双手捏合时用两只手的捏合点中心控制位置、两点距离控制绝对缩放、两点角度控制绝对旋转，减少逐帧命令叠加带来的僵硬和漂移。
- 相关文件：`src/shapeScene.ts`、`src/shapeLibrary.ts`、`src/main.ts`
- 参考：Three.js, https://github.com/mrdoob/three.js；Codrops Creating a 3D Hand Controller, https://tympanus.net/codrops/2024/10/24/creating-a-3d-hand-controller-using-a-webcam-with-mediapipe-and-three-js/

### 调试 HUD

- 功能：显示 FPS、识别到的手数量、对象数量、当前交互模式、手势事件、Pinch/Palm/Motion/Fingers 等调试信息。
- 技术：原生 DOM 渲染、Lucide 图标、CSS Grid。
- 算法/规则：每帧刷新识别状态；动态内容放在右侧紧凑调试面板中，不再影响摄像头主舞台尺寸。
- 相关文件：`src/main.ts`、`src/styles.css`
- 参考：Lucide icons, https://github.com/lucide-icons/lucide

### 稳定放大的摄像头主舞台

- 功能：摄像头区域放大为页面主舞台，保持 16:9；几何体、托盘和校准提示都叠加在舞台内部，不再分屏。
- 技术：CSS Grid、`aspect-ratio`、absolute overlay、`object-fit: contain`。
- 算法/规则：`ar-stage` 独立固定比例；右侧动态调试面板滚动显示，避免内容变化挤压摄像头窗口。
- 相关文件：`src/styles.css`、`src/main.ts`
- 参考：自研布局规则。

### 本地资料忽略与快捷启动

- 功能：`Doc.pdf`、`1.png`、`课件/` 保留本地但不推送到 GitHub；双击 `start-project.bat` 可启动项目。
- 技术：`.gitignore`、Windows batch、npm scripts。
- 算法/规则：启动脚本检测依赖目录，不存在时先安装依赖，再启动 Vite 并打开本地地址。
- 相关文件：`.gitignore`、`start-project.bat`、`package.json`
- 参考：自研工程脚本。

## Notes

当前 AR 几何体是屏幕平面叠加原型，不包含真实深度遮挡、真实世界平面检测、物理碰撞、重力、小球玩法或完整游戏关卡。

如果浏览器页面在张开手掌时出现缩放，那不是本项目代码实现的功能。本项目没有监听手势去修改页面缩放、浏览器缩放或 CSS scale；这类情况更可能来自系统、浏览器、触控板或扩展的缩放手势。

## Verification

```powershell
npm run build
```

手动演示建议依次验证：

- 点击 `Start` 后进入 `Calibrating: open hand`，按提示张手和捏合，完成后进入 `Ready`。
- 点击 `Skip` 后使用默认阈值也能进入交互。
- 摄像头启动后，中间的 `Click Start to enable camera` 提示消失。
- 单手进入画面后出现 21 点骨架。
- 拇指和食指靠近时 `Pinch` 从 `open` 变为 `active`。
- 底部五类几何体可点击生成；也可用单手 Pinch 在某个托盘按钮上保持 1 秒，按钮高亮后继续捏住并移出托盘，松开生成对应几何体。
- 几何体直接显示在摄像头画面上，不出现独立 3D 分屏或坐标图。
- Pinch 已生成对象可选中并移动。
- 双手都捏合后可控制选中几何体的位置、缩放和旋转。
- 右侧面板内容变化不影响摄像头主舞台尺寸。

## Git Workflow

项目使用 `main` 作为主分支，功能按小步提交管理。远端仓库为 `https://github.com/MXH814/hci-gesture-game`。

本地资料文件不会推送到 GitHub：

- `Doc.pdf`
- `1.png`
- `课件/`

以后每次新增功能，都必须同步更新 README 的 `Implemented Features`，写明功能说明、实现技术、算法/规则、相关文件和参考来源。没有参考开源仓库的自研逻辑，只写“自研规则”，不编造来源。
