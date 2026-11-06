@echo off
setlocal
chcp 65001 >nul
where py >nul 2>nul
if not errorlevel 1 goto use_py
where python >nul 2>nul
if not errorlevel 1 goto use_python
echo [ERROR] Python 3 was not found.
echo Install Python 3.10 or newer and enable the PATH option, then run this file again.
pause
exit /b 9009

:use_py
py -3 "%~dp0simple_hackintosh.py" %*
goto finish

:use_python
python "%~dp0simple_hackintosh.py" %*

:finish
set "RC=%errorlevel%"
if "%~1"=="" pause
exit /b %RC%
