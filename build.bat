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

echo Verifying vigemclient.dll integrity...
set "EXPECTED_HASH=e024fa8d2b0ae2262dd1b33170bf66735ebc721541666cfb421ed8b63d985805"
for /f "skip=1 tokens=*" %%H in ('certutil -hashfile "%ROOT%vendor\vigemclient.dll" SHA256 2^>nul') do (
    if not defined ACTUAL_HASH set "ACTUAL_HASH=%%H"
)
if /i not "%ACTUAL_HASH%"=="%EXPECTED_HASH%" (
    echo [ERROR] vigemclient.dll SHA-256 mismatch!
    echo   Expected: %EXPECTED_HASH%
    echo   Got:      %ACTUAL_HASH%
    exit /b 1
)
echo   Hash OK: %ACTUAL_HASH%

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

