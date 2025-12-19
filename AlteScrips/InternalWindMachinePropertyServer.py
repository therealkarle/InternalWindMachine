import socket
import time
import threading
import os # Import for path handling
os.chdir(os.path.dirname(os.path.abspath(__file__)))


# ###############################################################
#                  CONFIGURATION
# ###############################################################
HOST = '127.0.0.1' 
PORT = 18082 
PROPERTY_NAME = "ShakeItWindPlugin.OutputCenter" 

# Path to the file that FanControl reads
# PLEASE ADJUST THIS PATH TO YOUR FANCONTROL CONFIGURATION!
SENSOR_FILE_PATH = "WindPercentage.sensor" 
# Reconnection attempt interval in seconds
RECONNECT_DELAY = 5 
running = True
# ###############################################################


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

def connect_and_subscribe():
    """Attempts to establish a connection to the Property Server and subscribe."""
    
    client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM) 
    
    try:
        client_socket.connect((HOST, PORT))
        client_socket.settimeout(2.0)
        print(f"[{time.strftime('%H:%M:%S')}] Connected to SimHub Property Server at {HOST}:{PORT}")
        subscribe_command = f'subscribe {PROPERTY_NAME}\n'
        
        client_socket.sendall(subscribe_command.encode('utf-8'))
        print(f"[{time.strftime('%H:%M:%S')}] Subscribed to property: {PROPERTY_NAME}")

        return client_socket
    
    except ConnectionRefusedError:
        print(f"[{time.strftime('%H:%M:%S')}] Error: Connection to SimHub refused. Is the Property Server running on port {PORT}?")
        return None
    except Exception as e:
        print(f"[{time.strftime('%H:%M:%S')}] Error connecting or subscribing: {e}")
        return None

def main():
    sock = None
    buffer = ""
    
    # Ensure the directory exists
    try:
        os.makedirs(os.path.dirname(SENSOR_FILE_PATH) or '.', exist_ok=True)
    except Exception as e:
        print(f"Error creating directory: {e}")
        return

    print("\n" + "="*60)
    print("HOW TO CLOSE:")
    print("Type 'stop' and press ENTER to close the program safely.")
    print("OR press Ctrl+C.")
    print("IMPORTANT: This is required to reset the sensors to -1.")
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
            # Receive data
            data = sock.recv(4096).decode('utf-8')
            
            if not data:
                # Connection closed (e.g. SimHub terminated)
                print(f"[{time.strftime('%H:%M:%S')}] Connection closed. Retrying in {RECONNECT_DELAY}s...")
                sock.close()
                sock = None
                continue

            buffer += data
            
            # Process buffer line by line
            while "\n" in buffer:
                line, buffer = buffer.split("\n", 1)
                line = line.strip()
                
                # Print line for debugging (commented out to reduce spam)
                # if line:
                #     print(f"[{time.strftime('%H:%M:%S')}] Received: {line}")
                
                # Format: "Property <name> <type> <value>"
                # Example: "Property ShakeItWindPlugin.OutputCenter double 0.75"
                if line.startswith("Property "):
                    try:
                        parts = line.split(None, 3)  # Split into max 4 parts: "Property", name, type, value
                        
                        if len(parts) >= 4:
                            prop_name = parts[1]
                            prop_type = parts[2]
                            prop_value = parts[3]
                            
                            # Check if it's our property
                            if prop_name == PROPERTY_NAME:
                                # Skip (null) values
                                if prop_value == "(null)":
                                    # print(f"[{time.strftime('%H:%M:%S')}] Property is null, waiting for value...")
                                    # with open(SENSOR_FILE_PATH, "w") as f:
                                    #    f.write("-1.0")
                                    continue
                                
                                # Convert to float
                                fanSpeedPercentage = float(prop_value)
                                
                                # Write value to sensor file
                                with open(SENSOR_FILE_PATH, "w") as f:
                                    f.write(str(fanSpeedPercentage))
                                
                                # Print update (commented out to reduce spam)
                                # print(f"[{time.strftime('%H:%M:%S')}] Wind strength updated: {fanSpeedPercentage}")
                                
                    except ValueError as e:
                        print(f"[{time.strftime('%H:%M:%S')}] Could not convert value to number: {e}")
                    except Exception as e:
                        print(f"[{time.strftime('%H:%M:%S')}] Error processing message: {e}")

        except socket.timeout:
            # No data received for 2 seconds -> do NOT reset sensor to -1 automatically
            # This prevents flickering if data stream pauses briefly
            continue

        except socket.error as e:
            # Errors like timeout or connection interrupted
            print(f"[{time.strftime('%H:%M:%S')}] Socket error ({e}). Retrying in {RECONNECT_DELAY}s...")
            if sock:
                sock.close()
            sock = None
        except Exception as e:
            print(f"[{time.strftime('%H:%M:%S')}] Unexpected error: {e}")
            if sock:
                sock.close()
            sock = None
            
if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass

    # Cleanup / Shutdown sequence
    print("\nStop command processed.")
    try:
        with open(SENSOR_FILE_PATH, "w") as f:
            f.write("-1.0")
        print(f"[{time.strftime('%H:%M:%S')}] User stop: Sensor manually reset to -1.0")
    except Exception as e:
        print(f"Error resetting sensor: {e}")

    print("Sensor has been reset to -1.")
    print("Closing program in 3 seconds...")
    time.sleep(3)