# HCI Gesture Game

用户交互技术 HCI 期末项目。当前项目实现了基于摄像头的手部关键点识别、手势事件底座，以及一个用于后续游戏玩法验证的 3D 几何体交互沙盒。

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

打开 Vite 输出的本地地址后，点击 `Start`，允许浏览器摄像头权限即可开始识别。

也可以直接双击 `start-project.bat` 启动项目。脚本会自动进入项目目录，缺少依赖时先执行 `npm install`，然后启动本地服务并打开 `http://127.0.0.1:5173/`。

## Implemented Features

### 摄像头启动与本地模型加载

- 功能：启动/停止摄像头，启动后加载手部识别模型并进入 `Tracking` 状态。
- 技术：`navigator.mediaDevices.getUserMedia`、MediaPipe Tasks Vision wasm。
- 算法/规则：先请求摄像头，再加载本地模型；模型加载有超时保护，避免外网资源阻塞。
- 相关文件：`src/handTracker.ts`、`public/vendor/mediapipe/`
- 参考：Google MediaPipe Tasks Vision Hand Landmarker, https://ai.google.dev/edge/mediapipe/solutions/vision/hand_landmarker

### 21 点手部关键点与骨架叠加

- 功能：摄像头画面上叠加 MediaPipe Hands 21 个关节点和骨架连接。
- 技术：MediaPipe `HandLandmarker.detectForVideo`、HTML Canvas 2D。
- 算法/规则：每帧读取 `landmarks`，按 `HandLandmarker.HAND_CONNECTIONS` 绘制骨架；拇指和食指指尖使用高亮色。
- 相关文件：`src/main.ts`
- 参考：Google MediaPipe Tasks Vision Hand Landmarker, https://github.com/google-ai-edge/mediapipe

### 基础手势分析

- 功能：识别左右手、手指伸展状态、捏合状态、捏合距离、掌心朝向和手部运动向量。
- 技术：MediaPipe normalized landmarks、TypeScript 几何计算。
- 算法/规则：拇指食指距离除以掌宽得到归一化捏合距离；指尖到手腕距离判断伸展；掌根、食指根、小指根叉乘估计掌心法向；相邻帧掌心中心差值得到运动向量。
- 相关文件：`src/gestureAnalyzer.ts`、`src/types.ts`
- 参考：自研规则，基于 MediaPipe landmark 数据。

### 手势事件系统

- 功能：把连续帧状态转换成 `pinchStart`、`pinchMove`、`pinchEnd`、`twoHandTransformStart`、`twoHandTransformMove`、`twoHandTransformEnd`。
- 技术：TypeScript 状态机。
- 算法/规则：使用持续帧阈值、防抖和置信度评分；Pinch 置信度由捏合距离和检测置信度共同决定；双手变换置信度由两只手检测分数平均得到。
- 相关文件：`src/gestureEventEngine.ts`、`src/types.ts`
- 参考：自研规则。

### 坐标映射

- 功能：把摄像头归一化手部坐标映射到页面坐标和 3D 舞台坐标。
- 技术：DOMRect、归一化坐标转换。
- 算法/规则：镜像模式下水平坐标取 `1 - x`；页面坐标按目标区域宽高缩放；3D 舞台坐标映射到固定的世界坐标范围。
- 相关文件：`src/interactionMapper.ts`
- 参考：自研规则。

### 3D 几何体交互沙盒

- 功能：右侧菜单提供 Cube、Sphere、Cylinder、Cone、Torus；可用单手捏合拖出几何体，也可点击菜单生成；生成后双手控制位置、旋转和缩放。
- 技术：Three.js scene、camera、lights、geometry、raycaster。
- 算法/规则：单手 Pinch 命中菜单项后进入拖拽预览，松手且位于 3D 舞台内时生成几何体；双手中心控制对象位置，双手距离变化控制缩放，双手连线角度变化控制旋转。
- 相关文件：`src/shapeScene.ts`、`src/shapeLibrary.ts`、`src/main.ts`
- 参考：Three.js, https://github.com/mrdoob/three.js

### 调试 HUD

- 功能：显示 FPS、识别到的手数量、每只手的 Pinch/Palm/Motion/Fingers、当前手势事件、事件置信度和 3D 对象数量。
- 技术：原生 DOM 渲染、Lucide 图标。
- 算法/规则：每帧刷新识别状态；右侧列表固定高度并内部滚动，避免影响摄像头窗口尺寸。
- 相关文件：`src/main.ts`、`src/styles.css`
- 参考：Lucide icons, https://github.com/lucide-icons/lucide

### 稳定摄像头视口

- 功能：无论手势数据、右侧卡片或几何体状态如何变化，摄像头窗口保持固定 16:9，不被页面内容撑大或缩小。
- 技术：CSS Grid、`aspect-ratio`、`object-fit: contain`。
- 算法/规则：摄像头窗口独立固定比例；右侧动态内容使用滚动区域消化高度变化。
- 相关文件：`src/styles.css`
- 参考：自研布局规则。

### 本地资料忽略与快捷启动

- 功能：`Doc.pdf`、`1.png`、`课件/` 保留本地但不推送到 GitHub；双击 `start-project.bat` 可启动项目。
- 技术：`.gitignore`、Windows batch、npm scripts。
- 算法/规则：启动脚本检测依赖目录，不存在时先安装依赖，再启动 Vite 并打开本地地址。
- 相关文件：`.gitignore`、`start-project.bat`、`package.json`
- 参考：自研工程脚本。

## Notes

如果浏览器页面在张开手掌时出现缩放，那不是本项目代码实现的功能。本项目没有监听手势去修改页面缩放、浏览器缩放或 CSS scale；这种情况更可能来自系统、浏览器、触控板或扩展的缩放手势。

当前几何体交互是调试沙盒，不包含完整游戏关卡、物理碰撞、重力、小球玩法或复杂关卡逻辑。

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
- 右侧五类几何体可点击生成，也可用单手 Pinch 拖出。
- 双手进入画面后可控制选中几何体的位置、缩放和旋转。
- 右侧面板内容变化不影响摄像头窗口尺寸。

## Git Workflow

项目使用 `main` 作为主分支，功能按小步提交管理。远端仓库为 `https://github.com/MXH814/hci-gesture-game`。

本地资料文件不会推送到 GitHub：

- `Doc.pdf`
- `1.png`
- `课件/`

以后每次新增功能，都必须同步更新 README 的 `Implemented Features`，写明功能说明、实现技术、算法/规则、相关文件和参考来源。
