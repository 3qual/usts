// Connecting necessary namespaces
using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using MongoDB.Driver;
using MongoDB.Bson;

namespace USTS_server
{
    // ─── AES-256-GCM decryption helper ───────────────────────────────────────
    public static class CryptoHelper
    {
        // Shared 256-bit key — MUST match the client's SHARED_KEY.
        // In production, load this from an environment variable or secrets store.
        private static readonly byte[] SharedKey = Convert.FromHexString(
            "4a7d1e9f3b2c8a056e0f4d71c3b5a29e"
            + "8f1c4e7a0d3b6f92a5c8e1d4b7f0a3c6"
        );

        /// <summary>
        /// Decrypts a base64-encoded payload produced by the Python client.
        /// Layout: nonce(12 bytes) || ciphertext || GCM-tag(16 bytes)
        /// </summary>
        public static string Decrypt(string base64Payload)
        {
            byte[] payload    = Convert.FromBase64String(base64Payload);
            byte[] nonce      = payload[..12];
            byte[] ciphertext = payload[12..];

            using var aes = new AesGcm(SharedKey, AesGcm.TagByteSizes.MaxSize); // tag = 16 bytes
            byte[] tag       = ciphertext[^16..];
            byte[] encrypted = ciphertext[..^16];
            byte[] plaintext = new byte[encrypted.Length];

            aes.Decrypt(nonce, encrypted, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
    }

    // ─── MongoDB handler ──────────────────────────────────────────────────────
    public class MongoDBHandler
    {
        private static readonly string connectionString = "mongodb://username:password@localhost:27017/database_name";
        private static readonly string databaseName     = "database_name";
        private static readonly string collectionName   = "messages";

        public static async Task<string> PostValueAsync(string message, string messageId)
        {
            try
            {
                var client     = new MongoClient(connectionString);
                var database   = client.GetDatabase(databaseName);
                var collection = database.GetCollection<BsonDocument>(collectionName);

                if (!await CollectionExistsAsync(database, collectionName))
                    await database.CreateCollectionAsync(collectionName);

                var document = new BsonDocument
                {
                    { "messageId", messageId },
                    { "message",   message   }
                };
                await collection.InsertOneAsync(document);
                return $"Message with id - {messageId} successfully saved into DB on server side";
            }
            catch (Exception ex)
            {
                return $"Message with id - {messageId} has !ERROR! with writing into DB on server side! \nException: {ex.GetBaseException()}";
            }
        }

        private static async Task<bool> CollectionExistsAsync(IMongoDatabase database, string name)
        {
            var filter      = new BsonDocument("name", name);
            var collections = await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter });
            return await collections.AnyAsync();
        }
    }

    // ─── UDP server ───────────────────────────────────────────────────────────
    class UDPserver
    {
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<int, string>>
            messageFragments = new();

        public static async Task ListenAsync()
        {
            int PORT = 5367;
            if (!IsPortAvailable(PORT))
            {
                LogMessage("Port already occupied!");
                return;
            }

            using UdpClient udpClient = new UdpClient(PORT);
            LogMessage($"Listening on port {PORT}\n\n\n");

            while (true)
            {
                var result = await udpClient.ReceiveAsync();
                _ = Task.Run(() => HandleMessageAsync(udpClient, result));
            }
        }

        private static async Task HandleMessageAsync(UdpClient udpClient, UdpReceiveResult receivedResult)
        {
            var receivedData = Encoding.UTF8.GetString(receivedResult.Buffer);
            string[] parts   = receivedData.Split(new char[] { ':' }, 4);

            if (parts.Length != 4)
            {
                LogMessage("Received data is not in the correct format.");
                return;
            }

            string messageId  = parts[0];
            int    partIndex  = int.Parse(parts[1]);
            int    totalParts = int.Parse(parts[2]);
            string messagePart = parts[3];

            if (!messageFragments.ContainsKey(messageId))
            {
                messageFragments[messageId] = new ConcurrentDictionary<int, string>();

                string startMessage = "---Response start---";
                LogMessage(startMessage);
                await SendResponseAsync(udpClient, startMessage, receivedResult.RemoteEndPoint);

                string serverTimeMessage = DateTime.Now.ToString("dd.MM.yyyy-HH:mm:ss zzz");
                LogMessage(serverTimeMessage);
                await SendResponseAsync(udpClient, serverTimeMessage, receivedResult.RemoteEndPoint);
            }

            messageFragments[messageId][partIndex] = messagePart;

            string responseMessage = $"Message with id - {messageId} part {partIndex + 1} received";
            LogMessage(responseMessage);
            await SendResponseAsync(udpClient, responseMessage, receivedResult.RemoteEndPoint);

            if (messageFragments[messageId].Count == totalParts)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < totalParts; i++)
                    sb.Append(messageFragments[messageId][i]);

