import socket
import time
import threading
import os
import urllib.request
import json
import hashlib
import sys
import shutil

# Set working directory to the script's location
os.chdir(os.path.dirname(os.path.abspath(__file__)))

# Hardcoded defaults (will be overridden by config if present)
GITHUB_REPO = "therealkarle/InternalWindMachine"
GITHUB_API_URL = f"https://api.github.com/repos/{GITHUB_REPO}/contents"
REQUIRED_CONFIG_VERSION = 2
SCRIPT_VERSION = 3  # Current version of this script

def get_file_hash(content):
    """Calculates a git-style blob hash for a given content string or bytes."""
    if isinstance(content, str):
        content = content.encode('utf-8')
    header = f"blob {len(content)}\0".encode('utf-8')
    return hashlib.sha1(header + content).hexdigest()

def check_for_updates(mode="ask"):
    """
    Checks GitHub for newer versions using a numeric version check.
    mode: "true" (auto), "ask" (prompt), or "false" (skip)
    """
    if mode == "false": return
    
    print(f"[{time.strftime('%H:%M:%S')}] Checking for updates (Version v{SCRIPT_VERSION})...")
    
    try:
        # 1. Fetch Version Info (we look for a version.json file on GitHub)
        version_url = f"https://raw.githubusercontent.com/{GITHUB_REPO}/main/version.json"
        try:
            with urllib.request.urlopen(version_url, timeout=5) as response:
                remote_info = json.loads(response.read().decode())
                remote_version = remote_info.get("version", 0)
        except:
            # Fallback: if no version.json exists, we can't do numeric check
            # For now, we'll assume remote is 0 if not found to prevent accidental downgrades
            remote_version = 0

        if remote_version < SCRIPT_VERSION:
            print(f"\n" + "!"*60)
            print(f" ATTENTION: DOWNGRADE DETECTED")
            print(f" Your local version (v{SCRIPT_VERSION}) is NEWER than the GitHub version (v{remote_version}).")
            print(" Continuing will replace your code with an OLDER version!")
            print("!"*60)
            
            confirm1 = input(f"Are you ABSOLUTELY sure you want to downgrade to v{remote_version}? (y/n): ").strip().lower()
            if confirm1 != 'y':
                print("Downgrade cancelled.")
                return
            
            confirm2 = input(f"FINAL WARNING: This will overwrite your files with OLD code. Proceed? (y/n): ").strip().lower()
            if confirm2 != 'y':
                print("Downgrade cancelled.")
                return
        
        elif remote_version == SCRIPT_VERSION:
            if mode != "true":
                print(f"[{time.strftime('%H:%M:%S')}] You are running the latest version (v{SCRIPT_VERSION}).")
            return
        
        else: # remote_version > SCRIPT_VERSION
            print(f"\n" + "*"*60)
            print(f" NEW UPDATE AVAILABLE: v{remote_version}")
            print(f" Current Version: v{SCRIPT_VERSION}")
            print("*"*60)

            if mode == "ask":
                choice = input(f"Do you want to download and install version v{remote_version}? (y/n): ").strip().lower()
                if choice != 'y':
                    print("Update skipped by user.")
                    return

        # 3. Proceed with Download using the existing SHA-based check for the actual files
        req = urllib.request.Request(GITHUB_API_URL, headers={'User-Agent': 'InternalWindMachine-Updater'})
        with urllib.request.urlopen(req, timeout=5) as response:
            remote_files = json.loads(response.read().decode())
            
        updated_count = 0
        files_to_update = ["InternalWindMachine.py", "InternalWindMachineStart.bat", "ResetSensorFiles.bat"]
        
        for remote_file in remote_files:
            filename = remote_file['name']
            if filename in files_to_update:
                remote_sha = remote_file['sha']
                local_path = filename
                
                # Check if local file exists and compare hashes
                needs_download = True
                if os.path.exists(local_path):
                    with open(local_path, 'rb') as f:
                        local_content = f.read()
                    if get_file_hash(local_content) == remote_sha:
                        needs_download = False
                
                if needs_download:
                    print(f"[{time.strftime('%H:%M:%S')}] Downloading {filename}...")
                    download_url = remote_file['download_url']
                    with urllib.request.urlopen(download_url) as dl_response:
                        new_content = dl_response.read()
                    
                    tmp_path = local_path + ".tmp"
                    with open(tmp_path, 'wb') as f:
                        f.write(new_content)
                    
                    if os.path.exists(local_path):
                        os.replace(tmp_path, local_path)
                    else:
                        os.rename(tmp_path, local_path)
                    updated_count += 1
                    
        if updated_count > 0:
            print(f"[{time.strftime('%H:%M:%S')}] Successfully installed v{remote_version}.")
            print(f"[{time.strftime('%H:%M:%S')}] Please restart the program to apply changes.")
            sys.exit(0)
            
    except Exception as e:
        print(f"[{time.strftime('%H:%M:%S')}] Update check failed: {e}")

