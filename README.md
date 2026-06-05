# Hand of God

同济 HCI 期末项目。项目已经完全迁移为 **Unity 6.4 + Python MediaPipe 手势桥接**：Unity 负责游戏、校准、菜单、选关、关卡交互和 HUD；Python 只负责摄像头识别并通过 UDP 发送手部数据。

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

1. Python 桥接打开摄像头并发送手部数据。
2. Unity 启动后进入校准界面。
3. 玩家用手势完成校准，或用单指悬停触发“跳过校准”。
4. 进入全手势主菜单和选关界面。

推荐通过 `Play-HandOfGod.bat` 或桌面快捷方式启动。启动脚本只准备 Python 环境并启动 Unity；进入游戏后点击 `Start Camera`，Unity 会隐藏启动 Python MediaPipe 桥接。桥接不会打开独立 OpenCV 摄像头窗口，而是把摄像头画面嵌入 Unity 底层显示。

### 校准

- 第一步：张开手掌保持 1 秒。
- 第二步：拇指和食指捏合保持 1 秒。
- Unity 根据第二步采样生成本次游玩的捏合阈值。
- 校准界面中的“跳过校准”也使用单指悬停触发。
- 校准和游戏过程中，Unity 屏幕最前景会显示实时 21 点手骨架。看不到骨架时，说明摄像头桥接未启动或没有收到手部帧。
- 校准界面提供 `Start Camera` 按钮；没有手势输入时可直接用鼠标点击它来打开 Unity 内嵌摄像头画面。

### 菜单与按钮

- 所有菜单按钮都用 **食指悬停** 选择。
- 默认悬停时间：`0.85s`。
- 手指离开按钮后进度立即清空。
- 捏合不用于菜单点击，避免误触。
- 右上角始终有 `Exit` 按钮；关卡中还会显示 `Menu` 按钮。

### Level 0: Gesture Lab

第 0 关用于训练和验证手势：

- 场景中央有可交互物体。
- 右侧可用悬停选择 `Cube`、`Sphere`、`Cylinder`。
- 单手捏合可抓取并平滑移动物体。
- 双手同时捏合可旋转和缩放当前物体。
- 成功移动物体后显示完成状态，并可悬停进入第 1 关。

### Level 1: First Path

第 1 关是第一版正式玩法：

- 一条真实倾斜的 3D 石质道路。
- 小球会沿重力方向自动向下滚动。
- 木箱挡在路中间。
- 玩家不能直接碰球，只能捏取并移开木箱。
- 木箱靠近手势点时高亮，被抓取时变为青绿色高亮。
- 小球到达终点祭坛后显示 `PASS`，并提供 `Restart` / `Menu` 悬停按钮。

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

第一次启动时会在英文路径创建 Python 虚拟环境并安装依赖，可能需要几分钟。进入 Unity 后点击 `Start Camera`，摄像头权限由隐藏的 Python 桥接进程申请，画面直接显示在 Unity 游戏窗口内。

桥接依赖固定使用 `mediapipe==0.10.14`，因为更新版本的 MediaPipe 移除了当前脚本使用的 `mp.solutions.hands` API。启动脚本会检测该 API 是否存在；如果不兼容，会自动强制重装依赖。

MediaPipe 的原生模型加载对中文路径不稳定，因此启动脚本会把 Python 运行环境放在纯英文路径：

```text
E:\Unity\HandOfGodGestureBridge\.venv
```

项目仍然保存在当前中文目录，只有 Python 依赖环境放到英文路径。

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

生成的场景虽然文件名仍为 `Level01.unity`，但现在它是完整游戏入口：启动后先校准，再进入手势菜单和选关。

## 验证清单

- `python -m py_compile unity\gesture_bridge\mediapipe_udp_sender.py`
- Unity batchmode 生成场景无编译错误。
- `.\Build-HandOfGod.bat` 成功生成 Windows exe。
- 双击 `Play-HandOfGod.bat` 后进入 Unity 首屏，点击 `Start Camera` 后摄像头画面应出现在 Unity 窗口底层。
- 不使用鼠标完成校准、菜单选择、第 0 关物体移动、第 1 关木箱移动。
- 第 1 关小球到达终点后显示 `PASS`。
- 控制台没有 runtime error。

## Git 约定

- 每次新增功能同步更新 README。
- 不提交 `.docx`、`.venv`、Unity `Library/`、`Builds/`、`Logs/`、`UserSettings/`、临时截图和构建产物。
- 当前仓库不再保留网页旧版本源码。