                string raw = sb.ToString().Replace("<EOF>", "");
                messageFragments.TryRemove(messageId, out _);
                await ProcessRequestAsync(udpClient, raw, receivedResult.RemoteEndPoint, messageId);
            }
        }

        private static async Task ProcessRequestAsync(
            UdpClient udpClient, string encryptedPayload,
            IPEndPoint remoteEndPoint, string messageId)
        {
            var stopwatch = Stopwatch.StartNew();

            // ── Decrypt ──────────────────────────────────────────────────────
            string plaintext;
            try
            {
                plaintext = CryptoHelper.Decrypt(encryptedPayload);
                string decryptedMsg = $"Message with id - {messageId} decrypted successfully on server side";
                await SendResponseAsync(udpClient, decryptedMsg, remoteEndPoint);
                LogMessage(decryptedMsg);
            }
            catch (Exception ex)
            {
                string errMsg = $"Message with id - {messageId} DECRYPTION FAILED! Exception: {ex.Message}";
                await SendResponseAsync(udpClient, errMsg, remoteEndPoint);
                LogMessage(errMsg);
                await SendResponseAsync(udpClient, "---Response end---", remoteEndPoint);
                return;
            }

            await SendResponseAsync(udpClient,
                $"Message with id - {messageId} successfully received on server side", remoteEndPoint);
            LogMessage($"Message with id - {messageId} successfully received on server side");

            // ── Persist to file ───────────────────────────────────────────────
            string response1 = await FilePostValueAsync(plaintext, messageId);
            await SendResponseAsync(udpClient, response1, remoteEndPoint);
            LogMessage(response1);

            // ── Persist to MongoDB ────────────────────────────────────────────
            string response2 = await DBPostValueAsync(plaintext, messageId);
            await SendResponseAsync(udpClient, response2, remoteEndPoint);
            LogMessage(response2);

            if (response1.Contains("successfully") && response2.Contains("successfully"))
            {
                string ok = $"Transmit chain of message with id - {messageId} completed successfully";
                await SendResponseAsync(udpClient, ok, remoteEndPoint);
                LogMessage(ok);
            }
            else
            {
                string fail = $"Transmit chain of message with id - {messageId} FAILED!";
                await SendResponseAsync(udpClient, fail, remoteEndPoint);
                LogMessage(fail);
            }

            stopwatch.Stop();
            string elapsed = $"{stopwatch.Elapsed.TotalSeconds:F2} seconds was spent on processing";
            await SendResponseAsync(udpClient, elapsed, remoteEndPoint);
            LogMessage(elapsed);
            await SendResponseAsync(udpClient, $"{DateTime.Now:dd.MM.yyyy - HH:mm:ss zzz}", remoteEndPoint);
            await SendResponseAsync(udpClient, "---Response end---", remoteEndPoint);
            LogMessage("---Response end---");
        }

        private static async Task SendResponseAsync(UdpClient udpClient, string message, IPEndPoint remoteEndPoint)
        {
            const int maxPacketSize = 512;
            var messageBytes = Encoding.UTF8.GetBytes(message);

            for (int i = 0; i < messageBytes.Length; i += maxPacketSize)
            {
                var packet = new byte[Math.Min(maxPacketSize, messageBytes.Length - i)];
                Array.Copy(messageBytes, i, packet, 0, packet.Length);
                await udpClient.SendAsync(packet, packet.Length, remoteEndPoint);
                await Task.Delay(50);
            }
        }

        public static Task<string> DBPostValueAsync(string value, string messageId)
            => MongoDBHandler.PostValueAsync(value, messageId);

        public static async Task<string> FilePostValueAsync(string value, string messageId)
        {
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy-HH:mm:ss zzz");
            string resvalue  = $"{timestamp}  messageId: {messageId}, message: {value}";

            try
            {
                string dataDirectory = Path.Combine(
                    Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.FullName,
                    "data");
                string filePath = Path.Combine(dataDirectory, "data.txt");

                if (!Directory.Exists(dataDirectory))
                    Directory.CreateDirectory(dataDirectory);

                if (!File.Exists(filePath))
                    File.Create(filePath).Dispose();

                await File.AppendAllTextAsync(filePath, resvalue + Environment.NewLine + Environment.NewLine);
                return $"Message with id - {messageId} successfully saved in file on server side";
            }
            catch (Exception ex)
            {
                return $"Message with id - {messageId} has !ERROR! with writing into file on server side! \nException: {ex.Message}";
            }
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork && ip.ToString() != "127.0.0.1")
                    return ip.ToString();
            return "127.0.0.1";
        }

        static bool IsPortAvailable(int port)
        {
            bool available = true;
            try { var t = new TcpListener(IPAddress.Any, port); t.Start(); t.Stop(); }
            catch (SocketException) { available = false; }
            if (available)
                try { var u = new UdpClient(port); u.Close(); }
                catch (SocketException) { available = false; }
            return available;
        }

        public static void LogMessage(string message)
        {
            string timestamp  = DateTime.Now.ToString("dd.MM.yyyy-HH:mm:ss.fff zzz");
            string logMessage = $"{timestamp} - {message}";
            Console.WriteLine(logMessage);

            string dataDirectory = Path.Combine(
                Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.FullName,
                "data");
            string logFilePath = Path.Combine(dataDirectory, "server.log");

            try
            {
                if (!Directory.Exists(dataDirectory))
                    Directory.CreateDirectory(dataDirectory);
                using var writer = new StreamWriter(logFilePath, append: true);
                writer.WriteLine(logMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to log message. Exception: {ex.Message}");
            }
        }
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine();
            UDPserver.LogMessage(UDPserver.GetLocalIPAddress());
            await UDPserver.ListenAsync();
        }
    }
}
