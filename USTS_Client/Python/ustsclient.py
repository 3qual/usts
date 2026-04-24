import socket
import uuid
import time
import os
import base64
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

# Shared 256-bit key (must match server). Keep in sync or load from env.
SHARED_KEY = bytes.fromhex(
    "4a7d1e9f3b2c8a056e0f4d71c3b5a29e"
    "8f1c4e7a0d3b6f92a5c8e1d4b7f0a3c6"
)


def encrypt_message(message: str) -> str:
    """Encrypt a UTF-8 string with AES-256-GCM.
    Returns base64(nonce || ciphertext || tag).
    """
    nonce = os.urandom(12)          # 96-bit random nonce
    aesgcm = AESGCM(SHARED_KEY)
    ciphertext = aesgcm.encrypt(nonce, message.encode("utf-8"), None)
    payload = nonce + ciphertext    # nonce(12) + ct + tag(16)
    return base64.b64encode(payload).decode("ascii")


def send_message(message: str, server_address: tuple) -> None:
    encrypted = encrypt_message(message)
    print(f"[ENC] Payload length: {len(encrypted)} chars")

    message_id = str(uuid.uuid4())
    data = encrypted + "<EOF>"
    part_size = 500
    total_parts = (len(data) + part_size - 1) // part_size

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        print(f"Sending message with ID: {message_id}")
        for i in range(total_parts):
            part = data[i * part_size:(i + 1) * part_size]
            packet = f"{message_id}:{i}:{total_parts}:{part}".encode("utf-8")
            sock.sendto(packet, server_address)
            print(f"Sending part {i + 1} of {total_parts}")
            time.sleep(0.1)
        print("Message sent on client side\n")

        received_confirmations = set()
        while len(received_confirmations) < total_parts:
            response = sock.recv(1024).decode("utf-8")
            print(response)
            if "part" in response:
                received_confirmations.add(int(response.split("part")[1].split()[0]))

        while True:
            response = sock.recv(1024).decode("utf-8")
            print(response)
            if "---Response end---" in response:
                break
    except Exception as e:
        print(f"Error: {e}")
    finally:
        sock.close()


if __name__ == "__main__":
    ip = input("Enter server IP: ")
    port = 5367
    server_address = (ip, port)
    print("Enter 'exit' to quit")
    message = ""
    while message != "exit":
        message = input("Enter your message: ")
        if message != "exit":
            send_message(message, server_address)
