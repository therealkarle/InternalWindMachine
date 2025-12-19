# simhub_udp_debug.py
# debug-version: zeigt raw packets, robustes parsing, logfile
import socket
import json
import os
import time
import threading
from pathlib import Path
from datetime import datetime
os.chdir(os.path.dirname(os.path.abspath(__file__)))

#%% =========== CONFIG ===========
UDP_IP = "127.0.0.1"
UDP_PORT = 20778
SENSOR_FILE = r"carspeed.sensor"
LOG_FILE = r"debug.log"
# possible keys to try (will try these in order, then search recursively)
POSSIBLE_KEYS = ["CarSpeed", "SpeedKmh", "Speed", "car_speed", "carSpeed"]
running = True

def user_input_listener():
    global running
    while running:
        try:
            i = input()
            if i.strip().lower() == "stop":
                running = False
                break
        except:
            break

#%% ==============================
#defining funktions

def log(msg):
    t = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    line = f"[{t}] {msg}"
    print(line)
    try:
        with open(LOG_FILE, "a", encoding="utf-8") as f:
            f.write(line + "\n")
    except Exception:
        pass

def safe_write(path, text):
    tmp = path + ".tmp"
    try:
        with open(tmp, "w", encoding="utf-8") as f:
            f.write(text)
        os.replace(tmp, path)
    except Exception as e:
        log(f"ERROR writing sensor file: {e}")

def find_speed_in_obj(obj):
    # direct keys
    if isinstance(obj, dict):
        for k in POSSIBLE_KEYS:
            if k in obj:
                try:
                    return float(obj[k])
                except Exception:
                    pass
    # recursive search
    stack = [obj]
    while stack:
        node = stack.pop()
        if isinstance(node, dict):
            for k, v in node.items():
                if isinstance(v, (int, float)):
                    # heuristic: if key contains 'speed' or 'km' accept it
                    if "speed" in k.lower() or "km" in k.lower():
                        try:
                            return float(v)
                        except:
                            pass
                stack.append(v)
        elif isinstance(node, list):
            for v in node:
                stack.append(v)
    return None

def main():
    log(f"Starting UDP listener on {UDP_IP}:{UDP_PORT}")
    print(f"Listening on UDP {UDP_IP}:{UDP_PORT}")
    print(f"Writing sensor data to: {SENSOR_FILE}")
    Path(os.path.dirname(SENSOR_FILE)).mkdir(parents=True, exist_ok=True)
    Path(os.path.dirname(LOG_FILE)).mkdir(parents=True, exist_ok=True)

    print("\n" + "="*60)
    print("HOW TO CLOSE:")
    print("Type 'stop' and press ENTER to close the program safely.")
    print("OR press Ctrl+C.")
    print("IMPORTANT: This is required to reset the sensors to -1.")
    print("="*60 + "\n")

    t = threading.Thread(target=user_input_listener, daemon=True)
    t.start()


    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.settimeout(2.0)
    try:
        sock.bind((UDP_IP, UDP_PORT))
    except Exception as e:
        log(f"ERROR binding UDP socket: {e}")
        return

    last_written = None
    while running:
        try:
            data, addr = sock.recvfrom(131072)  # big buffer
            try:
                decoded = data.decode("utf-8", errors="replace").strip()
            except Exception as e:
                log(f"Error decoding packet: {e}")
                continue

            # quick log of raw packet (first 500 chars)
            raw_preview = decoded[:500].replace("\n", "\\n")
            log(f"Packet from {addr}: {raw_preview}")

            # try parse json
            try:
                parsed = json.loads(decoded)
            except Exception as e:
                log(f"JSON parse error: {e}")
                continue

            # find speed
            speed = None
            # try direct keys first
            for key in POSSIBLE_KEYS:
                if isinstance(parsed, dict) and key in parsed:
                    try:
                        speed = float(parsed[key])
                        break
                    except:
                        pass
            if speed is None:
                speed = find_speed_in_obj(parsed)

            if speed is None:
                # log("No speed value found in packet.")
                speed = -1.0

            out = f"{speed:.2f}\n"
            if out != last_written:
                safe_write(SENSOR_FILE, out)
                last_written = out
                log(f"Wrote sensor value: {out.strip()}")
        except socket.timeout:
            speed = -1.0
            out = f"{speed:.2f}\n"
            if out != last_written:
                safe_write(SENSOR_FILE, out)
                last_written = out
        except Exception as e:
            log(f"Loop exception: {e}")
            time.sleep(0.2)


#%% Starts the program
if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
    
    # Cleanup / Shutdown sequence
    print("\nStop command processed.")
    safe_write(SENSOR_FILE, "-1.00\n")
    log("User stop: Sensor manually reset to -1.00")
    print("Sensor has been reset to -1.")
    print("Closing program in 3 seconds...")
    time.sleep(3)
