@echo off
setlocal
chcp 65001 >nul

set "ROOT=%~dp0"
set "UNITY_PROJECT=%ROOT%unity\HandOfGodUnity"
set "GAME_EXE=%UNITY_PROJECT%\Builds\Windows\HandOfGod.exe"
set "BRIDGE_DIR=%ROOT%unity\gesture_bridge"
set "BRIDGE_PY=%BRIDGE_DIR%\mediapipe_udp_sender.py"
set "VENV_PY=%BRIDGE_DIR%\.venv\Scripts\python.exe"

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
    pushd "%BRIDGE_DIR%"
    python -m venv .venv
    popd
  )

  "%VENV_PY%" -c "import cv2, mediapipe, numpy" >nul 2>nul
  if errorlevel 1 (
    echo Installing gesture bridge dependencies. This may take a few minutes the first time.
    pushd "%BRIDGE_DIR%"
    "%VENV_PY%" -m pip install -r requirements.txt
    popd
  )
)

start "" "%GAME_EXE%"
exit /b 0
