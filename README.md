# Hand of God

**Hand of God** 是同济大学用户交互技术 HCI 期末项目。项目以“无需鼠标键盘，直接用手操控物理世界”为核心体验：玩家在 Unity 游戏窗口中看到实时摄像头画面和 21 点手部骨架，通过捏合、双手旋转、双手拉合、张掌悬停、气流指向等手势完成校准、教学和机关解谜关卡。

项目由 Unity 游戏端和 Python MediaPipe 手势桥接端组成。Python 负责摄像头采集、MediaPipe Hands 识别和数据平滑；Unity 负责摄像头画面显示、手骨架叠加、校准流程、教学关、关卡机关、物理小球、UI 和桥接进程生命周期管理。

## 项目信息

- 游戏名称：`Hand of God`
- Unity 工程：`unity/HandOfGodUnity`
- 手势桥接：`unity/gesture_bridge`
- Unity 版本：Unity 6.4，项目使用 `E:\Unity\Hub\Editor\6000.4.10f1`
- Python 运行环境：`E:\Unity\HandOfGodGestureBridge\.venv`
- 仓库：`MXH814/hci-gesture-game`
- 主分支：`main`
- 支持平台：Windows

## 核心功能

- Unity 启动后自动进入校准界面，并自动尝试启动隐藏的 Python 摄像头桥接。
- 摄像头画面以内嵌背景形式显示在 Unity 窗口中，不打开独立 OpenCV 预览窗口。
- 屏幕最前景实时显示 MediaPipe Hands 的 21 点手骨架。
- 支持最多两只手，并保留 `Left` / `Right` handedness、识别置信度和手势特征。
- 校准界面只显示摄像头背景、手骨架和清晰提示，避免地图物体干扰。
- 所有主要 UI 按钮支持食指悬停选择，同时保留鼠标点击兜底。
- 左上方 `Exit` / `Calibrate` 按钮放大并向屏幕内侧移动，降低边缘手势抖动和误触。
- 校准完成后，左侧常驻 `Level Select` 选关栏，可随时进入或重进 Level 0、Level 1、Level 2。
- `Start / Retry Camera` 可在自动桥接失败时手动重试。
- Python 桥接使用本地锁端口 `5007` 保证单实例运行。
- Unity 退出时会关闭由 Unity 启动的桥接进程，释放摄像头。
- 第 0 关提供完整手势教学，新增手势都会在教学关中练习或在正式关卡中给出 HUD 提示。
- 第 1 关提供分段机关解谜路线，使用清障、合桥、旋转空中桥和张掌激活终点等机关。
- 第 2 关提供传送门与气流机关路线，使用钥匙放置、传送门激活和气流方向控制。
- 小球到达终点后立即停止并隐藏，显示醒目的 `PASS`。
- 小球掉落或离开安全路径后重置当前关卡机关状态。

## 运行方式

先构建 Windows 可执行文件：

```powershell
.\Build-HandOfGod.bat
```

构建成功后，通过根目录启动脚本运行：

```text
Play-HandOfGod.bat
```

也可以双击桌面快捷方式：

```text
Hand of God
```

启动脚本会准备 Python 环境和依赖，然后运行：

```text
unity\HandOfGodUnity\Builds\Windows\HandOfGod.exe
```

第一次运行时会在英文路径 `E:\Unity\HandOfGodGestureBridge\.venv` 创建 Python 虚拟环境并安装依赖，耗时可能较长。该路径用于规避 MediaPipe 原生模型资源在中文路径下加载不稳定的问题；Unity 项目本身仍保留在当前中文目录中。

## 启动与校准流程

1. 启动 Unity 游戏。
2. Unity 自动进入校准界面。
3. Unity 自动尝试隐藏启动 Python MediaPipe 桥接。
4. 摄像头画面显示在 Unity 背景层。
5. 前景显示实时 21 点手骨架和左右手标签。
6. 玩家张开手掌保持约 1 秒。
7. 玩家用拇指和食指捏合保持约 1 秒。
8. Unity 根据校准结果生成本次游戏的捏合阈值。
9. 校准完成后直接进入第 0 关教学。

校准阶段提供这些兜底能力：

- `Start / Retry Camera`：重新启动摄像头桥接。
- `Skip Calibration`：食指悬停跳过校准，用于课堂展示或无摄像头环境。
- `Exit`：退出游戏。
- 鼠标点击：所有 UI 按钮仍可用鼠标触发。

校准完成后，游戏界面左侧会显示 `Level Select` 选关栏。玩家可以用食指悬停直接进入 `Level 0: Tutorial`、`Level 1: Moving Path` 或 `Level 2: Portals`；在关卡中选择当前关卡会重新开始该关。

## 手势与交互

