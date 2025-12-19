@echo off
REM Reseting all wind sensor values to -1

set BASEDIR=%~dp0

echo -1 > "%BASEDIR%Sensors\WindPercentageCenter(default).sensor"
echo -1 > "%BASEDIR%Sensors\WindPercentageLeft.sensor"
echo -1 > "%BASEDIR%Sensors\WindPercentageRight.sensor"

echo Wind sensors succesfully reset to -1.
timeout /t 3
