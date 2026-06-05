# HCI Gesture Game

用户交互技术 HCI 期末项目。当前版本实现了浏览器摄像头手部识别、手势事件底座、校准流程、AR 叠加式几何体交互原型，以及 **Hand of God** 2.5D 物理小球关卡。当前推荐游戏名使用 **Hand of God**：名字直接呼应“用手像神一样干预世界”的核心玩法，比 `the hands` 更有记忆点，也更适合后续关卡叙事和视觉包装。

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
- 算法/规则：交互点使用 MediaPipe 21 点中的拇指尖 4 和食指尖 8 的中点，而不是手掌中心；同时记录捏合点 `z` 和掌部尺度 `handScale` 作为前后移动的深度代理；镜像模式下水平坐标取 `1 - x`；手势点按 `ar-stage` 宽高映射到屏幕坐标；Three.js 使用透明正交相机把屏幕点转换到摄像头画面平面。
- 相关文件：`src/interactionMapper.ts`、`src/shapeScene.ts`、`src/main.ts`
- 参考：自研规则；参考开源项目 Colliding Scopes threejs-handtracking-101, https://github.com/collidingScopes/threejs-handtracking-101

### AR 叠加式几何体交互

- 功能：摄像头画面作为底层，3D 几何体直接叠加显示在画面上；底部托盘提供 Cube、Sphere、Cylinder、Cone、Torus。
- 技术：Three.js transparent WebGL renderer、orthographic camera、geometry、raycaster。
- 算法/规则：单手 Pinch 命中底部托盘按钮后必须保持 1 秒，按钮进度条填满并高亮后才进入 armed 状态；保持捏合并离开托盘后出现拖拽预览，松开后在手部所在画面位置生成几何体；后创建的几何体分配更靠前的 z 层级，视觉和命中上都位于旧几何体前方；Pinch 已生成对象可选中并移动；双手捏合时用两只手的捏合点中心控制位置、两点屏幕角度控制 Z 轴旋转；两手平均 `handScale` 变化控制 X 轴前后倾斜，左右手 `handScale/z` 差异控制 Y 轴旋转；只有双手都处于食指和中指并拢状态时，两手距离变化才允许控制缩放，其他双手手势的 `scaleDelta` 固定为 1。对象变换基于抓取开始时的基准姿态做绝对映射，并使用角度平滑，减少逐帧命令叠加带来的僵硬和漂移。
- 相关文件：`src/shapeScene.ts`、`src/shapeLibrary.ts`、`src/main.ts`
- 参考：Three.js, https://github.com/mrdoob/three.js；Codrops Creating a 3D Hand Controller, https://tympanus.net/codrops/2024/10/24/creating-a-3d-hand-controller-using-a-webcam-with-mediapipe-and-three-js/

### 双食指交叉删除几何体

- 功能：双手食指交叉时，交叉点命中的最前方几何体会被删除；交叉点没有命中几何体时只显示调试提示。
- 技术：MediaPipe 21 点关键点、线段相交检测、Three.js raycaster。
- 算法/规则：每只手取食指根部 `5` 到食指尖 `8` 作为食指线段；两只手食指均伸展、两条线段在 `ar-stage` 屏幕坐标内相交且夹角大于阈值时，使用交叉点做 Three.js 命中测试；raycaster 返回该位置最前方对象；交叉点稳定命中同一对象约 850ms 后删除该对象，并设置 900ms 冷却避免一次交叉连续删除多个对象。
- 相关文件：`src/main.ts`、`src/shapeScene.ts`、`src/interactionMapper.ts`
- 参考：自研规则，基于 MediaPipe landmark 数据。

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

### 交互反馈与命中优化

- 功能：双食指交叉删除增加交叉点进度条，几何体菜单增加更大的手势命中范围，正在操作或即将删除的几何体会高光显示。
- 技术：DOM overlay 进度条、Three.js material emissive highlight、扩大后的托盘命中矩形、renderOrder/depth layering。
- 算法/规则：双食指交叉命中同一几何体后需要保持约 850ms，进度条填满才删除；交叉失效、离开目标或角度不足会清空进度。托盘按钮视觉尺寸不变，但手势命中区在上下方向扩展，降低摄像头定位抖动造成的选不中。新创建几何体使用递增 z 深度和 `renderOrder`，后创建对象渲染和命中均位于旧对象前方。移动、旋转、缩放中的对象使用黄色高光，删除瞄准目标使用红色高光，普通选中使用弱高光。
- 相关文件：`src/main.ts`、`src/shapeScene.ts`、`src/styles.css`
- 参考：自研规则。

