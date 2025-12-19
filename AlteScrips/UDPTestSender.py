# udp_test_sender.py
import socket, json, time
UDP_IP = "127.0.0.1"
UDP_PORT = 20778
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

payload = {
    "CarSpeed": 123.45,
    "other": {"nested": {"SpeedKmh": 123.45}}
}
for i in range(5):
    payload["CarSpeed"] = i * 10.0
    data = json.dumps(payload)
    sock.sendto(data.encode("utf-8"), (UDP_IP, UDP_PORT))
    print("sent", data)
    time.sleep(0.5)
