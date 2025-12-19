import socket

UDP_IP = "127.0.0.1"
UDP_PORT = 20778   # Target Port aus SimHub!

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((UDP_IP, UDP_PORT))

print(f"Listening on {UDP_IP}:{UDP_PORT}...")

while True:
    data, addr = sock.recvfrom(2048)
    print(f"From {addr}: {data}")
