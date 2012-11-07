cd /d "%~dp0"
powershell -c "set-executionpolicy unrestricted"
powershell .\downloadstuff.ps1 >> downloadstuff-output.log 2>> downloadstuff-error.log
