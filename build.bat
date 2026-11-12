@echo off
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] 64-bit .NET Framework csc.exe not found at "%CSC%".
    exit /b 1
)

set "DIST=%ROOT%dist"
if not exist "%DIST%" mkdir "%DIST%"

set "ICON_ARG="
if exist "%ROOT%assets\app.ico" (
    set "ICON_ARG=/win32icon:"%ROOT%assets\app.ico""
)

echo Building Twin2XInput.exe (x64)...
"%CSC%" /nologo /optimize+ /platform:x64 /target:winexe %ICON_ARG% ^
    /out:"%DIST%\Twin2XInput.exe" ^
    /r:System.Windows.Forms.dll ^
    /r:System.Drawing.dll ^
    "%ROOT%src\Program.cs" ^
    "%ROOT%src\Native\NativeMethods.cs" ^
    "%ROOT%src\Config\BridgeSettings.cs" ^
    "%ROOT%src\Hid\HidDeviceDiscovery.cs" ^
    "%ROOT%src\Bridge\ControllerBridge.cs" ^
    "%ROOT%src\UI\MainForm.cs"

if errorlevel 1 (
    echo [ERROR] Compilation failed.
    exit /b 1
)

copy /Y "%ROOT%vendor\vigemclient.dll" "%DIST%\vigemclient.dll" >nul
copy /Y "%ROOT%scripts\VibrationTester.ps1" "%DIST%\VibrationTester.ps1" >nul
copy /Y "%ROOT%THIRD_PARTY_NOTICES.md" "%DIST%\THIRD_PARTY_NOTICES.md" >nul

echo Build succeeded: %DIST%\Twin2XInput.exe
endlocal

