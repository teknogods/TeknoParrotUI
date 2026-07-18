@echo off
setlocal
cd /d "%~dp0"

> bridge-result.txt echo TEKNOPARROT_WINLATOR_BRIDGE_DIAGNOSTIC=1
>> bridge-result.txt echo STARTED=1

set BRIDGE_PORT=__BRIDGE_PORT__
if "%BRIDGE_PORT%"=="" (
    >> bridge-result.txt echo ERROR=BRIDGE_PORT_MISSING
    exit /b 2
)

start "" /b pipehelper64.exe TPWinlatorPipe64 127.0.0.1 %BRIDGE_PORT% TeknoParrot_JvsState64 64 "C:\teknoparrot-diagnostics\shared-page.bin" > pipehelper64-bridge.log 2>&1
timeout /t 1 /nobreak > nul
bridgeguest64.exe TPWinlatorPipe64 TeknoParrot_JvsState64 64 > bridgeguest64.log 2>&1
set GUEST64_EXIT=%ERRORLEVEL%
>> bridge-result.txt echo BRIDGEGUEST64_EXIT=%GUEST64_EXIT%
taskkill /f /im pipehelper64.exe > taskkill64.log 2>&1
timeout /t 1 /nobreak > nul

start "" /b pipehelper32.exe TPWinlatorPipe32 127.0.0.1 %BRIDGE_PORT% TeknoParrot_JvsState32 64 "C:\teknoparrot-diagnostics\shared-page.bin" > pipehelper32-bridge.log 2>&1
timeout /t 1 /nobreak > nul
bridgeguest32.exe TPWinlatorPipe32 TeknoParrot_JvsState32 64 > bridgeguest32.log 2>&1
set GUEST32_EXIT=%ERRORLEVEL%
>> bridge-result.txt echo BRIDGEGUEST32_EXIT=%GUEST32_EXIT%
taskkill /f /im pipehelper32.exe > taskkill32.log 2>&1

if not "%GUEST64_EXIT%"=="0" exit /b %GUEST64_EXIT%
if not "%GUEST32_EXIT%"=="0" exit /b %GUEST32_EXIT%
>> bridge-result.txt echo COMPLETE=1
exit /b 0
