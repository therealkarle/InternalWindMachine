@echo off
cd /d "%~dp0"

REM Opens the program in a new window.
REM This allows the batch file to close immediately, preventing the 'Terminate batch job (Y/N)?' prompt.
start "Internal Wind Machine" py InternalWindMachine.py

exit