def handle_config_mismatch(current_version):
    """Handles backup, update, and cleanup of an outdated config.txt."""
    print("\n" + "!"*60)
    print(" CONFIGURATION UPDATE REQUIRED")
    print(f" Your config.txt (v{current_version}) is outdated.")
    print(f" Required Version: v{REQUIRED_CONFIG_VERSION}")
    print("!"*60)
    
    # 1. Start Backup
    old_configs_dir = "OldConfigs"
    os.makedirs(old_configs_dir, exist_ok=True)
    
    base_backup_name = "configOld.txt"
    backup_path = os.path.join(old_configs_dir, base_backup_name)
    counter = 1
    while os.path.exists(backup_path):
        backup_path = os.path.join(old_configs_dir, f"configOld_{counter}.txt")
        counter += 1
    
    try:
        shutil.copy("config.txt", backup_path)
        print(f"\n[OK] Current config.txt backed up to: {backup_path}")
    except Exception as e:
        print(f"\n[ERROR] Could not create backup: {e}")
        return

    print(f"\nPlease check for the latest template at:\nhttps://github.com/{GITHUB_REPO}\n")
    
    # 2. Ask for Auto-Download
    choice = input("Should the new config.txt template be downloaded automatically? (y/n): ").strip().lower()
    if choice == 'y':
        try:
            download_url = f"https://raw.githubusercontent.com/{GITHUB_REPO}/main/config.txt"
            print(f"Downloading latest config.txt...")
            with urllib.request.urlopen(download_url) as response:
                new_content = response.read()
            with open("config.txt", "wb") as f:
                f.write(new_content)
            print("[OK] New config.txt downloaded. Please transfer your settings from the backup.")
        except Exception as e:
            print(f"[ERROR] Download failed: {e}")
    else:
        print("Please update your config.txt manually using the GitHub link.")

    # 3. Two-Step Deletion Confirmation
    print("\n" + "-"*40)
    del_choice = input("Do you want to delete the OLD backup config now? (y/n): ").strip().lower()
    if del_choice == 'y':
        print("\n!!! WARNING !!!")
        print("Only delete the backup if you have already transferred ALL your custom settings")
        print("to the new config.txt or if you didn't change anything.")
        
        confirm1 = input("Are you sure? (y/n): ").strip().lower()
        if confirm1 == 'y':
            confirm2 = input("ARE YOU ABSOLUTELY SURE? (y/n): ").strip().lower()
            if confirm2 == 'y':
                try:
                    os.remove(backup_path)
                    print(f"[OK] Backup {backup_path} deleted.")
                except Exception as e:
                    print(f"[ERROR] Could not delete backup: {e}")
            else:
                print("Deletion cancelled (2nd confirmation failed).")
        else:
            print("Deletion cancelled (1st confirmation failed).")
    else:
        print("Backup file kept for your safety.")
    
    print("\n" + "="*60)
    print(" UPDATE PROCESS FINISHED")
    print(" Please check your config.txt and restart the program.")
    print("="*60 + "\n")
    sys.exit(0)

