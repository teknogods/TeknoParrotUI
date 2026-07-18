@echo off
setlocal

rem Winlator's experimental WoW64 resolves the game's imported DLLs while
rem CreateProcess(CREATE_SUSPENDED) is still returning to OpenParrotLoader.
rem Keep iDmacDrv32.dll in the canonical OpenParrotWin32 directory and expose
rem that directory only through this test process's inherited Windows PATH.
set "TP_ANDROID_ROOT=D:\TeknoParrotAndroidTest"
set "PATH=%TP_ANDROID_ROOT%\OpenParrotWin32;%PATH%"

cd /d "%TP_ANDROID_ROOT%"
"%TP_ANDROID_ROOT%\OpenParrotWin32\OpenParrotLoader.exe" ".\OpenParrotWin32\OpenParrot" "D:\TeknoParrotGames\Rastan Saga[401500]\game.exe" > "%TP_ANDROID_ROOT%\rastan-loader-20260716.log" 2>&1
exit /b %ERRORLEVEL%
