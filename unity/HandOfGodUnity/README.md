# Hand of God Unity Prototype

This Unity project is the new main direction for the HCI gesture rolling-ball game.

## Version

- Recommended editor: Unity 6.4 (6000.4.10f1)
- Project path: `E:\同济\大二下\用户交互技术HCI\期末项目\unity\HandOfGodUnity`
- Recommended editor install path: `E:\Unity\Hub\Editor`

## Scene Generation

Open the project in Unity 6.4. The editor script creates `Assets/Scenes/Level01.unity` automatically if it is missing.

Manual rebuild:

```text
Hand of God > Rebuild Level 01 Scene
```

Build local Windows player:

```text
Hand of God > Build Windows Player
```

Or run the root script:

```powershell
..\..\Build-HandOfGod.bat
```

## Gameplay Prototype

- A top-down 3D temple map is generated and saved into `Assets/Scenes/Level01.unity`.
- Level 01 is a single sloped road with one movable obstacle box and a glowing goal altar.
- The golden ball rolls downhill using Rigidbody physics.
- Pinch frames from UDP port `5005` pick up and move the obstacle box.
- The hand does not collide with the ball directly.
- Mouse drag fallback is available for quick level testing.

## Gesture Bridge

Run the Python bridge before entering Play mode:

```powershell
cd ..\gesture_bridge
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
python mediapipe_udp_sender.py
```

In the bridge preview:

- `C` calibrates the current pose as neutral.
- `Q` quits.

The bridge sends JSON frames such as:

```json
{
  "roll": 0.12,
  "pitch": -0.25,
  "pinchX": 0.5,
  "pinchY": 0.5,
  "confidence": 1.0,
  "pinch": false,
  "openPalm": true,
  "timestamp": 1780000000.0
}
```
