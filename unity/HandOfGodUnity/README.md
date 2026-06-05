# Hand of God Unity Project

This is the main Unity-only version of the HCI gesture rolling-ball game.

## Version

- Editor: Unity 6.4 `(6000.4.10f1)`
- Project path: `E:\同济\大二下\用户交互技术HCI\期末项目\unity\HandOfGodUnity`
- Editor path: `E:\Unity\Hub\Editor\6000.4.10f1`

## Runtime Flow

The generated scene `Assets/Scenes/Level01.unity` is now the full game entry scene:

1. Gesture calibration
2. Gesture-only main menu
3. Level 0 gesture lab
4. Level 1 first path
5. PASS / Restart / Menu screen

All player-facing UI is controlled by index-finger dwell selection. Mouse controls are not part of the main game.

The recommended launcher is the repository root `Play-HandOfGod.bat`. It prepares the Python environment, then launches Unity with the bridge directory argument. The player clicks `Start Camera` inside Unity to start the hidden MediaPipe bridge. The bridge sends landmarks through UDP `5005` and embedded JPEG camera frames through TCP `5006`.

The launcher keeps the Python virtual environment at `E:\Unity\HandOfGodGestureBridge\.venv` because MediaPipe native model loading is unreliable from the repository's Chinese path.

## Gesture Input

Python MediaPipe bridge:

```powershell
cd ..\gesture_bridge
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
python mediapipe_udp_sender.py
```

Unity receives JSON frames from UDP `127.0.0.1:5005`, including:

- 21 landmarks
- handedness and score
- pinch center and index fingertip
- pinch distance and palm span
- finger extended state
- palm roll / pitch / yaw
- timestamp

Unity draws the live 21-point hand skeleton in the foreground during calibration, menus, and gameplay. If the skeleton is missing, the game has not received a fresh UDP hand frame yet.

The calibration panel includes a `Start Camera` button that can be clicked with the mouse when no hand frame is available yet. The live camera image is drawn as the bottom GUI layer, and the 21-point hand skeleton is drawn above it. The top-right `Exit` button is always visible. Levels also expose a top-right `Menu` button.

## Scenes And Build

Manual scene rebuild:

```text
Hand of God > Rebuild Level 01 Scene
```

Batch rebuild:

```powershell
& "E:\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.com" -batchmode -quit -projectPath "E:\同济\大二下\用户交互技术HCI\期末项目\unity\HandOfGodUnity" -executeMethod HandOfGod.EditorTools.LevelSceneGenerator.RebuildLevel01
```

Build Windows player from the repository root:

```powershell
.\Build-HandOfGod.bat
```

## Current Levels

- `Level 0: Gesture Lab`: hover-select Cube / Sphere / Cylinder, pinch to move, two-hand pinch to rotate and scale.
- `Level 1: First Path`: the ball rolls downhill by physics; the player pinches and moves the blocking box away; reaching the altar shows `PASS`.

The hand never directly pushes the ball. Gesture interaction acts on UI and mechanisms only.
