import socket
import time
import json 
import os # Importiert für die Pfadbehandlung
os.chdir(os.path.dirname(os.path.abspath(__file__)))


# ###############################################################
#                  KONFIGURATION
# ###############################################################
HOST = '127.0.0.1' 
PORT = 18082 
PROPERTY_NAME = "ShakeItWindPlugin.OutputCenter" 

# Der Pfad zur Datei, die FanControl liest
# BITTE PASSEN SIE DIESEN PFAD AN IHRE FANCONTROL-KONFIGURATION AN!
SENSOR_FILE_PATH = "WindPercentage.sensor" 
# Wiederverbindungsversuch-Intervall
RECONNECT_DELAY = 5 
# ###############################################################
wind_value_normalized = 0.67
fan_control_speed = int(wind_value_normalized * 100)


with open(SENSOR_FILE_PATH, "w") as f:
     f.write(str(fan_control_speed))