# ###############################################################
#                  CONFIGURATION LOADING
# ###############################################################
def load_config():
    # Default values
    config = {
        "config_version": 0,
        "auto_update": "ask",
        "use_3d_wind": False,
        "host": "127.0.0.1",
        "port": 18082,
        "enable_center": True,
        "enable_left": True,
        "enable_right": True,
        "prop_center": "ShakeItWindPlugin.OutputCenter",
        "prop_left": "ShakeItWindPlugin.OutputLeft",
        "prop_right": "ShakeItWindPlugin.OutputRight",
        "github_repo": "therealkarle/InternalWindMachine",
        "github_api_url": "https://api.github.com/repos/therealkarle/InternalWindMachine/contents",
        "show_plugin_notice": True
    }
    
    config_path = "config.txt"
    if os.path.exists(config_path):
        try:
            with open(config_path, "r") as f:
                for line in f:
                    line = line.strip()
                    if not line or line.startswith("#"):
                        continue
                    if "=" in line:
                        key, value = line.split("=", 1)
                        key = key.strip().lower()
                        value = value.split("#")[0].strip()
                        
                        if key == "config_version":
                            try:
                                config["config_version"] = int(value)
                            except ValueError:
                                pass
                        elif key == "use_3d_wind":
                            config["use_3d_wind"] = (value.lower() == "true")
                        elif key == "auto_update":
                            val = value.lower()
                            if val in ["true", "false", "ask"]:
                                config["auto_update"] = val
                            else:
                                # Legacy support for boolean-like strings
                                if val == "true": config["auto_update"] = "true"
                                elif val == "false": config["auto_update"] = "false"
                        elif key == "host":
                            config["host"] = value
                        elif key == "port":
                            try:
                                config["port"] = int(value)
                            except ValueError:
                                pass
                        elif key == "enable_center":
                            config["enable_center"] = (value.lower() == "true")
                        elif key == "enable_left":
                            config["enable_left"] = (value.lower() == "true")
                        elif key == "enable_right":
                            config["enable_right"] = (value.lower() == "true")
                        elif key == "prop_center":
                            config["prop_center"] = value
                        elif key == "prop_left":
                            config["prop_left"] = value
                        elif key == "prop_right":
                            config["prop_right"] = value
                        elif key == "github_repo":
                            config["github_repo"] = value
                        elif key == "github_api_url":
                            config["github_api_url"] = value
                        elif key == "show_plugin_notice":
                            config["show_plugin_notice"] = (value.lower() == "true")
        except Exception as e:
            print(f"Error reading config.txt: {e}")

    # Logic: if 3D wind is disabled, force only Center and Disable Left/Right
    if not config["use_3d_wind"]:
        config["enable_center"] = True
        config["enable_left"] = False
        config["enable_right"] = False
    
    return config

# Initialize settings
config = load_config()
HOST = config["host"]
PORT = config["port"]
GITHUB_REPO = config["github_repo"]
GITHUB_API_URL = config["github_api_url"]

# Build properties map: { "SimHub.PropertyName": "SensorFile.sensor" }
PROPERTIES = {}
if config["enable_center"]:
    PROPERTIES[config["prop_center"]] = os.path.join("Sensors", "WindPercentageCenter(default).sensor")
if config["enable_left"]:
    PROPERTIES[config["prop_left"]] = os.path.join("Sensors", "WindPercentageLeft.sensor")
if config["enable_right"]:
    PROPERTIES[config["prop_right"]] = os.path.join("Sensors", "WindPercentageRight.sensor")

RECONNECT_DELAY = 5 
running = True 
# ###############################################################


def reset_sensors():
    """Resets all .sensor files in the Sensors directory to -1.00."""
    sensor_dir = "Sensors"
    if os.path.exists(sensor_dir):
        try:
            for filename in os.listdir(sensor_dir):
                if filename.lower().endswith(".sensor"):
                    file_path = os.path.join(sensor_dir, filename)
                    try:
                        with open(file_path, "w") as f:
                            f.write("-1.00")
                    except:
                        pass
            print(f"[{time.strftime('%H:%M:%S')}] All files in '{sensor_dir}' have been reset to -1.00.")
        except Exception as e:
            print(f"Error accessing Sensors directory: {e}")
    else:
        # Fallback for active properties if folder doesn't exist yet
        for file_path in PROPERTIES.values():
            try:
                os.makedirs(os.path.dirname(file_path), exist_ok=True)
                with open(file_path, "w") as f:
                    f.write("-1.00")
            except:
                pass

