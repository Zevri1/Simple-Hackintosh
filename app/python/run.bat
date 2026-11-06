@echo off
setlocal
where py >nul 2>nul
if not errorlevel 1 goto use_py
where python >nul 2>nul
if not errorlevel 1 goto use_python
echo [ERROR] Python 3 was not found. Install Python 3.10 or newer and enable the PATH option.
exit /b 9009

:use_py
py -3 "%~dp0simple_hackintosh.py" %*
exit /b %errorlevel%

:use_python
python "%~dp0simple_hackintosh.py" %*
exit /b %errorlevel%
