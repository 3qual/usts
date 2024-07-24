using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace USTS_Client;
class Program
{
    static void Main(string[] args)
    {
        Console.Write("Enter server IP: ");
        string ip = Console.ReadLine();
        int port = 5367;
        IPEndPoint serverAddress = new IPEndPoint(IPAddress.Parse(ip), port);
        Console.WriteLine("Enter 'exit' if you want to close the program");

        string message = "";
        while (message != "exit")
        {
            Console.Write("Enter your message: ");
            message = Console.ReadLine();
            if (message != "exit")
            {
                SendMessage(message, serverAddress);
            }
        }
    }

    static void SendMessage(string message, IPEndPoint serverAddress)
    {
        string messageId = Guid.NewGuid().ToString();
        message += "<EOF>";
        int partSize = 500;
        int totalParts = (message.Length + partSize - 1) / partSize;
        Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        try
        {
            Console.WriteLine($"Sending message with ID: {messageId}");
            for (int i = 0; i < totalParts; i++)
            {
                string part = message.Substring(i * partSize, Math.Min(partSize, message.Length - i * partSize));
                string packet = $"{messageId}:{i}:{totalParts}:{part}";
                byte[] data = Encoding.UTF8.GetBytes(packet);
                sock.SendTo(data, serverAddress);
                Console.WriteLine($"Sending part {i + 1} of {totalParts}");
                Thread.Sleep(100);
            }
            Console.WriteLine("Message sent on client side\n");

            HashSet<int> receivedConfirmations = new HashSet<int>();
            while (receivedConfirmations.Count < totalParts)
            {
                byte[] buffer = new byte[1024];
                int bytesReceived = sock.Receive(buffer);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                Console.WriteLine(response);
                if (response.Contains("part"))
                {
                    int partNumber = int.Parse(response.Split("part")[1].Split()[0]);
                    receivedConfirmations.Add(partNumber);
                }
            }

            HashSet<string> completeMessageReceived = new HashSet<string>();
            while (true)
            {
                byte[] buffer = new byte[1024];
                int bytesReceived = sock.Receive(buffer);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                Console.WriteLine(response);
                if (response.Contains("successfully received on server side"))
                {
                    completeMessageReceived.Add("received");
                }
                if (response.Contains("successfully saved in file on server side"))
                {
                    completeMessageReceived.Add("file");
                }
                if (response.Contains("successfully written in DB on server side"))
                {
                    completeMessageReceived.Add("db");
                }
                if (response.Contains("---Response end---"))
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
        }
        finally
        {
            sock.Close();
        }
    }
}
