cd /d "%~dp0"
setlocal enableextensions enabledelayedexpansion
if exist %SYSTEMDRIVE%\python27 (
    set PYTHONPATH=%SYSTEMDRIVE%\python27
) else (
    powershell -c "set-executionpolicy unrestricted"
    for /f %%p in ('powershell .\getLocalResource.ps1 Python') do set PYTHONPATH=%%p

    msiexec /i python-2.7.2.msi /qn TARGETDIR="!PYTHONPATH!" /log installPython-output.log
)
%PYTHONPATH%\python -c "import sys, os; sys.path.insert(0, os.path.abspath('setuptools-0.6c11-py2.7.egg')); from setuptools.command.easy_install import bootstrap; sys.exit(bootstrap())"
%PYTHONPATH%\scripts\easy_install pymongo > pymongo-output.log 2> pymongo-error.log

echo y| cacls "%PYTHONPATH%" /grant everyone:f /t

exit /b 0