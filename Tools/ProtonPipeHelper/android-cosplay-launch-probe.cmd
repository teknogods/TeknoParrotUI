@echo off
setlocal

rem Keep the OpenParrot runtime in its canonical directory while allowing
rem Winlator's experimental WoW64 loader to resolve early native imports.
set "TP_ANDROID_ROOT=D:\TeknoParrotAndroidTest"
set "PATH=%TP_ANDROID_ROOT%\OpenParrotWin32;%PATH%"

cd /d "%TP_ANDROID_ROOT%"
"%TP_ANDROID_ROOT%\OpenParrotWin32\OpenParrotLoader.exe" ".\OpenParrotWin32\OpenParrot" "D:\TeknoParrotGames\3D Cosplay Mahjong - 401300\game.exe" > "%TP_ANDROID_ROOT%\cosplay-loader-20260716.log" 2>&1
exit /b %ERRORLEVEL%
