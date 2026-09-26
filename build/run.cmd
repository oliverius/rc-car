@echo off
setlocal
set "carApp=%~dp0..\src\RcCar.App\bin\Release\net10.0-windows10.0.19041.0\RcCar.App.exe"
if not exist "%carApp%" (
  echo Build first: powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
  exit /b 2
)
"%carApp%" %*
exit /b %errorlevel%