### 单手姿态旋转与宽容捏合阈值

- 功能：单手捏住几何体后可更流畅地控制位置和三轴旋转；捏取几何体、托盘选中和双指缩放不再要求两个 landmark 完全重合，只要在合理距离内即可触发。
- 技术：MediaPipe 21 点 landmark、Three.js Euler rotation、校准阈值、TypeScript 连续几何特征映射。
- 算法/规则：单手控制点仍使用拇指尖 `4` 与食指尖 `8` 的中点来移动对象；旋转不再只依赖屏幕横移，而是同时读取食指根部 `5` 与小指根部 `17` 的屏幕连线角度作为 `palmRoll`、两点 z 差作为 `palmYaw`，再结合手掌尺度和捏合点 z 变化生成 X/Y/Z 三轴旋转，并使用角度平滑避免突跳。默认 Pinch 阈值放宽到 `0.56`，校准阈值按个人捏合距离放大并限制在 `0.36..0.62`；双手食指中指并拢缩放阈值放宽到 `0.62`，适配识别点不完全落在真实指尖的情况。
- 相关文件：`src/interactionMapper.ts`、`src/shapeScene.ts`、`src/gestureAnalyzer.ts`、`src/calibrationManager.ts`、`src/gestureEventEngine.ts`、`src/types.ts`
- 参考：自研规则，基于 MediaPipe landmark 数据；参考 landmark-driven 控制思路：Codrops Webcam 3D HandControls, https://tympanus.net/Tutorials/webcam-3D-handcontrols/

### 固定 AR 舞台布局

- 功能：手势移动、旋转、缩放几何体时，摄像头窗口、骨架画布和 Three.js 叠加层保持固定，不再因为右侧 HUD 文本、手部列表或对象计数变化而抖动。
- 技术：CSS Grid 固定视口布局、右侧调试面板内部滚动、`aspect-ratio`、`contain: layout paint size`。
- 算法/规则：桌面布局下 `html/body/#app` 锁定为视口高度并隐藏页面级滚动；`app-shell` 使用两行 grid，把顶部栏和工作区分离；右侧调试内容只在 `.debug-panel` 内滚动；AR 舞台使用稳定宽度和 16:9 比例，并通过 containment 隔离内部叠加层，避免实时 HUD 更新参与外部布局计算。窄屏布局仍恢复页面滚动，保证移动端可访问完整调试内容。
- 相关文件：`src/styles.css`
- 参考：自研布局规则。

### Hand of God 2.5D 物理小球关卡

- 功能：在同一个摄像头 AR 舞台中加入第一版可玩关卡：小球从左上起点出发，受重力、碰撞、滚动阻尼和摩擦影响，需要被手部碰撞体推入右下绿色终点。右侧 HUD 显示游戏状态、小球位置、速度、参与碰撞的手数，并提供 `Reset Ball` 重置小球。
- 技术：Three.js 渲染关卡、小球、终点和半透明手部碰撞提示；`cannon-es` 负责 2.5D 刚体世界、球体、静态平台、墙体、重力、摩擦和反弹；MediaPipe 映射后的手部坐标驱动 kinematic 手部碰撞体。
- 算法/规则：游戏物理默认暂停，必须先启动摄像头并完成或跳过校准才开始；物理世界使用屏幕平面作为 X/Y 运动平面，Z 轴锁定为 0，视觉层再以斜俯视 3D 相机显示平台厚度、墙体侧面和立体小球；每帧把手部捏合点通过相机射线投到地图平面，根据相邻帧位移计算 kinematic 手部速度，张手可推球，Pinch 时推力略增强；小球落出边界会回到起点；小球进入终点半径且速度低于阈值时判定通关。
- 相关文件：`src/shapeScene.ts`、`src/main.ts`、`src/styles.css`、`package.json`
- 参考：cannon-es, https://github.com/pmndrs/cannon-es；Three.js, https://github.com/mrdoob/three.js；自研手势到物理碰撞映射规则。

### 立体桌游风 2.5D 地图系统

