@echo off
setlocal
chcp 65001 >nul

set "ROOT=%~dp0"
set "UNITY_PROJECT=%ROOT%unity\HandOfGodUnity"
set "GAME_EXE=%UNITY_PROJECT%\Builds\Windows\HandOfGod.exe"
set "BUILD_STAMP=%UNITY_PROJECT%\Builds\Windows\last-source-build.stamp"
set "BRIDGE_DIR=%ROOT%unity\gesture_bridge"
set "BRIDGE_PY=%BRIDGE_DIR%\mediapipe_udp_sender.py"
set "BRIDGE_RUNTIME=D:\Unity\HandOfGodGestureBridge"
set "VENV_PY=%BRIDGE_RUNTIME%\.venv\Scripts\python.exe"
set "PYTHON_CMD=py -3.11"
set "PYTHON_AVAILABLE=1"

if not exist "%GAME_EXE%" (
  echo HandOfGod.exe was not found.
  echo Expected: "%GAME_EXE%"
  echo Building it now...
  call "%ROOT%Build-HandOfGod.bat"
  if errorlevel 1 exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$stamp='%BUILD_STAMP%'; $project='%UNITY_PROJECT%'; if (!(Test-Path -LiteralPath $stamp)) { exit 1 }; $stampTime=(Get-Item -LiteralPath $stamp).LastWriteTimeUtc; $paths=@('Assets\Scripts','Assets\Editor','Assets\Scenes','ProjectSettings\EditorBuildSettings.asset') | ForEach-Object { Join-Path $project $_ }; $newer=Get-ChildItem -LiteralPath $paths -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTimeUtc -gt $stampTime } | Select-Object -First 1; if ($newer) { exit 1 } else { exit 0 }" >nul 2>nul
if errorlevel 2 (
  echo Build status could not be checked.
) else if errorlevel 1 (
  echo Project files are newer than the last successful build.
  echo Rebuilding before launch so the latest levels are included...
  call "%ROOT%Build-HandOfGod.bat"
  if errorlevel 1 exit /b 1
)

where py >nul 2>nul
if errorlevel 1 (
  echo Python launcher was not found. The game will start, but the gesture bridge cannot run.
  echo Install Python 3.11 or repair the Python launcher.
  pause
  set "PYTHON_AVAILABLE=0"
)

if "%PYTHON_AVAILABLE%"=="1" (
  %PYTHON_CMD% --version >nul 2>nul
  if errorlevel 1 (
    echo Python 3.11 was not found.
    echo Install 64-bit Python 3.11 and make sure "py -3.11 --version" works.
    pause
    set "PYTHON_AVAILABLE=0"
  )
)

if "%PYTHON_AVAILABLE%"=="1" if exist "%BRIDGE_PY%" (
  if not exist "%VENV_PY%" (
    echo Gesture bridge virtual environment was not found.
    echo Creating it now. This may take a few minutes the first time.
    if not exist "%BRIDGE_RUNTIME%" mkdir "%BRIDGE_RUNTIME%"
    pushd "%BRIDGE_RUNTIME%"
    %PYTHON_CMD% -m venv .venv
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