| 手势 | 用途 | 实现要点 |
| --- | --- | --- |
| 食指悬停 | 选择 UI 按钮 | 使用食指尖屏幕坐标命中按钮区域，保持约 `0.85s` 后触发；手指离开即清空进度 |
| 张掌保持 | 校准、激活神印按钮 | Python 根据五指伸展状态判断 open palm；Unity 检测手掌是否位于目标区域并保持指定时长 |
| 单手捏合 | 拖动物块、木箱、传送门钥匙 | Python 输出 pinch 状态和 pinch center；Unity 将 pinch center 映射到世界平面并移动目标物体 |
| 双手捏合旋转 | 旋转教学物体、旋转空中桥 | Unity 计算两只手 pinch center 连线角度变化，将角度增量映射为物体或机关的 Y 轴旋转 |
| 双手捏合拉合 | 合拢桥板机关 | Unity 记录双手初始距离，检测距离缩短比例并驱动桥板向中心吸附 |
| 双手食指中指并拢 | 教学地图旋转和缩放 | Python 输出 index/middle landmarks；Unity 检测每只手 index 和 middle 距离，双手同时满足后进入地图控制 |
| 气流指向手势 | 第 0 关教学、第 2 关临时产生气流 | 单手食指和中指并拢、拇指伸出、无名指和小指收起；Unity 用食指相对手腕的 X 方向判断左风或右风，手势消失后气流关闭 |

捏合与悬停均使用阈值范围判断，不要求指尖完全重合。捏合状态带有迟滞逻辑：进入捏合使用较小阈值，保持捏合使用较大阈值，从而减少临界点抖动导致的频繁断开。

## 关卡设计

### Level 0: Gesture Tutorial

第 0 关是完整教学关，目标是让玩家在进入正式关卡前理解核心手势。教学提示显示在画面中上方，成功后显示醒目的 `SUCCESS`，玩家再通过大号 `Continue` 按钮进入下一步；最后一个教学步骤完成后直接显示 `Next: Level 1`。左侧提供 `Tutorial Steps` 菜单，玩家可以随时跳转到任意教学步骤重新练习。

关卡类型：手势教学与交互练习。

核心规则：

- 教学关地板比正式关前的旧练习台更大，留出更宽的手势活动空间。
- 每个教学步骤只显示当前步骤需要的道具，避免无关物体干扰识别和理解。
- 单手拖动和双手旋转步骤显示中心物块，并保留 `Cube`、`Sphere`、`Cylinder` 形状切换。
- 拉合步骤只显示教学桥板；张掌步骤只显示发光神印；地图控制步骤只保留可旋转、缩放的练习地板。
- 气流步骤直接使用第 2 关同款中央气流走廊地板、格栅、青色箭头、雾带、流线和粒子风效，并放置在教学地板中央。
- 教学完成后不再进入额外的自由操作页，玩家可通过左侧步骤菜单选择任意教学项，或通过 `Next: Level 1` 进入正式关卡。

教学步骤：

| 步骤 | 教学目标 | 成功条件 |
| --- | --- | --- |
| 1/7 Move your hands freely | 随意移动双手，观察屏幕上的识别效果 | Unity 同时识别到左右手 |
| 2/7 Pinch an object with one hand and drag it | 单手拇指和食指捏合拖动物体 | 物体被拖动超过指定距离 |
| 3/7 Pinch both sides and rotate the object | 双手捏住物体两侧并像真实物体一样旋转 | 物体旋转超过指定角度 |
| 4/7 Join a bridge with both hands | 双手捏住桥板两端并向内拉合 | 双手距离缩短到阈值，桥板锁定 |
| 5/7 Open your palm over the glowing seal | 张开手掌悬停在发光神印上 | 神印区域内张掌保持到指定时长 |
| 6/7 Bring index and middle fingers together on both hands | 双手食指中指并拢控制地图 | 地图旋转或缩放超过指定变化量 |
| 7/7 Point the airflow with one hand | 单手拇指伸出、食指中指并拢、无名指小指收起，向左或向右指示气流 | 第 2 关同款气流区显示 `LEFT` 或 `RIGHT` 风效反馈；手势消失后风效关闭 |

教学关中的交互反馈：

- 可操作物体接近手势点时高亮。
- 抓取、旋转、桥板合拢和神印激活时使用高亮材质。
- 第 0 关左侧 `Tutorial Steps` 可随时选择任意教学步骤。
- 成功提示使用更大的固定字号、暖色高亮和脉冲式视觉反馈；`SUCCESS`、`TUTORIAL COMPLETE` 和 `PASS` 只做颜色/光晕闪烁，文字始终保持横向稳定显示。
- `Continue` 和 `Next: Level 1` 按钮尺寸和字体都较大，位于成功提示下方，便于手势悬停选择。

### Level 1: Trial of the Moving Path

