cd /d "%~dp0"
powershell -c "set-executionpolicy unrestricted"

cd startup

7za x mongodb.zip -y -o"..\MongoDB" >> installMongoBin-output.log 2>> installMongoBin-error.log

exit /b 0