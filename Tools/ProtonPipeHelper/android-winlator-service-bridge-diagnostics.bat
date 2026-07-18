@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

> service-bridge-result.txt echo TEKNOPARROT_SERVICE_GUEST_DIAGNOSTIC=1
>> service-bridge-result.txt echo STARTED=1

set BRIDGE_PORT=%~1
set SHARED_PAGE=%~2
set SESSION_ID=%~3
set SESSION_TOKEN=%~4
set PIPE_NAME_64=%~5
set PIPE_NAME_32=%~6
if "%BRIDGE_PORT%"=="" (
    >> service-bridge-result.txt echo ERROR=BRIDGE_PORT_MISSING
    exit /b 2
)
if "%SHARED_PAGE%"=="" (
    >> service-bridge-result.txt echo ERROR=SHARED_PAGE_MISSING
    exit /b 2
)
set AUTH_MODE=0
if not "%SESSION_ID%"=="" set AUTH_MODE=1
if not "%SESSION_TOKEN%"=="" set AUTH_MODE=1
if not "%PIPE_NAME_64%"=="" set AUTH_MODE=1
if not "%PIPE_NAME_32%"=="" set AUTH_MODE=1
if "%AUTH_MODE%"=="1" if "%SESSION_ID%"=="" exit /b 2
if "%AUTH_MODE%"=="1" if "%SESSION_TOKEN%"=="" exit /b 2
if "%AUTH_MODE%"=="1" if "%PIPE_NAME_64%"=="" exit /b 2
if "%AUTH_MODE%"=="1" if "%PIPE_NAME_32%"=="" exit /b 2

if "%AUTH_MODE%"=="1" (
    set WRONG_TOKEN=0%SESSION_TOKEN:~1%
    if "%SESSION_TOKEN:~0,1%"=="0" set WRONG_TOKEN=1%SESSION_TOKEN:~1%
    start "" /b pipehelper64.exe pipe --name %PIPE_NAME_64% --host 127.0.0.1 --port %BRIDGE_PORT% --session %SESSION_ID% --token !WRONG_TOKEN! --shared-page TeknoParrot_ServiceJvsState64 4096 "%SHARED_PAGE%" > pipehelper64-reject.log 2>&1
    timeout /t 1 /nobreak > nul
    bridgeguest64.exe %PIPE_NAME_64% TeknoParrot_ServiceJvsState64 4096 > bridgeguest64-reject.log 2>&1
    set REJECT_GUEST_EXIT=!ERRORLEVEL!
    taskkill /f /im pipehelper64.exe > taskkill64-reject.log 2>&1
    if "!REJECT_GUEST_EXIT!"=="0" exit /b 9
    >> service-bridge-result.txt echo WRONG_TOKEN_REJECTED=1
    timeout /t 1 /nobreak > nul
)

if "%AUTH_MODE%"=="1" (
    start "" /b pipehelper64.exe pipe --name %PIPE_NAME_64% --host 127.0.0.1 --port %BRIDGE_PORT% --session %SESSION_ID% --token %SESSION_TOKEN% --shared-page TeknoParrot_ServiceJvsState64 4096 "%SHARED_PAGE%" > pipehelper64-service.log 2>&1
) else (
    start "" /b pipehelper64.exe TPWinlatorServicePipe64 127.0.0.1 %BRIDGE_PORT% TeknoParrot_ServiceJvsState64 4096 "%SHARED_PAGE%" > pipehelper64-service.log 2>&1
    set PIPE_NAME_64=TPWinlatorServicePipe64
)
timeout /t 1 /nobreak > nul
bridgeguest64.exe %PIPE_NAME_64% TeknoParrot_ServiceJvsState64 4096 > bridgeguest64-service.log 2>&1
set GUEST64_EXIT=%ERRORLEVEL%
>> service-bridge-result.txt echo BRIDGEGUEST64_EXIT=%GUEST64_EXIT%
taskkill /f /im pipehelper64.exe > taskkill64-service.log 2>&1
timeout /t 1 /nobreak > nul

if "%AUTH_MODE%"=="1" (
    start "" /b pipehelper32.exe pipe --name %PIPE_NAME_32% --host 127.0.0.1 --port %BRIDGE_PORT% --session %SESSION_ID% --token %SESSION_TOKEN% --shared-page TeknoParrot_ServiceJvsState32 4096 "%SHARED_PAGE%" > pipehelper32-service.log 2>&1
) else (
    start "" /b pipehelper32.exe TPWinlatorServicePipe32 127.0.0.1 %BRIDGE_PORT% TeknoParrot_ServiceJvsState32 4096 "%SHARED_PAGE%" > pipehelper32-service.log 2>&1
    set PIPE_NAME_32=TPWinlatorServicePipe32
)
timeout /t 1 /nobreak > nul
bridgeguest32.exe %PIPE_NAME_32% TeknoParrot_ServiceJvsState32 4096 > bridgeguest32-service.log 2>&1
set GUEST32_EXIT=%ERRORLEVEL%
>> service-bridge-result.txt echo BRIDGEGUEST32_EXIT=%GUEST32_EXIT%
taskkill /f /im pipehelper32.exe > taskkill32-service.log 2>&1

if not "%GUEST64_EXIT%"=="0" exit /b %GUEST64_EXIT%
if not "%GUEST32_EXIT%"=="0" exit /b %GUEST32_EXIT%
>> service-bridge-result.txt echo COMPLETE=1
exit /b 0