第 1 关是分段机关解谜关。玩家需要逐段完成机关，小球才会被放行进入下一段。画面中上方显示当前目标，机关完成后出现 `SUCCESS`，并打开对应挡门。关卡整体使用第 2 关同款地牢地板、金属边框、青色能量和终点祭坛视觉。

关卡类型：分段放行的物理机关解谜。

胜利条件：

- 完成四段机关。
- 让小球沿倾斜道路进入终点祭坛。
- 到达终点后小球停止并隐藏，显示 `PASS`。

失败条件：

- 小球掉落到安全高度以下。
- 失败后重置小球、木箱、桥、旋转空中桥、神印按钮和所有挡门。

关卡结构：

| 分段 | 名称 | 机关 | 使用手势 | 目标 |
| --- | --- | --- | --- | --- |
| Segment 1 | 清障区 | 木箱、侧槽、起点门 | 单手捏合拖动 | 把木箱拖入发光侧槽，打开起点门 |
| Segment 2 | 断桥区 | 两块分离桥板、第二道门 | 双手捏合拉合 | 捏住桥板两端向内合拢，桥板锁定后开门 |
| Segment 3 | 旋转空中桥区 | 横在空隙中央的简单窄板桥、第三道门 | 双手捏合旋转 | 将窄板桥旋转 90 度接上两侧地板，未接好时小球会从空隙掉落 |
| Segment 4 | 终点按钮区 | 发光神印、终点门、祭坛 | 张掌悬停 | 张掌悬停激活神印，打开终点门 |

第 1 关的设计重点：

- 每一段只开放当前需要解决的机关；未到阶段的机关和已经完成的机关不会继续响应手势，降低玩家猜测成本。
- 挡门控制小球节奏，避免玩家还未理解机关时小球提前滚入复杂区域。
- 当前可操作机关使用 hover/held 高亮，反馈风格与教学关保持一致。
- 路径两侧不再使用长墙或高护栏，避免木箱进入侧槽时出现穿墙视觉问题。
- 断桥初始缝隙明显大于小球直径，拉合后吸附成连续通路。
- 旋转机关是横在空隙中央的简单窄板桥，初始与道路垂直；玩家旋转 90 度接上两侧地板后才会放行小球。
- 窄板桥使用单个 Unity primitive 盒体实现，不叠加 Kenney 地板模型，避免视觉上变成大面积平台。
- 窄板桥桥面高度与左右道路顶面基本平齐，并略低一点点，减少小球在桥头被碰撞边缘卡住的概率。
- 空中桥左侧地板加长，给玩家观察和操作的反应时间；桥两侧空隙明显宽于小球，桥未接好时小球会自然掉落。
- 终点复用第 2 关终点祭坛视觉。
- 双手旋转方向与真实手势方向一致。
- 双手捏合旋转物体不再缩放物体，缩放只用于地图控制手势。
- 小球物理由 Unity Rigidbody 驱动，并限制最大速度，保证通关过程可读。

### Level 2: Portals & Airflow

第 2 关是传送门与气流机关关。它使用独立的 `Level02.unity` 场景资源和 Kenney 模块化地牢素材搭建美术空间，关卡目标为“激活传送门，再保持气流手势产生风，让气流把小球送到终点”。玩家可以从主菜单选择 `Level 2: Portals & Airflow`，也可以在第 1 关通关后的 `PASS` 界面悬停 `Next: Level 2` 进入；在 Unity Editor 中直接打开 `Level02.unity` 时会自动进入第二关，便于单独调试美术和机关。

关卡类型：传送门触发 + 方向控制 + 物理推动。

胜利条件：

- 将传送门钥匙放到发光锁座插槽上。
- 激活传送门，使小球从左侧 Portal A 传送到右侧 Portal B。
- 保持气流指向手势产生向右的风。
- 气流带只在手势保持期间推动小球穿过最后挡门并进入终点。

失败条件：

- 小球掉落到安全高度以下。
- 失败后重新构建第 2 关，传送门、钥匙、气流方向、挡门和小球回到初始状态。

核心物体：

- `Portal Key`：可单手捏合拖动的低模钥匙，使用 OpenGameArt CC0 FBX 模型导入，初始位于左上侧。
- `key socket`：钥匙目标机关，位于平台左侧上方区域；由石质锁座、暗色插槽、黄铜护边和发光锁芯组成。
- `portal A`：小球初始传送门，位于左侧。
- `portal B`：传送后的落点，位于中右侧。
- `air belt`：与加长中央气流走廊地面等长的不可见触发器 + 可见气流 VFX，使用 Kenney Particle Pack 的透明粒子贴图制作青色雾带、细流线和方向反馈；内部触发区负责对小球施加水平加速度。
- `level2 gate`：传送门前铁栅门，传送成功后打开。
- `level2 gate2`：气流带到终点之间的铁栅门，保持右风手势时打开。
- `Goal Trigger`：右侧终点祭坛。

第二关美术：

