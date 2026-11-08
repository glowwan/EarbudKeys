@echo off
rem Build EarbudKeys.exe on Windows with the in-box C# compiler (csc.exe).
rem No .NET SDK, no Visual Studio, no downloads: .NET Framework 4 ships
rem with Windows 10/11. Run this file by double-clicking it.
setlocal EnableDelayedExpansion
cd /d %~dp0

set CSC=
if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set CSC="%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if "!CSC!"=="" if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set CSC="%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if "!CSC!"=="" (
  echo [EarbudKeys] Cannot find csc.exe. This PC needs .NET Framework 4 (built into Windows 10/11).
  echo [EarbudKeys] Looked in %SystemRoot%\Microsoft.NET\Framework64\v4.0.30319 and Framework\v4.0.30319.
  pause
  exit /b 1
)

set ICON=
if exist app.ico set ICON=/win32icon:app.ico

echo [EarbudKeys] Compiling with !CSC! ...
!CSC! /nologo /target:winexe /platform:anycpu /optimize+ !ICON! /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:EarbudKeys.exe src\*.cs
if errorlevel 1 (
  echo [EarbudKeys] Compile FAILED. See the errors above.
  pause
  exit /b 1
)

echo.
echo [EarbudKeys] OK: EarbudKeys.exe
dir EarbudKeys.exe
pause
