# Hand of God Unity Project

This directory contains the Unity 6.4 game project for **Hand of God**.

For the complete project documentation, including gameplay, gesture design, Python bridge details, level design, build steps, validation checklist, and open-source references, read the repository root:

```text
..\..\README.md
```

## Project Role

The Unity project is responsible for:

- Rendering the embedded camera background.
- Drawing the foreground 21-point hand skeleton.
- Running calibration and gesture-only UI.
- Hosting Level 0 gesture tutorial and Level 1 `Trial of the Moving Path`.
- Handling physics, puzzle mechanisms, HUD feedback, pass/fail states, and Python bridge lifecycle.

## Main Files

- `Assets/Scenes/Level01.unity`
- `Assets/Scripts/GameBootstrap.cs`
- `Assets/Scripts/GestureGameController.cs`
- `Assets/Scripts/GestureFrame.cs`
- `Assets/Scripts/GestureUdpReceiver.cs`
- `Assets/Scripts/CameraFrameReceiver.cs`
- `Assets/Scripts/BallController.cs`
- `Assets/Editor/LevelSceneGenerator.cs`
- `Assets/Editor/GameBuildRunner.cs`

## Build

Run from the repository root:

```powershell
.\Build-HandOfGod.bat
```
