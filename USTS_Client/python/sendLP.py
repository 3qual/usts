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
    message = "Lorem Ipsum. Neque porro quisquam est qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit.... .There is no one who loves pain itself, who seeks after it and wants to have it, simply because it is pain.... Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec dictum leo augue, id blandit mauris interdum vel. Vestibulum quis quam at velit eleifend egestas id id ex. Vestibulum mauris ex, placerat vel lectus eget, tincidunt vulputate nulla. Proin laoreet leo eget velit fringilla ullamcorper. Praesent elementum efficitur ultrices. Curabitur eleifend vestibulum mattis. Proin turpis ligula, vestibulum ut iaculis in, aliquam consequat nulla. Curabitur odio ligula, auctor at faucibus vel, vulputate quis magna. Cras ornare urna bibendum, dictum urna quis, fermentum risus. Nullam gravida bibendum risus nec maximus. Curabitur sollicitudin finibus ligula sit amet semper. Aenean fringilla suscipit justo nec ullamcorper. Etiam interdum urna elit, aliquam elementum tortor dapibus ac. Vestibulum euismod lectus nisi, quis dictum ante dictum a. Ut blandit consequat commodo. Vivamus tincidunt pretium urna, eu egestas mauris. Vivamus pulvinar metus sed lorem mattis, quis varius est viverra. Nunc quis purus quam. Cras fringilla odio sit amet diam condimentum, at facilisis mauris sagittis. Etiam a tellus vitae sapien aliquet venenatis ac et felis. Morbi porta quam nunc, vel tincidunt mauris laoreet in. Praesent bibendum iaculis lorem, sit amet aliquet ligula rutrum id. Cras sit amet finibus felis. Pellentesque convallis eros in pulvinar euismod. Vivamus fringilla magna non sapien imperdiet, non rhoncus lectus gravida. Integer vitae interdum metus. Mauris non lacus et libero semper congue ac eget lorem. Vestibulum tincidunt vitae tellus ac tincidunt. Donec ac nibh viverra magna vehicula fringilla. Quisque pharetra vestibulum arcu, quis fermentum elit eleifend quis. In rutrum interdum pulvinar. Nulla et consequat ligula. Vestibulum dictum nisl ac mauris rhoncus consectetur. Donec vulputate felis augue, ac fermentum magna commodo non. Sed congue, metus sed hendrerit malesuada, neque arcu pretium magna, quis scelerisque erat justo a mi. Proin pellentesque vulputate massa non tincidunt. Proin cursus, metus id lacinia dictum, nunc nisi blandit purus, eget feugiat nisl quam non nibh. Nunc tempus, diam nec ultricies efficitur, lorem enim tincidunt dui, non semper felis sem id ante. Nam neque metus, sagittis eu rutrum in, venenatis non felis. Sed suscipit elit ac nisl tempor tincidunt. Pellentesque eu lorem ligula. Mauris id est eu felis convallis fringilla. Donec eu vulputate urna. Phasellus pellentesque dui aliquam, dapibus ex a, fermentum est. Sed porttitor nunc odio, ac aliquet turpis ornare porttitor. Vivamus porttitor consectetur tellus sit amet vulputate. Nullam vitae lacinia dolor. Maecenas quis rutrum nunc, vitae egestas lacus. Morbi odio nibh, pellentesque vitae commodo non, porttitor et magna. Pellentesque habitant morbi tristique senectus et netus et malesuada fames ac turpis egestas. Pellentesque nec porta dolor. In nec lorem a nisl finibus bibendum. Nunc aliquam porta purus, et ullamcorper tellus finibus ut. Quisque ac feugiat felis. Quisque aliquam leo odio. Etiam sit amet rhoncus risus, porttitor lacinia metus. Duis sollicitudin leo sit amet facilisis dictum. Class aptent taciti sociosqu ad litora torquent per conubia nostra, per inceptos himenaeos. Ut neque erat, interdum eu consequat et, imperdiet quis arcu. Nam cursus odio gravida libero egestas tempus. Praesent hendrerit interdum mi tempus dapibus. Class aptent taciti sociosqu ad litora torquent per conubia nostra, per inceptos."
    send_message(message, server_address)
