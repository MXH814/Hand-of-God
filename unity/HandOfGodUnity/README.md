# Hand of God Unity Prototype

This Unity project is the new main direction for the HCI gesture rolling-ball game.

## Version

- Recommended editor: Unity 2022.3 LTS
- Project path: `E:\同济\大二下\用户交互技术HCI\期末项目\unity\HandOfGodUnity`
- Recommended editor install path: `E:\Unity\Hub\Editor`

## Scene Generation

Open the project in Unity. The editor script creates `Assets/Scenes/Level01.unity` automatically if it is missing.

Manual rebuild:

```text
Hand of God > Rebuild Level 01 Scene
```

## Gameplay Prototype

- A top-down 3D temple maze is generated with Unity primitives.
- The golden ball moves through the maze using Rigidbody physics.
- Hand Roll / Pitch frames from UDP port `5005` drive the board tilt control.
- The hand does not collide with the ball directly.
- Keyboard fallback uses WASD / arrow keys for quick level testing.

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