- 场景拆分为左侧传送平台、中央气流廊和右侧祭坛平台，整体保持开阔俯视视野。
- 石板、边框和机关细节使用 Kenney `Modular Dungeon Kit` 的 FBX 模型与项目材质风格，放在 `Assets/Resources/KenneyDungeon/`，运行时通过 `Resources.Load` 加载。
- 传送门钥匙使用 OpenGameArt `Key - low poly` FBX 模型，放在 `Assets/Resources/OpenGameArt/LowPolyKey/`；Unity 只负责归一化尺寸、材质高亮和交互 collider。
- 气流与传送门漩涡使用 Kenney `Particle Pack` 透明 PNG 贴图，放在 `Assets/Resources/KenneyParticles/`；Unity 通过透明材质、quad 和 particle renderer 组合成动态 VFX。
- Unity 程序化几何只保留用于物理 collider、钥匙插座、传送门光圈、地面风向标记和必要机关结构。
- 中央气流走廊地面、气流触发区和气流 VFX 使用同一长度，保证玩家看到的风区和真实物理受力范围一致。
- 气流走廊地面包含通风格栅与青色箭头纹，提示玩家这里是风机关区域。
- 气流带关闭时不显示实体碰撞盒；玩家保持有效气流手势时显示覆盖整段气流走廊的青色雾带、流线和方向提示，手势消失后风效和物理风同时关闭。
- `LevelSceneGenerator.CaptureLevel02Preview` 会生成第二关气流开启状态预览图，输出到 `unity/HandOfGodUnity/Preview/Level02Preview.png`。

关卡结构：

| 分段 | 名称 | 机关 | 使用手势 | 目标 |
| --- | --- | --- | --- | --- |
| Segment 1 | 钥匙激活区 | Portal Key、key socket、Portal A/B | 单手捏合拖动 | 捏住钥匙并放到锁座插槽上，激活传送门 |
| Segment 2 | 传送区 | Portal A、Portal B、第一道挡门 | 锁座触发 | 激活后小球从 Portal A 升起淡出，再从 Portal B 渐显落下，并打开第一道挡门 |
| Segment 3 | 气流控制区 | Air Belt、第二道挡门 | 气流指向手势 | 拇指伸出、食指中指并拢、无名指小指收起，保持向右指向以产生右风 |
| Segment 4 | 终点区 | 气流带、终点祭坛 | 物理推动 | 气流推动小球进入终点，显示 `PASS` |

第 2 关的交互细节：

- 钥匙靠近手势点时会整把钥匙显示 hover 高亮；玩家捏合靠近钥匙时会进入抓取。
- 钥匙有较大的磁吸抓取半径：玩家只要在钥匙附近捏合，钥匙会先吸附到捏合点，再进入拖动状态。
- 放开钥匙且钥匙位于锁座附近时，钥匙会吸附到插槽上方，锁芯、Portal A 和 Portal B 都变为激活高亮。
- 传送门激活后，小球会冻结物理，先在 Portal A 上升、缩小并淡出，再在 Portal B 上方渐显、落下；动画结束后恢复 Rigidbody 并清空速度。该传送流程由 Unity 自主更新驱动，不依赖手继续留在屏幕中。
- 玩家捏着钥匙时如果手部追踪丢失，Unity 会按释放处理，并立即检查钥匙是否已经位于锁座范围内。
- 气流手势由 `IndexMiddleTogether + thumbExtended + ring/pinky curled + !openPalm` 组成，并带有方向死区和短时间稳定确认，避免张掌或瞬时抖动误触发。
- 气流只能在传送完成、进入气流阶段后由手势临时产生；钥匙尚未开锁时，风向手势不会改变风带状态。
- Unity 根据食指尖相对手腕的水平偏移判断风向：向右为通关方向，向左会显示提示要求改为向右。
- 气流带使用 `AirBeltTrigger`，在小球停留于触发区时以渐进强度施加水平方向加速度，并限制最大水平推速，让玩家有时间修正方向；风向反转时不会瞬移或清零速度，而是通过反向加速度让小球自然减速、掉头。
- 气流带在关闭时隐藏实体触发器；保持有效气流手势后 Kenney 粒子贴图雾带和流线随风向滚动，HUD 同步显示 `Airflow: OFF / LEFT / RIGHT`。
- 保持右风手势时，第二道挡门打开，HUD 显示右风提示；左风会显示纠正提示；松开或姿态不满足气流手势时，气流回到 `OFF`，已打开的挡门保持打开，避免突然卡住小球。

## 技术架构