- 功能：把原本由几根平台组成的简陋关卡升级为 `Level 01: Toybox Descent`：包含深色桌面底板、起点区、终点区、主路径、分层平台、边界墙、斜坡、危险掉落区、装饰柱、路径箭头和弹性阻挡点，整体呈现俯视角但带厚度和层次的 2.5D 立体桌游观感。右侧 `Game` 面板显示关卡名、掉落/重置提示、小球速度和参与碰撞的手数。
- 技术：新增配置化关卡数据，把起点、终点、掉落边界、物理平台、视觉危险区和装饰物从 `ShapeScene` 的硬编码中拆出；`ShapeScene` 根据关卡配置同时生成 Three.js mesh 和 cannon-es body。渲染层使用斜俯视正交相机和柔和阴影，让平台、墙体和装饰物显示侧面厚度；调试几何体托盘改为弱化的右上角浮层，避免抢占主地图视觉中心。
- 算法/规则：物理碰撞只绑定到平台、墙体、护栏、斜坡和弹性阻挡点；危险区只作为视觉掉落提示，小球越过关卡掉落边界后自动回到起点并在 HUD 中短暂显示掉落重置状态。关卡配置保留 `level-01` 编号和结构，后续可继续扩展 `level-02` 或把机关加入配置。
- 相关文件：`src/gameLevel.ts`、`src/shapeScene.ts`、`src/main.ts`、`src/styles.css`
- 参考：自研关卡配置和地图规则；Three.js, https://github.com/mrdoob/three.js；cannon-es, https://github.com/pmndrs/cannon-es。

## Notes

当前推荐继续使用网页技术栈完成期末项目，而不是迁移到 Unity 或虚幻。理由是现有 MediaPipe、Three.js、摄像头权限、手势校准、AR 坐标映射和 2.5D 物理关卡都已经在浏览器内打通；网页更容易演示、部署和提交，也足以实现俯视 2.5D 地图、立体桌游风美术和轻量机关。Unity/虚幻在专业地图编辑、复杂灯光和大型美术资产管线上更强，但迁移会重新处理摄像头输入、MediaPipe 接入、WebGL/桌面打包和交互调试，短期风险更高。后续如果需要大型 3D 场景、专业美术管线或复杂刚体机关，再考虑 Unity；当前阶段优先在网页中把核心手势玩法做完整。

当前物理关卡是 2.5D 游戏切片，不包含真实世界平面检测或真实深度遮挡。AR 几何体编辑器仍保留为调试和后续关卡道具原型能力。

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
- 双手都捏合后可控制选中几何体的位置和三轴旋转；双手前后移动时应能看到 X/Y 轴旋转响应。
- 双手食指和中指并拢后拉开/合拢，才会缩放选中几何体；普通双手捏合、交叉或移动不会改变缩放。
- 双手食指交叉时，如果交叉点落在多个重叠几何体上，会删除最前方的那个；立方体、球体、圆柱、圆锥和圆环都可删除。
- 右侧面板内容变化不影响摄像头主舞台尺寸。

- 几何体托盘在按钮上下方更大的区域内也能被手势命中，捏住 1 秒后按钮仍会高亮。
- 双食指交叉命中几何体时，交叉点附近会出现删除进度条；进度条填满后才删除目标。
- 删除进度期间目标几何体显示红色高光；移动、旋转、缩放期间当前对象显示黄色高光。
- 后创建的几何体应显示在旧几何体前方，交叉删除重叠对象时应删除最前方对象。
- 页面启动后应能直接看到 `Hand of God` 标题、绿色终点、小球、平台和右侧 `Game` 面板。
- 页面启动后应能直接看到 `Level 01: Toybox Descent` 的立体桌游风俯视地图，包含深色底板、起点环、终点环、路径平台、墙体、斜坡、危险区和装饰柱。
- 未开启摄像头或校准尚未完成时，`Game` 面板显示 `Waiting for calibration`，小球保持在起点且物理世界不推进。
- 完成或跳过校准后，游戏才开始运行；点击 `Reset Ball` 可回到左上起点。
- 小球掉出地图边界后应自动回到起点，`Game` 面板短暂显示掉落重置提示。
- 开启摄像头并完成或跳过校准后，手部位置会出现半透明白色碰撞体，张手可推小球，Pinch 时推动响应更强。
- 小球进入右下绿色终点并减速后，`Game` 面板应显示 `Goal reached`。

## Git Workflow

项目使用 `main` 作为主分支，功能按小步提交管理。远端仓库为 `https://github.com/MXH814/hci-gesture-game`。

本地资料文件不会推送到 GitHub：

- `Doc.pdf`
- `1.png`
- `课件/`

以后每次新增功能，都必须同步更新 README 的 `Implemented Features`，写明功能说明、实现技术、算法/规则、相关文件和参考来源。没有参考开源仓库的自研逻辑，只写“自研规则”，不编造来源。