def user_input_listener():
    global running
    while running:
        try:
            cmd = input().strip().lower()
            if cmd == "stop":
                running = False
                break
            elif cmd == "reset":
                reset_sensors()
            elif cmd == "update":
                check_for_updates(mode="true")
        except:
            break

def connect_and_subscribe():
    """Attempts to establish connection and subscribe only to active properties."""
    client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM) 
    try:
        client_socket.connect((HOST, PORT))
        client_socket.settimeout(2.0)
        print(f"[{time.strftime('%H:%M:%S')}] Connected to SimHub at {HOST}:{PORT}")
        
        # Subscribe ONLY to properties used in this session
        for prop_name in PROPERTIES:
            subscribe_command = f'subscribe {prop_name}\n'
            client_socket.sendall(subscribe_command.encode('utf-8'))
            print(f"[{time.strftime('%H:%M:%S')}] Subscribed to: {prop_name}")

        return client_socket
    except Exception as e:
        print(f"[{time.strftime('%H:%M:%S')}] Connection failed: {e}")
        return None

def main():
    sock = None
    buffer = ""
    
    # Check for updates if enabled
    check_for_updates(mode=config.get("auto_update", "ask"))

    # Check config version
    if config.get("config_version", 0) < REQUIRED_CONFIG_VERSION:
        handle_config_mismatch(config.get("config_version", 0))

    # Ensure the Sensors directory exists
    os.makedirs("Sensors", exist_ok=True)
    print("\n" + "="*60)
    print(" INTERNAL WIND MACHINE")
    print(" by The Real Karle | https://linktr.ee/therealkarle")
    print("="*60)
    print(f" Status: ACTIVE (v{SCRIPT_VERSION})")
    print(f" Mode:   {'3D (Multi-Fan)' if config['use_3d_wind'] else 'Mono (Center Fan)'}")
    print(f" Active: {'Center ' if config['enable_center'] else ''}{'Left ' if config['enable_left'] else ''}{'Right ' if config['enable_right'] else ''}")
    print("="*60)
    
    # Optional Plugin Notice
    if config.get("show_plugin_notice", True):
        print(" NEW: A native SimHub Plugin is now available!")
        print(" Get it here: https://github.com/therealkarle/InternalWindMachine")
        print("="*60)
    print(" Type 'stop'   to close safely (or press Ctrl + C).")
    print(" Type 'reset'  to reset sensors to -1.")
    print(" Type 'update' to check for updates.")
    print("="*60 + "\n")

    t = threading.Thread(target=user_input_listener, daemon=True)
    t.start()

    while running:
        if sock is None:
            sock = connect_and_subscribe()
            if sock is None:
                time.sleep(RECONNECT_DELAY)
                continue

        try:
            data = sock.recv(4096).decode('utf-8')
            if not data:
                print(f"[{time.strftime('%H:%M:%S')}] Connection lost. Retrying...")
                sock.close()
                sock = None
                continue

            buffer += data
            while "\n" in buffer:
                line, buffer = buffer.split("\n", 1)
                line = line.strip()
                
                if line.startswith("Property "):
                    try:
                        parts = line.split(None, 3) 
                        if len(parts) >= 4:
                            prop_name = parts[1]
                            prop_value = parts[3]
                            
                            if prop_name in PROPERTIES and prop_value != "(null)":
                                fan_speed = float(prop_value)
                                sensor_path = PROPERTIES[prop_name]
                                
                                with open(sensor_path, "w") as f:
                                    f.write(f"{fan_speed:.2f}")
                    except:
                        pass
        except socket.timeout:
            continue
        except Exception:
            if sock: sock.close()
            sock = None
            
if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass

    # Cleanup: Reset sensors to -1
    print("\nShutting down...")
    reset_sensors()
    print("Cleanup complete. Exiting.")
    time.sleep(2)