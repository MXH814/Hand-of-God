# Hand of God

同济 HCI 期末项目。项目已经完全迁移为 **Unity 6.4 + Python MediaPipe 手势桥接**：Unity 负责游戏、校准、教学关、关卡交互、摄像头画面显示和 HUD；Python 只负责摄像头识别并通过 UDP/TCP 发送手部数据。

## 当前方向

- 游戏名：**Hand of God**
- 主工程：`unity/HandOfGodUnity`
- 手势桥接：`unity/gesture_bridge`
- Unity Editor：`E:\Unity\Hub\Editor\6000.4.10f1`
- 远端仓库：`MXH814/hci-gesture-game`
- 主分支：`main`

旧的 Vite / TypeScript / Web 原型已经从仓库移除。正式展示和后续开发均以 Unity 版本为准。

## 玩法结构

### 启动流程

1. Unity 启动后立即进入校准界面，并自动尝试启动隐藏的 Python 摄像头桥接。
2. 摄像头画面显示在 Unity 内嵌背景层，21 点手骨架显示在最前景。
3. 玩家用 open hand hold + pinch hold 完成校准，或用单指悬停触发“跳过校准”。
4. 校准完成后直接进入第 0 关教学关，再由教学关进入第 1 关。

推荐通过 `Play-HandOfGod.bat` 或桌面快捷方式启动。启动脚本准备 Python 环境并启动 Unity；Unity 正常情况下会自动隐藏启动 Python MediaPipe 桥接，不需要手动点摄像头按钮。校准界面仍保留 `Start / Retry Camera` 作为兜底。桥接不会打开独立 OpenCV 摄像头窗口，而是把摄像头画面嵌入 Unity 底层显示。

### 校准

- 第一步：张开手掌保持 1 秒。
- 第二步：拇指和食指捏合保持 1 秒。
- Unity 根据第二步采样生成本次游玩的捏合阈值。
- 校准界面不显示游戏地图或神殿台座，只显示内嵌摄像头背景、前景手骨架和清晰校准提示。
- 校准界面中的“跳过校准”也使用单指悬停触发。
- 校准和游戏过程中，Unity 屏幕最前景会显示实时 21 点手骨架，并用不同颜色/标签区分 `Left` 与 `Right`。
- 校准界面提供 `Start / Retry Camera` 按钮；没有手势输入时可直接用鼠标点击它来重试 Unity 内嵌摄像头桥接。

### 按钮

- 所有界面按钮都可用 **食指悬停** 选择。
- 默认悬停时间：`0.85s`。
- 手指离开按钮后进度立即清空。
- 捏合不用于按钮点击，避免误触。
- 右上角始终有更靠内、更大的 `Exit` 按钮；关卡中还会显示同样内移的 `Calibrate` 按钮用于重新校准，避免手靠近屏幕边缘时抖动误触。

### Level 0: Gesture Tutorial

第 0 关是真正的手势教学关，会按步骤给出目标、进度和成功反馈：

- 第 1 步：同时识别左右手和食指位置。
- 第 2 步：单手拇指 + 食指捏合，拖动物体。
- 第 3 步：双手拇指 + 食指捏合，像真实物理世界一样旋转物体；物体不会被缩放。
- 第 4 步：双手食指 + 中指并拢，拖动调节教学地图旋转和缩放。
- 第 5 步：显示完成状态，仍可继续练习拖动、旋转物体和控制地图，悬停 `Next: Level 1` 后进入第 1 关。
- 场景中央有可交互物体。
- 物体靠近手势点时会高亮，被抓取或双手旋转时会使用和第 1 关一致的青绿色高亮。
- 教学步骤需要保持约 3 秒，方便玩家看清动作和进度反馈。
- 右侧可用悬停选择 `Cube`、`Sphere`、`Cylinder`。
- 教学主提示显示在画面中上方，使用更大的文字和更长的进度条。
- “捏合”和“并拢”都使用阈值范围识别，不要求指尖完全重合。

### Level 1: First Path

第 1 关是第一版正式玩法：

- 一条真实倾斜的 3D 石质道路。
- 小球会沿重力方向自动向下滚动。
- 木箱挡在路中间。
- 玩家不能直接碰球，只能捏取并移开木箱。
- 木箱靠近手势点时高亮，被抓取时变为青绿色高亮。
- 小球到达终点祭坛后会立即停止并消失，再显示 `PASS`，并提供 `Restart` / `Tutorial` 悬停按钮。
- 小球掉落失败时会重置整个第 1 关，包括小球和可操作木箱位置。

## 手势桥接数据

`unity/gesture_bridge/mediapipe_udp_sender.py` 使用 MediaPipe Hands 识别最多两只手，并向 `127.0.0.1:5005` 发送 JSON UDP 帧。

Unity 通过两个本地通道接收数据：

- UDP `127.0.0.1:5005`：手势 JSON。
- TCP `127.0.0.1:5006`：长度前缀 JPEG 摄像头帧。

手势 JSON 包括：