```text
Camera
  │
  ▼
Python MediaPipe Bridge
  ├─ MediaPipe Hands: 21 点手部 landmarks、handedness、score
  ├─ Gesture analysis: pinch、open palm、finger extended、palm pose
  ├─ Smoothing: landmarks 速度感知平滑、pinch/index cursor EMA
  ├─ UDP 5005: gesture JSON
  └─ TCP 5006: JPEG camera frames
       │
       ▼
Unity HandOfGodUnity
  ├─ GestureUdpReceiver: 接收并解析手势 JSON
  ├─ CameraFrameReceiver: 接收 JPEG 并更新摄像头纹理
  ├─ GestureGameController: 校准、HUD、骨架绘制、关卡逻辑、桥接进程管理
  ├─ BallController: 小球物理、失败与通关状态
  ├─ AirBeltTrigger: 第 2 关气流触发区与小球加速度
  └─ LevelSceneGenerator: batchmode 场景生成入口
```

## Python 手势桥接

脚本：`unity/gesture_bridge/mediapipe_udp_sender.py`

依赖：`unity/gesture_bridge/requirements.txt`

```text
mediapipe==0.10.14
opencv-python
numpy
```

桥接功能：

- 使用 OpenCV 采集摄像头。
- 使用 MediaPipe Hands 最多识别两只手。
- 支持镜像画面，保证玩家看到的左右移动符合屏幕直觉。
- 输出 21 点 landmarks、左右手标签、识别分数、捏合中心、食指尖位置、掌宽、五指伸展、掌心姿态。
- 通过 UDP `127.0.0.1:5005` 发送手势 JSON。
- 通过 TCP `127.0.0.1:5006` 发送长度前缀 JPEG 摄像头帧。
- 使用 TCP 锁端口 `5007` 防止多个桥接实例同时占用摄像头。
- 默认 headless 运行；只有手动传入 `--preview` 时才显示 OpenCV 调试窗口。

### 手势数据字段

每帧 JSON 包含：

- `handCount`
- `hands`
- `timestamp`
- `pinch`
- `openPalm`
- `pinchX`
- `pinchY`
- `indexX`
- `indexY`
- `pinchDistance`
- `palmSpan`
- `palmRoll`
- `palmPitch`
- `palmYaw`
- `confidence`

每只手包含：

- `id`
- `handedness`
- `score`
- `landmarks`
- `thumbExtended`
- `indexExtended`
- `middleExtended`
- `ringExtended`
- `pinkyExtended`
- `pinch`
- `openPalm`
- `pinchX`
- `pinchY`
- `indexX`
- `indexY`

## Unity 实现

主要脚本：

- `unity/HandOfGodUnity/Assets/Scripts/GameBootstrap.cs`
- `unity/HandOfGodUnity/Assets/Scripts/GestureGameController.cs`
- `unity/HandOfGodUnity/Assets/Scripts/GestureFrame.cs`
- `unity/HandOfGodUnity/Assets/Scripts/GestureUdpReceiver.cs`
- `unity/HandOfGodUnity/Assets/Scripts/CameraFrameReceiver.cs`
- `unity/HandOfGodUnity/Assets/Scripts/BallController.cs`
- `unity/HandOfGodUnity/Assets/Scripts/AirBeltTrigger.cs`
- `unity/HandOfGodUnity/Assets/Editor/LevelSceneGenerator.cs`

Unity 端职责：

- 创建相机、灯光、背景摄像头画面平面和关卡根节点。
- 在 `OnGUI` 中绘制校准、教学、关卡、成功、失败和全局控制 UI。
- 将摄像头帧作为 Unity 纹理显示在背景层。
- 绘制 21 点手骨架、骨架连线、左右手标签和手势点。
- 根据 UDP 手势帧驱动校准状态机。
- 将 normalized screen coordinates 映射到 Unity 世界平面。
- 处理单手拖动、双手旋转、双手拉合、张掌悬停、地图旋转缩放、气流方向临时控制。
- 管理 Python 桥接进程启动、失败提示和退出清理。
- 生成 Level 0、Level 1 的程序化几何体、材质和机关；生成 Level 2 时额外加载 Kenney 地牢模型、Kenney 粒子贴图和 OpenGameArt 钥匙模型，组合为第二关独立美术场景。

## 算法实现

### Landmark 平滑

Python 对每个 hand id 的每个 landmark 维护一个 `SmoothedPoint`。新 landmark 到来时，根据点位移动速度和屏幕边缘程度计算动态 `alpha`：

- 速度高时增大 `alpha`，让快速动作保持跟手。
- 靠近画面边缘时降低 `alpha`，减少边缘识别抖动。
- 位移很小时进一步降低 `alpha`，过滤握拳、弯曲手指和单指指向时的微小跳动。
- `alpha` 被限制在稳定范围内，避免过度延迟或过度跳变。
- 手势分析使用平滑后的 landmarks，避免显示骨架稳定但 pinch/index 交互点仍受原始抖动影响。

### Cursor EMA 平滑

Python 对 `pinchX`、`pinchY`、`indexX`、`indexY` 使用指数移动平均：

