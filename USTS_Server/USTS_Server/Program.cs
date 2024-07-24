// Connecting necessary namespaces
using System; // Main namespace for basic classes and types
using System.Net.Sockets; // Namespace for working with network sockets
using System.Net; // Namespace for working with network addresses and protocols
using System.Text; // Namespace for working with text data and encodings
using System.Threading.Tasks; // Namespace for working with asynchronous programming
using System.Collections.Concurrent; // Namespace for working with thread-safe collections
using System.IO; // Namespace for working with file operations
using System.Diagnostics; // Namespace for working with diagnostics, including measuring execution time
using System.Text.Json;
using MongoDB.Driver; // Namespace for working with MongoDB driver
using MongoDB.Bson; // Namespace for working with BSON (Binary JSON) in MongoDB

namespace USTS_server // Defining namespace for logical organization of code
{
    // Class for working with MongoDB
    public class MongoDBHandler
    {
        // Connection string to MongoDB
        private static readonly string connectionString = "mongodb://username:password@localhost:27017/database_name";
        // Database name
        private static readonly string databaseName = "database_name";
        // Collection name
        private static readonly string collectionName = "messages";

        // Asynchronous method for writing a message to MongoDB
        public static async Task<string> PostValueAsync(string message, string messageId)
        {
            try
            {
                // Creating MongoDB client
                var client = new MongoClient(connectionString);
                // Getting database
                var database = client.GetDatabase(databaseName);
                // Getting collection
                var collection = database.GetCollection<BsonDocument>(collectionName);

                // Checking if collection exists and creating if it doesn't
                if (!await CollectionExistsAsync(database, collectionName))
                {
                    await database.CreateCollectionAsync(collectionName);
                }

                // Creating BSON document to insert into collection
                var document = new BsonDocument
                {
                    { "messageId", messageId }, // Adding field messageId
                    { "message", message } // Adding field message
                };
                // Inserting document into collection
                await collection.InsertOneAsync(document);

                return $"Message with id - {messageId} successfully saved into DB on server side";
            }
            catch (Exception ex)
            {
                // Returning error message when writing to database
                return $"Message with id - {messageId} has !ERROR! with writing into DB on server side! \nException: {GetShortExceptionMessage(ex)}";
            }
        }

