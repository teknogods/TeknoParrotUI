@echo off
setlocal
cd /d "%~dp0"

> result.txt echo TEKNOPARROT_WINLATOR_DIAGNOSTIC=1
>> result.txt echo STARTED=1

pipehelper64.exe > pipehelper64.log 2>&1
>> result.txt echo PIPEHELPER64_EXIT=%ERRORLEVEL%

pipehelper32.exe > pipehelper32.log 2>&1
>> result.txt echo PIPEHELPER32_EXIT=%ERRORLEVEL%

>> result.txt echo COMPLETE=1
exit /b 0
