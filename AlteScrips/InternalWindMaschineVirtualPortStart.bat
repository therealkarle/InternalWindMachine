@echo off
:: Batch-Datei wird im selben Ordner ausgeführt wie das Python-Script
cd /d "%~dp0"

:: Conda initialisieren (damit py.exe & python.exe korrekt verfügbar sind)
call "%UserProfile%\anaconda3\Scripts\activate.bat"

:: gewünschtes Environment aktivieren (hier: base)
call conda activate PythonForFinace

:: Python-Script starten (Datei liegt im selben Ordner)
py.exe "%~InternalWindMachineVirtualPort"

pause
