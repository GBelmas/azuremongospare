cd /d "%~dp0"
setlocal enableextensions enabledelayedexpansion
powershell -c "set-executionpolicy unrestricted"

cd startup

for /f %%p in ('powershell .\getLocalResource.ps1 MMSAgent') do set MMSAGENTPATH=%%p
for /f %%p in ('powershell .\getLocalResource.ps1 Python') do set PYTHONPATH=%%p

NET STOP MongoMMS

7za x 10gen-mms-agent-Kobojo.zip -y -o"%MMSAGENTPATH%" > mms-agent-output.log 2> mms-agent-error.log

echo y| cacls "%MMSAGENTPATH%" /grant everyone:f /t

cd /d "%MMSAGENTPATH%\mms-agent"
powershell .\mongommsinstall.ps1 "%PYTHONPATH%" >> mms-agent-output.log 2>> mms-agent-error.log

NET START MongoMMS

exit /b 0