```text
smoothed = previous * 0.72 + current * 0.28
```

实际运行时会根据姿态动态调整平滑强度：捏合拖动物体时提高 pinch center 响应速度；只伸出食指且其他手指弯曲时降低 index cursor 的更新系数，减少 UI 悬停和经过物体附近时的抖动。

### 捏合检测

Python 使用拇指尖与食指尖距离除以掌宽得到归一化 `pinchDistance`。捏合采用迟滞阈值：

- 未捏合时，距离小于 `0.72` 进入捏合。
- 已捏合时，距离小于 `0.92` 继续保持捏合。

这样可以避免手指处于阈值附近时在捏合和释放之间频繁闪烁。

### 张掌检测

Python 根据五指伸展状态判断 open palm。Unity 进一步要求手掌位于指定机关区域，并保持短时间后才激活机关。捏合状态下会抑制 open palm，避免两种手势互相误判。

### 双手旋转

Unity 读取两只手的 pinch center，计算连线角度：

```text
angle = atan2(handB.y - handA.y, handB.x - handA.x)
delta = angle - startAngle
```

教学物体和旋转空中桥都使用该角度增量驱动旋转方向，使物体旋转方向与玩家双手旋转方向一致。

### 双手拉合

Unity 在进入拉合动作时记录两手初始距离：

```text
startDistance = distance(handA, handB)
```

随后计算当前距离缩短比例，并用插值驱动桥板向中心移动。当距离缩短到指定比例后，桥板吸附到锁定位置并触发机关完成。

### 地图旋转与缩放

Unity 检测每只手的食指和中指是否并拢。当两只手都满足条件时，记录双手中点、距离和角度：

- 双手整体移动用于平移控制参考。
- 双手距离变化用于地图缩放。
- 双手连线角度变化用于地图旋转。

该手势仅用于教学地图控制，不影响物块尺寸。

### 气流方向识别

第 0 关和第 2 关使用同一套气流指向手势。Unity 先检测：

```text
IndexMiddleTogether(hand)
thumbExtended == true
openPalm == false
ring/pinky curled by landmark geometry
ringExtended == false && pinkyExtended == false
```

该判定会显式排除张掌，并要求无名指和小指在 landmarks 上呈弯曲关系。满足后读取食指尖与手腕的水平差值：

```text
dirX = landmark[8].x - landmark[0].x
```

- `dirX > 0`：气流方向设为右。
- `dirX < 0`：气流方向设为左。
- 未检测到有效气流手势或方向落入死区时，气流方向设为 `OFF`。
- 右风会打开通往终点的挡门；左风只显示纠正提示。
- 小于方向死区的水平偏移会被忽略；同一方向需要短暂稳定保持后才正式切换气流方向。

### 气流带物理

`AirBeltTrigger` 是第 2 关的触发器组件。当小球 Rigidbody 停留在气流带触发区内，并且气流方向不是 `0` 时：

- 使用 `ForceMode.Acceleration` 按当前方向持续施加水平力。
- 推力强度从较低值开始，在约 `1.45s` 内渐进增强，避免小球刚进气流带就被突然吹走。
- 当风向切换或手势消失时降低气流 ramp 强度但保留小球当前速度，使反向风先抵消惯性再推动掉头；没有有效手势时不再施加风力。
- 对水平绝对风速做上限控制，保证气流有效但仍留给玩家调整空间。

### 物理小球

`BallController` 使用 Unity Rigidbody：

- 连续动态碰撞检测。
- 插值渲染。
- 最大速度限制。
- 掉落高度检测。
- 终点半径检测。
- 通关或失败后停止并隐藏小球。

## 场景生成与构建

Unity 场景由编辑器工具生成：

```powershell
& "E:\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.com" -batchmode -quit -projectPath "E:\同济\大二下\用户交互技术HCI\期末项目\unity\HandOfGodUnity" -executeMethod HandOfGod.EditorTools.LevelSceneGenerator.RebuildLevel01
```

构建 Windows 版本：

```powershell
.\Build-HandOfGod.bat
```

生成文件：

```text
unity\HandOfGodUnity\Builds\Windows\HandOfGod.exe
```

`Level01.unity` 是完整游戏入口场景，启动后包含自动桥接、校准、第 0 关教学、第 1 关和第 2 关。仓库中也包含 `Level02.unity` 作为第二关独立场景资源文件；在 Unity Editor 中直接打开 `Level02.unity` 会自动进入第二关，方便单独检查传送门、气流和地牢美术。Windows 构建包含 `Level01.unity` 和 `Level02.unity`，首个启动场景仍是 `Level01.unity`。

## 手动运行桥接

一般情况下不需要手动运行桥接。调试时可使用：

