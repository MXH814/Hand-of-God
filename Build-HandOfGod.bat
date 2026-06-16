@echo off
setlocal
chcp 65001 >nul

set "ROOT=%~dp0"
set "UNITY_EXE=E:\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.com"
set "UNITY_PROJECT=%ROOT%unity\HandOfGodUnity"
set "LOG=E:\Unity\hand-of-god-build.log"

if not exist "%UNITY_EXE%" (
  echo Unity.com was not found:
  echo "%UNITY_EXE%"
  pause
  exit /b 1
)

echo Building Hand of God Windows player...
"%UNITY_EXE%" -batchmode -quit -projectPath "%UNITY_PROJECT%" -executeMethod HandOfGod.EditorTools.GameBuildRunner.BuildWindowsPlayer -logFile "%LOG%"
if errorlevel 1 (
  echo Build failed. Log:
  echo "%LOG%"
  pause
  exit /b 1
)

echo Build complete:
echo "%UNITY_PROJECT%\Builds\Windows\HandOfGod.exe"
pause
