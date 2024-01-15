@echo off

rem Stop and delete the Aria2Service service.
net stop Aria2Service
sc delete Aria2Service
