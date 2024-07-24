import socket
import uuid
import time


def send_message(message, server_address):
    message_id = str(uuid.uuid4())
    message += "<EOF>"
    part_size = 500
    total_parts = (len(message) + part_size - 1) // part_size
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    
    try:
        print(f"Sending message with ID: {message_id}")
        for i in range(total_parts):
            part = message[i * part_size:(i + 1) * part_size]
            packet = f"{message_id}:{i}:{total_parts}:{part}".encode('utf-8')
            sock.sendto(packet, server_address)
            print(f"Sending part {i + 1} of {total_parts}")
            time.sleep(0.1)
        print("Message sented on client side\n")

        received_confirmations = set()
        while len(received_confirmations) < total_parts:
            response = sock.recv(1024).decode('utf-8')
            print(response)
            if "part" in response:
                received_confirmations.add(int(response.split("part")[1].split()[0]))

        complete_message_received = set()
        while True:
            response = sock.recv(1024).decode('utf-8')
            print(response)
            if "successfully received on server side" in response:
                complete_message_received.add("received")
            if "successfully saved in file on server side" in response:
                complete_message_received.add("file")
            if "successfully written in DB on server side" in response:
                complete_message_received.add("db")
            if "---Response end---" in response:
                break
    except Exception as e:
        print(f"Error: {str(e)}")
    finally:
        sock.close()

if __name__ == "__main__":
    #ip = "172.100.8.10"
    ip = input("Enter server IP: ")
    server_address = (ip, 12345)
    message = input("Enter your message: ")
    send_message(message, server_address)
