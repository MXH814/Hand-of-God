@echo off
setlocal
chcp 65001 >nul

set "ROOT=%~dp0"
set "UNITY_PROJECT=%ROOT%unity\HandOfGodUnity"
set "GAME_EXE=%UNITY_PROJECT%\Builds\Windows\HandOfGod.exe"
set "BRIDGE_DIR=%ROOT%unity\gesture_bridge"
set "BRIDGE_PY=%BRIDGE_DIR%\mediapipe_udp_sender.py"
set "BRIDGE_RUNTIME=E:\Unity\HandOfGodGestureBridge"
set "VENV_PY=%BRIDGE_RUNTIME%\.venv\Scripts\python.exe"

if not exist "%GAME_EXE%" (
  echo HandOfGod.exe was not found.
  echo Expected: "%GAME_EXE%"
  echo Build it once with Build-HandOfGod.bat, then run this file again.
  pause
  exit /b 1
)

where python >nul 2>nul
if errorlevel 1 (
  echo Python was not found in PATH. The game will start, but the gesture bridge cannot run.
  echo Install Python or start the gesture bridge manually later.
  pause
) else if exist "%BRIDGE_PY%" (
  if not exist "%VENV_PY%" (
    echo Gesture bridge virtual environment was not found.
    echo Creating it now. This may take a few minutes the first time.
    if not exist "%BRIDGE_RUNTIME%" mkdir "%BRIDGE_RUNTIME%"
    pushd "%BRIDGE_RUNTIME%"
    python -m venv .venv
    popd
  )

  "%VENV_PY%" -c "import cv2, mediapipe as mp, numpy; raise SystemExit(0 if hasattr(mp, 'solutions') else 1)" >nul 2>nul
  if errorlevel 1 (
    echo Installing compatible gesture bridge dependencies. This may take a few minutes the first time.
    pushd "%BRIDGE_DIR%"
    "%VENV_PY%" -m pip install --force-reinstall -r requirements.txt
    popd
  )

)

start "" "%GAME_EXE%" --gesture-bridge-dir "%BRIDGE_DIR%" --gesture-python "%VENV_PY%"
exit /b 0
