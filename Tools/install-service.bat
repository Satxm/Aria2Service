@echo off

rem Find Aria2Service root directory
for %%I in ("%~dp0") do if exist "%%~fI\Aria2Service.exe" set "SERVICE_BIN=%%~fI\Aria2Service.exe"
for %%I in ("%~dp0\..") do if exist "%%~fI\Aria2Service.exe" set "SERVICE_BIN=%%~fI\Aria2Service.exe"

set SERVICE_NAME=Aria2Service
set SERVICE_DNAME="Aria2 Service"

rem Set service to demand start. It will be changed to auto later if the user selected that option.
set SERVICE_START_TYPE=demand

rem Check if Aria2Service already exists
sc qc %SERVICE_NAME% > nul 2>&1
if %ERRORLEVEL%==0 (
    rem Stop the existing service if running
    net stop %SERVICE_NAME%

    rem Reconfigure the existing service
    set SC_CMD=config
) else (
    rem Create a new service
    set SC_CMD=create
)

rem Run the sc command to create/reconfigure the service
sc %SC_CMD% %SERVICE_NAME% binPath= %SERVICE_BIN% start= %SERVICE_START_TYPE% DisplayName= %SERVICE_DNAME%

rem Set the description of the service
sc description %SERVICE_NAME% "Aria2 Service for Windows."

rem Start the new service
net start %SERVICE_NAME%