        // Asynchronous method for checking if collection exists
        private static async Task<bool> CollectionExistsAsync(IMongoDatabase database, string collectionName)
        {
            var filter = new BsonDocument("name", collectionName);
            var collections = await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter });
            return await collections.AnyAsync();
        }

        // Method for getting a short exception message
        private static string GetShortExceptionMessage(Exception ex)
        {
            return ex.GetBaseException().ToString();
        }
    }

    // Main UDP server class
    class UDPserver
    {
        // Dictionary for storing message fragments
        private static ConcurrentDictionary<string, ConcurrentDictionary<int, string>> messageFragments = new ConcurrentDictionary<string, ConcurrentDictionary<int, string>>();

        // Asynchronous method for listening to UDP messages
        public static async Task ListenAsync()
        {
            int PORT = 12345; // Port for listening
            if (!IsPortAvailable(PORT)) // Checking port availability
            {
                LogMessage("Port already occupied!"); // Logging and outputting message about occupied port
                return;
            }

            using UdpClient udpClient = new UdpClient(PORT); // Creating UDP client
            LogMessage($"Listening on port {PORT}\n\n\n"); // Logging and outputting message about starting to listen

            var from = new IPEndPoint(IPAddress.Any, 0); // Defining endpoint for receiving data
            while (true)
            {
                var receivedResult = await udpClient.ReceiveAsync(); // Asynchronous receiving data
                _ = Task.Run(() => HandleMessageAsync(udpClient, receivedResult)); // Processing message in a separate task
            }
        }

        // Asynchronous method for processing received message
        private static async Task HandleMessageAsync(UdpClient udpClient, UdpReceiveResult receivedResult)
        {
            var receivedData = Encoding.UTF8.GetString(receivedResult.Buffer); // Converting received data to string
            string[] parts = receivedData.Split(new char[] { ':' }, 4); // Splitting string into parts

            if (parts.Length != 4)
            {
                LogMessage("Received data is not in the correct format."); // Logging and outputting message about incorrect data format
                return;
            }

            string messageId = parts[0]; // Message identifier
            int partIndex = int.Parse(parts[1]); // Part index of the message
            int totalParts = int.Parse(parts[2]); // Total number of message parts
            string messagePart = parts[3]; // Content of the message part

            if (!messageFragments.ContainsKey(messageId)) // Checking if key exists in the dictionary
            {
                messageFragments[messageId] = new ConcurrentDictionary<int, string>(); // Creating new entry in the dictionary

                string startMessage = "---Response start---"; // Start message
                LogMessage(startMessage); // Logging and outputting start message
                await SendResponseAsync(udpClient, startMessage, receivedResult.RemoteEndPoint); // Sending start message to client

                string serverTimeMessage = DateTime.Now.ToString("dd.MM.yyyy-HH:mm:ss zzz"); // Server time
                LogMessage(serverTimeMessage); // Logging and outputting time
                await SendResponseAsync(udpClient, serverTimeMessage, receivedResult.RemoteEndPoint); // Sending time to client
            }

            messageFragments[messageId][partIndex] = messagePart; // Adding message part to dictionary

            string responseMessage = $"Message with id - {messageId} part {partIndex + 1} received"; // Response message
            LogMessage(responseMessage); // Logging and outputting response message
            await SendResponseAsync(udpClient, responseMessage, receivedResult.RemoteEndPoint); // Sending response message to client

            if (messageFragments[messageId].Count == totalParts) // Checking if all parts of the message are received
            {
                var completeMessage = new StringBuilder(); // Creating StringBuilder for combining message parts
                for (int i = 0; i < totalParts; i++)
                {
                    completeMessage.Append(messageFragments[messageId][i]); // Combining message parts
                }

                string completeMessageStr = completeMessage.ToString().Replace("<EOF>", ""); // Replacing "<EOF>" with empty string
                messageFragments.TryRemove(messageId, out _); // Removing entry from dictionary
                await ProcessRequestAsync(udpClient, completeMessageStr, receivedResult.RemoteEndPoint, messageId); // Processing complete message
            }
        }

        // Asynchronous method for processing request
        private static async Task ProcessRequestAsync(UdpClient udpClient, string receivedData, IPEndPoint remoteEndPoint, string messageId)
        {
            Stopwatch stopwatch = Stopwatch.StartNew(); // Starting stopwatch

            await SendResponseAsync(udpClient, $"Message with id - {messageId} successfully received on server side", remoteEndPoint);
            LogMessage($"Message with id - {messageId} successfully received on server side");

            // Writing message to file
            string response1 = await FilePostValueAsync(receivedData, messageId);
            await SendResponseAsync(udpClient, response1, remoteEndPoint);
            LogMessage(response1);

            // Writing message to database
            string response2 = await DBPostValueAsync(receivedData, messageId);
            await SendResponseAsync(udpClient, response2, remoteEndPoint);
            LogMessage(response2);

            if (response1.Contains("successfully") && response2.Contains("successfully"))
            {
                string successMessage = $"Transmit chain of message with id - {messageId} completed successfully";
                await SendResponseAsync(udpClient, successMessage, remoteEndPoint);
                LogMessage(successMessage);
            }
            else
            {
                string failureMessage = $"Transmit chain of message with id - {messageId} FAILED!";
                await SendResponseAsync(udpClient, failureMessage, remoteEndPoint);
                LogMessage(failureMessage);
            }

            stopwatch.Stop(); // Stopping stopwatch
            string serverEndTimeMessage = $"{stopwatch.Elapsed.TotalSeconds:F2} seconds was spent on processing";
            await SendResponseAsync(udpClient, serverEndTimeMessage, remoteEndPoint);
            LogMessage(serverEndTimeMessage);
            await SendResponseAsync(udpClient, $"{DateTime.Now:dd.MM.yyyy - HH:mm:ss zzz}", remoteEndPoint);
            LogMessage($"{DateTime.Now:dd.MM.yyyy - HH:mm:ss zzz}");
            string endMessage = "---Response end---"; // End message
            await SendResponseAsync(udpClient, endMessage, remoteEndPoint); // Sending end message to client
            LogMessage(endMessage); // Logging and outputting end message
        }

        // Asynchronous method for sending response to client
        private static async Task SendResponseAsync(UdpClient udpClient, string message, IPEndPoint remoteEndPoint)
        {
            const int maxPacketSize = 512; // Maximum packet size
            var messageBytes = Encoding.UTF8.GetBytes(message); // Converting message to bytes

            for (int i = 0; i < messageBytes.Length; i += maxPacketSize)
            {
                var packet = new byte[Math.Min(maxPacketSize, messageBytes.Length - i)]; // Creating packet
                Array.Copy(messageBytes, i, packet, 0, packet.Length); // Copying data into packet
                await udpClient.SendAsync(packet, packet.Length, remoteEndPoint); // Sending packet to client
                await Task.Delay(50); // Delay before sending the next packet
            }
        }
        // Asynchronous method for writing value to database
        public static Task<string> DBPostValueAsync(string value, string messageId)
        {
            return MongoDBHandler.PostValueAsync(value, messageId);
        }

        // Asynchronous method for writing value to file
        public static async Task<string> FilePostValueAsync(string value, string messageId)
        {
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy-HH:mm:ss zzz"); // Getting current time
            string resvalue = $"{timestamp}  messageId: {messageId}, message: {value}"; // Forming string for writing

            try
            {
                string dataDirectory = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName, "data");
                string filePath = Path.Combine(dataDirectory, "data.txt");

                var directory = Path.GetDirectoryName(filePath); // Getting path to directory
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory); // Creating directory if it doesn't exist
                }

                if (!File.Exists(filePath))
                {
                    using (var stream = File.Create(filePath)) // Creating file if it doesn't exist
                    {
                    }
                }

                await File.AppendAllTextAsync(filePath, resvalue + Environment.NewLine + Environment.NewLine); // Appending string to file
                return $"Message with id - {messageId} successfully saved in file on server side";
            }
            catch (Exception ex)
            {
                return $"Message with id - {messageId} has !ERROR! with writing into file on server side! \nException: {ex.Message}";
            }
        }

        // Method for getting local IP address
        public static string GetLocalIPAddress()
        {
            var hostname = Dns.GetHostName(); // Getting hostname
            var host = Dns.GetHostEntry(hostname); // Getting IP addresses of the host
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && ip.ToString() != "127.0.0.1")
                {
                    return ip.ToString(); // Returning IP address if it's not local
                }
            }
            return "127.0.0.1"; // Returning local IP address if no others are found
        }

        // Method for checking port availability
        static bool IsPortAvailable(int port)
        {
            bool isAvailable = true;
            try
            {
                TcpListener tcpListener = new TcpListener(IPAddress.Any, port); // Creating TCP listener
                tcpListener.Start(); // Starting TCP listener
                tcpListener.Stop(); // Stopping TCP listener
            }
            catch (SocketException)
            {
                isAvailable = false; // If an exception occurs, the port is not available
            }
            if (isAvailable)
            {
                try
                {
                    UdpClient udpClient = new UdpClient(port); // Creating UDP client
                    udpClient.Close(); // Closing UDP client
                }
                catch (SocketException)
                {
                    isAvailable = false; // If an exception occurs, the port is not available
                }
            }
            return isAvailable; // Returning port availability result
        }

        // Method for logging messages
        public static void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy-HH:mm:ss.fff zzz"); // Getting current time
            string logMessage = $"{timestamp} - {message}"; // Forming string for logging
            Console.WriteLine(logMessage); // Outputting log message to console

            string dataDirectory = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName, "data");
            string logFilePath = Path.Combine(dataDirectory, "server.log");

            try
            {
                if (!Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory); // Creating directory if it doesn't exist
                }

                using (var writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine(logMessage); // Writing log message to file
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to log message. Exception: {ex.Message}"); // Outputting error message to console
            }
        }
    }

    // Internal class program
    internal class Program
    {
        // Asynchronous main method
        static async Task Main(string[] args)
        {
            Console.WriteLine("\n"); // Outputting empty line to console
            UDPserver.LogMessage($"{UDPserver.GetLocalIPAddress()}"); // Logging and outputting local IP address
            await UDPserver.ListenAsync(); // Starting UDP message listening method
        }
    }
}
