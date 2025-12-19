


import serial
import time

# COM-Port anpassen (die Leseseite!)
PORT = "COM6"
BAUD = 115200


print("Port:" ,PORT) 
# Datei, die FanControl später liest
OUTPUT_FILE = r"carspeed.sensor"

ser = serial.Serial(PORT, BAUD, timeout=1)

print(ser)

while True:
    line = ser.readline().decode().strip()
    print(line)
    print("test")
    if line:
        # Erwartet: ein einfacher Wert z. B. "45" oder "12.3"
        with open(OUTPUT_FILE, "w") as f:
            f.write(line)
    time.sleep(0.05)