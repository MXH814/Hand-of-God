# Hand of God

同济 HCI 期末项目。项目已从网页原型切换为 **Unity 6 真 3D 游戏重构**：玩家不再用手直接撞球，而是通过摄像头手势控制机关桌、挡板、机关等，让物理小球间接到达终点。

## 当前方向

- 游戏名：**Hand of God**
- 主实现：Unity 6.4 `(6000.4.10f1)`
- Unity 工程：`unity/HandOfGodUnity`
- 手势桥接：`unity/gesture_bridge`
- 旧网页原型仍保留在根目录，仅作为历史验证和调试参考。

## Unity 安装约定

- Unity Hub：`E:\Unity\UnityHub`
- Unity Editor：`E:\Unity\Hub\Editor\6000.4.10f1`
- Unity 项目：`E:\同济\大二下\用户交互技术HCI\期末项目\unity\HandOfGodUnity`

如果 Hub 仍通过 `C:\Program Files\Unity\Hub\Editor\6000.4.10f1` 找 Editor，该路径应只是一个 Junction，实际文件在 E 盘。

## 已实现功能

### Unity 6 正式地图第一版

`Level01` 已从临时几何体迷宫升级为“机关神殿”主题地图：

- 浮空黑曜石基座和石质岛屿
- 起点平台、分段石桥、中心圆形庭院、终点祭坛
- 外围围栏、内部导向墙、柱廊、火盆、符文引导线
- 青绿色发光终点环、中央控制光环、暖色火盆灯
- 固定俯视偏斜正交相机，能看到高度、侧面和空间层次
- 场景生成器会把地图真实保存到 Unity Scene 中，打开编辑器即可看到地图

相关文件：

- `unity/HandOfGodUnity/Assets/Scripts/GameBootstrap.cs`
- `unity/HandOfGodUnity/Assets/Editor/LevelSceneGenerator.cs`

### Unity 手势输入桥接

Python 脚本使用 MediaPipe + OpenCV 识别单手姿态，并通过 UDP `127.0.0.1:5005` 发给 Unity。

相关文件：

- `unity/gesture_bridge/mediapipe_udp_sender.py`
- `unity/HandOfGodUnity/Assets/Scripts/GestureUdpReceiver.cs`
- `unity/HandOfGodUnity/Assets/Scripts/BoardTiltController.cs`

### 旧网页原型

网页原型保留以下能力作为参考：

- Vite + TypeScript
- MediaPipe Hand Landmarker
- Three.js + cannon-es
- 基础手势识别、校准、调试几何体和网页 3D 原型

后续最终展示以 Unity 版本为主。

## 打开 Unity 项目

1. 打开 Unity Hub。
2. Add project from disk。
3. 选择：

```text
E:\同济\大二下\用户交互技术HCI\期末项目\unity\HandOfGodUnity
```

4. 用 Unity `6.4 (6000.4.10f1)` 打开。
5. 如果场景未生成，执行菜单：

```text
Hand of God > Rebuild Level 01 Scene
```

## 手势桥接运行

```powershell
cd "E:\同济\大二下\用户交互技术HCI\期末项目\unity\gesture_bridge"
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
python mediapipe_udp_sender.py
```

桥接窗口：

- `C`：校准当前手掌姿态为中立姿态
- `Q`：退出

没有启动手势桥接时，Unity 中可以用 WASD / 方向键临时验证地图路线。

## 验证

网页原型：

```powershell
npm run build
```

Unity：

```powershell
& "E:\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -batchmode -quit -projectPath "E:\同济\大二下\用户交互技术HCI\期末项目\unity\HandOfGodUnity" -executeMethod HandOfGod.EditorTools.LevelSceneGenerator.RebuildLevel01
```

手动检查：

- 打开 `Assets/Scenes/Level01.unity`
- 首屏能看到完整机关神殿地图，而不是散落几何体
- 地图应包含起点、分段路线、中心庭院、终点祭坛、柱子、火盆、符文、围栏和发光提示
- Play 模式下小球从起点出现，目标祭坛在地图右上区域

## 后续路线

- 地图美术继续升级：材质资产、地砖纹理、坡道模型、可交互机关 Prefab
- 玩法能力 1：手掌倾斜控制机关桌
- 玩法能力 2：捏取移动木块或升降门
- 玩法能力 3：食指戳按钮、开关门、启动传送带
- 玩法能力 4：旋转手势控制转盘或局部机关

## Git 约定

- 主分支：`main`
- 远端仓库：`MXH814/hci-gesture-game`
- 每次新增功能必须同步更新 README
- 不提交 `.docx` 方案书、课件资料、临时截图、Unity Library/Temp/Build 等本地文件
