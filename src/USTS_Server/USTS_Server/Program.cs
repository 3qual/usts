// Подключение необходимых пространств имен
using System; // Основное пространство имен для базовых классов и типов
using System.Net.Sockets; // Пространство имен для работы с сетевыми сокетами
using System.Net; // Пространство имен для работы с сетевыми адресами и протоколами
using System.Text; // Пространство имен для работы с текстовыми данными и кодировками
using System.Threading.Tasks; // Пространство имен для работы с асинхронным программированием
using System.Collections.Concurrent; // Пространство имен для работы с потокобезопасными коллекциями
using System.IO; // Пространство имен для работы с файловыми операциями
using System.Diagnostics; // Пространство имен для работы с диагностикой, включая измерение времени выполнения
using System.Text.Json;
using MongoDB.Driver; // Пространство имен для работы с MongoDB драйвером
using MongoDB.Bson; // Пространство имен для работы с BSON (бинарный JSON) в MongoDB

namespace USTS_server // Определение пространства имен для логической организации кода
{
    // Класс для работы с MongoDB
    public class MongoDBHandler
    {
        // Строка подключения к MongoDB
        private static readonly string connectionString = "mongodb://username:password@localhost:27017/database_name";
        // Название базы данных
        private static readonly string databaseName = "database_name";
        // Название коллекции
        private static readonly string collectionName = "messages";

        // Асинхронный метод для записи сообщения в MongoDB
        public static async Task<string> PostValueAsync(string message, string messageId)
        {
            try
            {
                // Создание клиента MongoDB
                var client = new MongoClient(connectionString);
                // Получение базы данных
                var database = client.GetDatabase(databaseName);
                // Получение коллекции
                var collection = database.GetCollection<BsonDocument>(collectionName);

                // Проверка, существует ли коллекция, и создание, если не существует
                if (!await CollectionExistsAsync(database, collectionName))
                {
                    await database.CreateCollectionAsync(collectionName);
                }

                // Создание BSON документа для вставки в коллекцию
                var document = new BsonDocument
                {
                    { "messageId", messageId }, // Добавление поля messageId
                    { "message", message } // Добавление поля message
                };
                // Вставка документа в коллекцию
                await collection.InsertOneAsync(document);

                return $"Message with id - {messageId} successfully saved into DB on server side";
            }
            catch (Exception ex)
            {
                // Возвращение сообщения об ошибке при записи в базу данных
                return $"Message with id - {messageId} has !ERROR! with writing into DB on server side! \nException: {GetShortExceptionMessage(ex)}";
            }
        }