- 21 个手部 landmarks
- handedness 和 score
- pinch center、index fingertip
- pinchDistance、palmSpan
- finger extended 状态
- palm roll / pitch / yaw
- timestamp

Python 桥接会对每只手的 21 个 landmarks 做轻量速度感知平滑，降低屏幕边缘骨架抖动；Unity 继续保留 handedness，用于骨架颜色、标签和教学步骤判断。

Unity 中的主要脚本：

- `unity/HandOfGodUnity/Assets/Scripts/GestureUdpReceiver.cs`
- `unity/HandOfGodUnity/Assets/Scripts/GestureFrame.cs`
- `unity/HandOfGodUnity/Assets/Scripts/GestureGameController.cs`
- `unity/HandOfGodUnity/Assets/Scripts/GameBootstrap.cs`
- `unity/HandOfGodUnity/Assets/Scripts/BallController.cs`

## 一键启动

先构建 Windows 可执行文件：

```powershell
.\Build-HandOfGod.bat
```

构建完成后，双击根目录：

```text
Play-HandOfGod.bat
```

或双击桌面快捷方式：

```text
Hand of God
```

启动脚本会先准备 Python 虚拟环境和依赖，再启动：

```text
unity\HandOfGodUnity\Builds\Windows\HandOfGod.exe
```

第一次启动时会在英文路径创建 Python 虚拟环境并安装依赖，可能需要几分钟。进入 Unity 后摄像头权限由隐藏的 Python 桥接进程申请，画面直接显示在 Unity 游戏窗口内；如果自动启动失败，可在校准界面点击 `Start / Retry Camera`。

桥接依赖固定使用 `mediapipe==0.10.14`，因为更新版本的 MediaPipe 移除了当前脚本使用的 `mp.solutions.hands` API。启动脚本会检测该 API 是否存在；如果不兼容，会自动强制重装依赖。

MediaPipe 的原生模型加载对中文路径不稳定，因此启动脚本会把 Python 运行环境放在纯英文路径：

```text
E:\Unity\HandOfGodGestureBridge\.venv
```

项目仍然保存在当前中文目录，只有 Python 依赖环境放到英文路径。

桥接进程使用本地锁端口 `5007` 保证单实例运行，避免自动启动或多次点击 `Start / Retry Camera` 后多个 Python 进程同时抢摄像头。退出 Unity 时会主动关闭由 Unity 启动的桥接进程。

如果直接双击 `HandOfGod.exe`，Unity 会尝试自行启动桥接；失败信息会显示在校准界面，并写入：

```text
unity\gesture_bridge\gesture-bridge-runtime.log
```

## 手动运行手势桥接

```powershell
cd "E:\同济\大二下\用户交互技术HCI\期末项目"
python -m venv "E:\Unity\HandOfGodGestureBridge\.venv"
& "E:\Unity\HandOfGodGestureBridge\.venv\Scripts\python.exe" -m pip install -r "unity\gesture_bridge\requirements.txt"
cd "unity\gesture_bridge"
& "E:\Unity\HandOfGodGestureBridge\.venv\Scripts\python.exe" mediapipe_udp_sender.py
```

桥接窗口快捷键：

- `C`：将当前手掌姿态设为 Python 侧中立姿态。
- `Q`：退出桥接。

## Unity 场景生成与构建

Unity 场景由编辑器工具生成：

```powershell
& "E:\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.com" -batchmode -quit -projectPath "E:\同济\大二下\用户交互技术HCI\期末项目\unity\HandOfGodUnity" -executeMethod HandOfGod.EditorTools.LevelSceneGenerator.RebuildLevel01
```

构建游戏：

```powershell
.\Build-HandOfGod.bat
```

生成的场景虽然文件名仍为 `Level01.unity`，但现在它是完整游戏入口：启动后自动启动摄像头桥接、进入校准，校准完成后直接进入第 0 关教学关。

## 验证清单

- `python -m py_compile unity\gesture_bridge\mediapipe_udp_sender.py`
- Unity batchmode 生成场景无编译错误。
- `.\Build-HandOfGod.bat` 成功生成 Windows exe。
- 双击 `Play-HandOfGod.bat` 后进入 Unity 校准界面，摄像头桥接应自动启动，摄像头画面应出现在 Unity 窗口底层。
- 不使用鼠标完成校准、第 0 关教学步骤、第 1 关木箱移动。
- 第 0 关完成教学后仍能继续操控；双手捏合物体只旋转不缩放，物体和地图旋转方向应与双手手势一致。
- 捏住物体移动时，物体应贴合手势目标移动，不再明显慢于手部。
- 第 1 关小球到达终点后显示 `PASS`。
- 第 1 关小球掉落后整关重置，木箱回到初始位置。
- 退出游戏后摄像头灯熄灭，没有残留 `mediapipe_udp_sender.py` 进程。
- 控制台没有 runtime error。

## Git 约定

- 每次新增功能同步更新 README。
- 不提交 `.docx`、`.venv`、Unity `Library/`、`Builds/`、`Logs/`、`UserSettings/`、临时截图和构建产物。
- 当前仓库不再保留网页旧版本源码。
