@echo off
rem Going Public Server: neues Passwort fuer die Weboberflaeche - new password for the web interface
cd /d "%~dp0"
"%~dp0GoingPublicServer.exe" reset-password %*
