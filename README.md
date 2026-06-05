# Hand of God

同济 HCI 期末项目。当前项目方向已从网页原型切换为 **Unity 真 3D 重构**：玩家不再用手直接撞球，而是通过摄像头手势控制机关桌、挡板、机关等，让物理小球间接到达终点。

## 当前状态

- 旧网页原型仍保留在根目录，技术栈为 Vite + TypeScript + MediaPipe + Three.js + cannon-es。
- 新 Unity 原型位于 `unity/HandOfGodUnity`。
- Unity 手势桥接脚本位于 `unity/gesture_bridge`，使用 MediaPipe + OpenCV 识别手部姿态，并通过 UDP `127.0.0.1:5005` 发送给 Unity。
- Unity Hub 已安装；Unity Editor 推荐安装到 `E:\Unity\Hub\Editor`，Unity 项目和缓存也统一放在 E 盘。

## 为什么迁移到 Unity

网页版本验证了摄像头、MediaPipe、基础手势事件和物理滚球概念，但在“真实游戏地图”上遇到明显上限：建模资产、关卡编辑、光照、碰撞调试、物理手感和美术管线都不如专业游戏引擎直接。

Unity 更适合本项目后续目标：

- 用 Scene、Prefab、Material、Lighting 做真正游戏地图，而不是网页里手写几何体堆叠。
- 用 Rigidbody、Collider、Physic Material 组织稳定的物理滚球玩法。
- 用关卡编辑器快速设计迷宫、坡道、机关、终点祭坛和危险区。
- 手势识别可作为外部输入流接入 Unity，避免把视觉识别和游戏逻辑绑死在同一个渲染循环里。

## Unity 原型玩法

第一版 Unity 原型采用方案书里的“俯视滚球迷宫”思路，但做了更适合展示的调整：

- 地图主题：机关神殿。
- 地图结构：浮空底座、起点平台、终点祭坛、围栏、迷宫隔板、符文引导、柱廊装饰。
- 核心控制：手掌 Roll / Pitch 被映射为机关桌的 X/Z 倾斜控制。
- 小球规则：小球只受 Unity 物理和倾斜控制影响，手不会直接碰球。
- 降级验证：没有摄像头桥接时，可用 WASD / 方向键测试关卡路线。

## 打开 Unity 项目

1. 打开 Unity Hub。
2. 设置 Editor 安装路径为 `E:\Unity\Hub\Editor`。
3. 安装 Unity `2022.3 LTS`。
4. 在 Hub 中选择 Add project from disk，打开：

```text
E:\同济\大二下\用户交互技术HCI\期末项目\unity\HandOfGodUnity
```

第一次打开项目后，编辑器脚本会自动生成 `Assets/Scenes/Level01.unity`。也可以手动执行菜单：

```text
Hand of God > Rebuild Level 01 Scene
```

## 手势桥接

在 Unity 进入 Play 前，先启动 Python 手势桥接：

```powershell
cd "E:\同济\大二下\用户交互技术HCI\期末项目\unity\gesture_bridge"
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
python mediapipe_udp_sender.py
```

桥接窗口中：

- 按 `C`：把当前手掌姿态校准为中立姿态。
- 按 `Q`：退出桥接。

Unity 每帧读取 UDP 手势帧。没有新鲜手势帧时，HUD 会显示等待输入；可以用键盘临时验证关卡。

## Web 原型验证

旧网页原型仍可运行：

```powershell
npm install
npm run dev
npm run build
```

网页原型仅作为历史验证和调试参考，不再作为最终游戏实现方向。

## 后续路线

- Unity Level 01：完成真实美术地图、起点到终点路线、基础胜利/重置流程。
- 手势能力 1：手掌倾斜控制机关桌。
- 手势能力 2：捏取移动木块或升降门。
- 手势能力 3：食指戳按钮、开关门、启动传送带。
- 手势能力 4：旋转手势控制转盘或局部机关。

## Git 约定

- 主分支：`main`。
- 远端仓库：`MXH814/hci-gesture-game`。
- 每次新增功能必须更新 README。
- 本地资料文件不提交到 GitHub，例如 `.docx` 方案书、课件资料、临时截图和构建产物。