        // Асинхронный метод для проверки существования коллекции
        private static async Task<bool> CollectionExistsAsync(IMongoDatabase database, string collectionName)
        {
            var filter = new BsonDocument("name", collectionName);
            var collections = await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter });
            return await collections.AnyAsync();
        }

        // Метод для получения краткого сообщения об исключении
        private static string GetShortExceptionMessage(Exception ex)
        {
            return ex.GetBaseException().ToString();
        }
    }

    // Основной класс UDP сервера
    class UDPserver
    {
        // Словарь для хранения фрагментов сообщений
        private static ConcurrentDictionary<string, ConcurrentDictionary<int, string>> messageFragments = new ConcurrentDictionary<string, ConcurrentDictionary<int, string>>();

        // Асинхронный метод для прослушивания UDP сообщений
        public static async Task ListenAsync()
        {
            int PORT = 12345; // Порт для прослушивания
            if (!IsPortAvailable(PORT)) // Проверка доступности порта
            {
                LogMessage("Port already occupied!"); // Логирование и вывод сообщения о занятом порте
                return;
            }

            using UdpClient udpClient = new UdpClient(PORT); // Создание UDP клиента
            LogMessage($"Listening on port {PORT}\n\n\n"); // Логирование и вывод сообщения о начале прослушивания

            var from = new IPEndPoint(IPAddress.Any, 0); // Определение конечной точки для приема данных
            while (true)
            {
                var receivedResult = await udpClient.ReceiveAsync(); // Асинхронное получение данных
                _ = Task.Run(() => HandleMessageAsync(udpClient, receivedResult)); // Обработка сообщения в отдельном таске
            }
        }

        // Асинхронный метод для обработки полученного сообщения
        private static async Task HandleMessageAsync(UdpClient udpClient, UdpReceiveResult receivedResult)
        {
            var receivedData = Encoding.UTF8.GetString(receivedResult.Buffer); // Преобразование полученных данных в строку
            string[] parts = receivedData.Split(new char[] { ':' }, 4); // Разделение строки на части

            if (parts.Length != 4)
            {
                LogMessage("Received data is not in the correct format."); // Логирование и вывод сообщения о некорректном формате данных
                return;
            }

            string messageId = parts[0]; // Идентификатор сообщения
            int partIndex = int.Parse(parts[1]); // Индекс части сообщения
            int totalParts = int.Parse(parts[2]); // Общее количество частей сообщения
            string messagePart = parts[3]; // Содержимое части сообщения

            if (!messageFragments.ContainsKey(messageId)) // Проверка наличия ключа в словаре
            {
                messageFragments[messageId] = new ConcurrentDictionary<int, string>(); // Создание новой записи в словаре

                string startMessage = "---Response start---"; // Стартовое сообщение
                LogMessage(startMessage); // Логирование и вывод стартового сообщения
                await SendResponseAsync(udpClient, startMessage, receivedResult.RemoteEndPoint); // Отправка стартового сообщения клиенту

                string serverTimeMessage = DateTime.Now.ToString("dd.MM.yyyy-HH:mm:ss zzz"); // Время на сервере
                LogMessage(serverTimeMessage); // Логирование и вывод времени
                await SendResponseAsync(udpClient, serverTimeMessage, receivedResult.RemoteEndPoint); // Отправка времени клиенту
            }

            messageFragments[messageId][partIndex] = messagePart; // Добавление части сообщения в словарь

            string responseMessage = $"Message with id - {messageId} part {partIndex + 1} received"; // Ответное сообщение
            LogMessage(responseMessage); // Логирование и вывод ответного сообщения
            await SendResponseAsync(udpClient, responseMessage, receivedResult.RemoteEndPoint); // Отправка ответного сообщения клиенту

            if (messageFragments[messageId].Count == totalParts) // Проверка получения всех частей сообщения
            {
                var completeMessage = new StringBuilder(); // Создание StringBuilder для объединения частей сообщения
                for (int i = 0; i < totalParts; i++)
                {
                    completeMessage.Append(messageFragments[messageId][i]); // Объединение частей сообщения
                }

                string completeMessageStr = completeMessage.ToString().Replace("<EOF>", ""); // Замена "<EOF>" на пустую строку
                messageFragments.TryRemove(messageId, out _); // Удаление записи из словаря
                await ProcessRequestAsync(udpClient, completeMessageStr, receivedResult.RemoteEndPoint, messageId); // Обработка полного сообщения
            }
        }

        // Асинхронный метод для обработки запроса
        private static async Task ProcessRequestAsync(UdpClient udpClient, string receivedData, IPEndPoint remoteEndPoint, string messageId)
        {
            Stopwatch stopwatch = Stopwatch.StartNew(); // Запуск секундомера

            await SendResponseAsync(udpClient, $"Message with id - {messageId} successfully received on server side", remoteEndPoint);
            LogMessage($"Message with id - {messageId} successfully received on server side");

            // Запись сообщения в файл
            string response1 = await FilePostValueAsync(receivedData, messageId);
            await SendResponseAsync(udpClient, response1, remoteEndPoint);
            LogMessage(response1);

            // Запись сообщения в базу данных
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

            stopwatch.Stop(); // Остановка секундомера
            string serverEndTimeMessage = $"{stopwatch.Elapsed.TotalSeconds:F2} seconds was spent on processing";
            await SendResponseAsync(udpClient, serverEndTimeMessage, remoteEndPoint);
            LogMessage(serverEndTimeMessage);
            await SendResponseAsync(udpClient, $"{DateTime.Now:dd.MM.yyyy - HH:mm: ss zzz}", remoteEndPoint);
            LogMessage($"{DateTime.Now:dd.MM.yyyy - HH:mm: ss zzz}");
            string endMessage = "---Response end---"; // Конечное сообщение
            await SendResponseAsync(udpClient, endMessage, remoteEndPoint); // Отправка конечного сообщения клиенту
            LogMessage(endMessage); // Логирование и вывод конечного сообщения
        }

        // Асинхронный метод для отправки ответа клиенту
        private static async Task SendResponseAsync(UdpClient udpClient, string message, IPEndPoint remoteEndPoint)
        {
            const int maxPacketSize = 512; // Максимальный размер пакета
            var messageBytes = Encoding.UTF8.GetBytes(message); // Преобразование сообщения в байты

            for (int i = 0; i < messageBytes.Length; i += maxPacketSize)
            {
                var packet = new byte[Math.Min(maxPacketSize, messageBytes.Length - i)]; // Создание пакета
                Array.Copy(messageBytes, i, packet, 0, packet.Length); // Копирование данных в пакет
                await udpClient.SendAsync(packet, packet.Length, remoteEndPoint); // Отправка пакета клиенту
                await Task.Delay(50); // Задержка перед отправкой следующего пакета
            }
        }

        // Асинхронный метод для записи значения в базу данных
        public static Task<string> DBPostValueAsync(string value, string messageId)
        {
            return MongoDBHandler.PostValueAsync(value, messageId);
        }

        // Асинхронный метод для записи значения в файл
        public static async Task<string> FilePostValueAsync(string value, string messageId)
        {
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy-HH:mm:ss zzz"); // Получение текущего времени
            string resvalue = $"{timestamp}  messageId: {messageId}, message: {value}"; // Формирование строки для записи

            try
            {
                string dataDirectory = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName, "data");
                string filePath = Path.Combine(dataDirectory, "data.txt");

                var directory = Path.GetDirectoryName(filePath); // Получение пути к директории
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory); // Создание директории, если она не существует
                }

                if (!File.Exists(filePath))
                {
                    using (var stream = File.Create(filePath)) // Создание файла, если он не существует
                    {
                    }
                }

                await File.AppendAllTextAsync(filePath, resvalue + Environment.NewLine + Environment.NewLine); // Дописывание строки в файл
                return $"Message with id - {messageId} successfully saved in file on server side";
            }
            catch (Exception ex)
            {
                return $"Message with id - {messageId} has !ERROR! with writing into file on server side! \nException: {ex.Message}";
            }
        }

        // Метод для получения локального IP адреса
        public static string GetLocalIPAddress()
        {
            var hostname = Dns.GetHostName(); // Получение имени хоста
            var host = Dns.GetHostEntry(hostname); // Получение IP адресов хоста
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && ip.ToString() != "127.0.0.1")
                {
                    return ip.ToString(); // Возвращение IP адреса, если он не является локальным
                }
            }
            return "127.0.0.1"; // Возвращение локального IP адреса, если не найдено других
        }

        // Метод для проверки доступности порта
        static bool IsPortAvailable(int port)
        {
            bool isAvailable = true;
            try
            {
                TcpListener tcpListener = new TcpListener(IPAddress.Any, port); // Создание TCP слушателя
                tcpListener.Start(); // Запуск TCP слушателя
                tcpListener.Stop(); // Остановка TCP слушателя
            }
            catch (SocketException)
            {
                isAvailable = false; // Если возникает исключение, порт недоступен
            }
            if (isAvailable)
            {
                try
                {
                    UdpClient udpClient = new UdpClient(port); // Создание UDP клиента
                    udpClient.Close(); // Закрытие UDP клиента
                }
                catch (SocketException)
                {
                    isAvailable = false; // Если возникает исключение, порт недоступен
                }
            }
            return isAvailable; // Возвращение результата проверки
        }

        // Метод для логирования сообщений
        public static void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy-HH:mm:ss.fff zzz"); // Получение текущего времени
            string logMessage = $"{timestamp} - {message}"; // Формирование строки для логирования
            Console.WriteLine(logMessage); // Вывод лог сообщения на консоль

            string dataDirectory = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName, "data");
            string logFilePath = Path.Combine(dataDirectory, "server.log");

            try
            {
                if (!Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory); // Создание директории, если она не существует
                }

                using (var writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine(logMessage); // Запись лог сообщения в файл
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to log message. Exception: {ex.Message}"); // Вывод сообщения об ошибке на консоль
            }
        }
    }

    // Внутренний класс программы
    internal class Program
    {
        // Асинхронный метод main
        static async Task Main(string[] args)
        {
            Console.WriteLine("\n"); // Вывод пустой строки на консоль
            UDPserver.LogMessage($"{UDPserver.GetLocalIPAddress()}"); // Логирование и вывод локального IP адреса
            await UDPserver.ListenAsync(); // Запуск метода прослушивания UDP сообщений
        }
    }
}