```powershell
cd "E:\同济\大二下\用户交互技术HCI\期末项目"
python -m venv "E:\Unity\HandOfGodGestureBridge\.venv"
& "E:\Unity\HandOfGodGestureBridge\.venv\Scripts\python.exe" -m pip install -r "unity\gesture_bridge\requirements.txt"
cd "unity\gesture_bridge"
& "E:\Unity\HandOfGodGestureBridge\.venv\Scripts\python.exe" mediapipe_udp_sender.py --preview
```

调试窗口快捷键：

- `C`：将当前姿态设为 Python 侧中立姿态。
- `Q`：退出桥接。

## 验证清单

基础验证：

- `python -m py_compile unity\gesture_bridge\mediapipe_udp_sender.py`
- Unity batchmode 重建 `Level01.unity` 无编译错误。
- `.\Build-HandOfGod.bat` 成功生成 Windows exe。
- 启动游戏后自动进入校准界面。
- 摄像头桥接自动启动，画面显示在 Unity 背景层。
- 不弹出独立摄像头预览窗口。
- 退出游戏后摄像头释放，无残留 `mediapipe_udp_sender.py` 进程。

校准与 UI：

- 张掌保持和捏合保持均可完成校准。
- `Start / Retry Camera` 可重新启动桥接。
- `Exit`、`Calibrate`、`Continue`、`Next: Level 1`、`Next: Level 2`、`Restart`、`Tutorial` 都可用食指悬停触发。
- 校准完成后的左侧 `Level Select` 可随时选择 Level 0、Level 1、Level 2。
- 第 0 关左侧 `Tutorial Steps` 可随时选择任意教学步骤。
- 按钮尺寸和位置适合手势悬停，不需要把手移动到屏幕极边缘。

第 0 关：

- 左右手识别提示正确。
- 教学关地板更大，每一步只显示该步用到的道具。
- 单手捏合拖动物体时物体跟手移动。
- 单指指向、其他手指弯曲时，骨架和食指悬停点应保持稳定，不应向附近物体明显吸附抖动。
- 双手捏合旋转物体方向自然，物体不缩放。
- 双手捏合拉合桥板可成功锁定。
- 张掌悬停可激活神印。
- 双手食指中指并拢可控制地图旋转和缩放。
- 单手气流指向手势可在教学地板中央的第 2 关同款气流地板、雾带、流线和箭头上显示左风或右风反馈；手势停止后风效关闭。
- 每步成功后显示醒目 `SUCCESS`，并等待玩家手动 `Continue`。
- 最后一个教学步骤完成后不进入自由操作页，悬停 `Next: Level 1` 进入第 1 关。
- `TUTORIAL COMPLETE` 始终横向显示，只做光晕闪烁，不出现横竖交替抖动。

第 1 关：

- 清障、合桥、旋转空中桥、神印激活四段目标顺序清楚。
- 第 1 关整体材质与第 2 关风格统一。
- 路径两侧长墙已删除，木箱移入侧槽时不会视觉穿墙。
- 第 1 段木箱可直接用拇指食指捏合抓取移动，不需要先张掌选中。
- 断桥初始缝隙明显宽于小球，拉合后形成连续通路。
- 第三段是横在空隙中央的简单窄板桥；未旋好时两侧空隙足够让小球掉落，旋好后小球可从桥面顺畅通过且不穿模、不在桥头卡住。
- 第 1、2 关未到阶段的机关和已经完成的机关不会继续响应手势。
- 终点视觉与第 2 关终点一致。
- 每段机关完成后显示 `SUCCESS` 并打开对应挡门。
- 小球不会在机关完成前提前进入下一段。
- 当前可操作机关有 hover/held 高亮。
- 小球到达终点后停止、隐藏，并显示醒目 `PASS`。
- 小球掉落后整关重置，所有机关回到初始状态。
- 第 1 关通关后 `PASS` 界面显示 `Next: Level 2`。

第 2 关：

- 主菜单可直接进入 `Level 2: Portals & Airflow`。
- Unity Editor 可直接打开 `Level02.unity` 进入第二关；`CaptureLevel02Preview` 可输出第二关截图。
- 传送门钥匙是导入的低模钥匙模型，可用单手捏合直接抓取和移动，抓取时整把钥匙高亮。
- 钥匙放到锁座插槽上并释放后，钥匙吸附固定，锁芯和两个传送门变为激活高亮。
- 小球从 Portal A 升起淡出，再从 Portal B 渐显落下，并打开第一道挡门；手离开屏幕时传送仍会自动完成。
- 传送完成前不能设置风向；传送完成后，气流指向手势可被识别：拇指伸出、食指中指并拢、无名指和小指收起；只有保持该手势时才产生风。
- 中央气流走廊地图段更长，气流带、青色雾带、细流线和方向提示与该段地面等长；HUD 显示当前气流方向。
- 地面通风格栅和青色箭头会提示玩家这里可以产生气流。
- 向右指向后 HUD 显示右风提示，第二道挡门打开。
- 小球进入气流带后，在玩家保持气流手势期间逐渐加速；风向反转时小球会先被反向加速度减速，再掉头；手势停止后风力关闭。
- 小球到达终点后显示 `PASS`。
- 小球掉落后 Level 2 全局重置，钥匙、锁座、传送门、气流方向、气流 VFX、挡门和小球回到初始状态。

## 目录结构

```text
.
├─ README.md
├─ Build-HandOfGod.bat
├─ Play-HandOfGod.bat
├─ unity
│  ├─ gesture_bridge
│  │  ├─ mediapipe_udp_sender.py
│  │  └─ requirements.txt
│  └─ HandOfGodUnity
│     ├─ Assets
│     │  ├─ Editor
│     │  │  └─ LevelSceneGenerator.cs
│     │  ├─ Scenes
│     │  │  ├─ Level01.unity
│     │  │  └─ Level02.unity
│     │  └─ Scripts
│     │     ├─ AirBeltTrigger.cs
│     │     ├─ BallController.cs
│     │     ├─ CameraFrameReceiver.cs
│     │     ├─ GameBootstrap.cs
│     │     ├─ GestureFrame.cs
│     │     ├─ GestureGameController.cs
│     │     ├─ GestureUdpReceiver.cs
│     │     └─ OneEuroFilter.cs
│     └─ ProjectSettings
└─ tools
```

## 第三方技术

- Unity 6.4：游戏运行、3D 场景、物理、UI 和构建。
- Python：摄像头桥接与手势识别进程。
- MediaPipe Hands：手部 21 点 landmarks、左右手识别和置信度。
- OpenCV：摄像头采集、镜像、缩放和 JPEG 编码。
- NumPy：手部距离、速度和平滑计算。
- Kenney Modular Dungeon Kit：CC0 模块化地牢 3D 素材。第二关使用其中精选 FBX 模型搭建地牢墙体、地面模块和场景细节，许可证文件保存在 `Assets/Resources/KenneyDungeon/ModularDungeonKit-License.txt`。
- Kenney Particle Pack：CC0 粒子贴图素材。第二关使用透明 PNG 制作气流雾带、流线和传送门漩涡，许可证文件保存在 `Assets/Resources/KenneyParticles/ParticlePack-License.txt`。
- OpenGameArt Key - low poly：CC0 低模钥匙模型。第二关使用其中 `key.fbx` 作为传送门钥匙，许可证文件保存在 `Assets/Resources/OpenGameArt/LowPolyKey/LowPolyKey-License.txt`。

## 开源项目与资料来源

- MediaPipe：Google 开源的跨平台感知计算框架。本项目通过 `mediapipe==0.10.14` Python package 使用其 Hands solution，获取手部 21 点 landmarks、handedness 和识别置信度。
  - GitHub: `https://github.com/google-ai-edge/mediapipe`
  - Documentation: `https://ai.google.dev/edge/mediapipe/solutions/vision/hand_landmarker`
- OpenCV：开源计算机视觉库。本项目通过 `opencv-python` 采集摄像头、镜像画面、调整分辨率并将摄像头帧编码为 JPEG。
  - GitHub: `https://github.com/opencv/opencv`
  - Python package: `https://pypi.org/project/opencv-python/`
- NumPy：开源数值计算库。本项目在 Python 桥接中用于 landmark 距离、速度和平滑相关的向量计算。
  - GitHub: `https://github.com/numpy/numpy`
  - Documentation: `https://numpy.org/doc/`
- One Euro Filter：实时交互场景中常用的低延迟平滑算法资料。本项目包含 Unity 侧 `OneEuroFilter` 实现，并在 Python 桥接中采用速度感知的轻量平滑策略来降低边缘抖动。
  - Project page: `https://gery.casiez.net/1euro/`
- Kenney Modular Dungeon Kit：公共领域 CC0 游戏素材包。
  - Source: `https://opengameart.org/content/modular-dungeon-kit`
  - Creator: `https://www.kenney.nl/`
- Kenney Particle Pack：公共领域 CC0 粒子贴图包。
  - Source: `https://kenney.nl/assets/particle-pack`
  - Creator: `https://www.kenney.nl/`
- OpenGameArt Key - low poly：公共领域 CC0 低模钥匙模型。
  - Source: `https://opengameart.org/content/key-low-poly`
  - Creator: `https://opengameart.org/users/knotai`

## Git 与提交约定

- README 与功能实现保持同步。
- 不提交 `.docx`、`.venv`、Unity `Library/`、`Builds/`、`Logs/`、`UserSettings/`、临时日志、临时截图和本地构建产物。
- 提交前确认 `git status` 中只包含计划内文件。
- 涉及代码或场景变化时，提交前运行 Python 编译、Unity 场景重建和 Windows 构建验证。
