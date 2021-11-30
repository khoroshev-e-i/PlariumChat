using PlariumChat.Settings;
using Shared;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlariumChat
{
    class Program
    {
        private static ClientSettings ClientSettings;
        static void Main(string[] args)
        {
            ClientSettings = ConfigurationHelper.GetSettings<ClientSettings>();
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.ForegroundColor = ConsoleColor.White;

            while (true)
            {
                try
                {
                    Process().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch(Exception e)
                {
                    Console.WriteLine("Соединение с сервером чата потеряно, попытка повторного соединения...");
                    Thread.Sleep(500);
                }
            }
        }

        public static async Task Process()
        {
            using var client = new TcpClient();
            Console.WriteLine("Введите имя пользователя:");
            await client.ConnectAsync(new IPAddress(ClientSettings.GatewayHost), ClientSettings.Port);
            var username = Console.ReadLine();

            if (client.Connected)
            {
                var thread = new Thread(async () => await AwaitMessages(client));
                await SendGreeting(client, username)
                    .ContinueWith(async t => await ReceiveGreeting(client, username))
                    .ContinueWith(x => Task.Run(() => thread.Start()));

                while (true)
                {
                    if (!client.Connected)
                        return;

                    Console.Write("Вы: ");

                    var message = Console.ReadLine();
                    var bytes = Encoding.UTF8.GetBytes(message);
                    var writer = new StreamWriter(client.GetStream());

                    await writer.FlushAsync();
                    await writer.WriteAsync(message);
                    await writer.FlushAsync();
                }
            }
        }

        private async static Task ReceiveGreeting(TcpClient client, string username)
        {
            var message = await ReadMessageAsync(client.GetStream());
            var lines = message.Split("\n");
            
            Console.SetCursorPosition(0, Console.CursorTop);

            ToSystemChat();
            Console.WriteLine($"История сообщений чата:");
            ToDefaultChat();


            foreach (var line in lines)
            {
                if (line.StartsWith(Constants.SystemMessageCaption))
                    ToSystemChat();

                WriteFromRight(line);
                ToDefaultChat();
            }

            Console.Write("Вы: ");
        }

        static void WriteFromRight(string message)
        {
            var backgroundColor = Console.BackgroundColor;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write("\t\t");
            Console.BackgroundColor = backgroundColor;

            Console.SetCursorPosition(Console.WindowWidth - message.Length, Console.CursorTop);
            Console.Write($"{message}");
        }

        private static void ToSystemChat()
        {
            Console.BackgroundColor = ConsoleColor.Gray;
            Console.ForegroundColor = ConsoleColor.Black;
        }

        private static void ToDefaultChat()
        {
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.ForegroundColor = ConsoleColor.White;
        }

        private async static Task<string> ReadMessageAsync(Stream stream)
        {
            var buffer = new byte[ClientSettings.BufferSize];
            await stream.FlushAsync();
            await stream.ReadAsync(buffer, 0, ClientSettings.BufferSize);
            await stream.FlushAsync();
            var message = Encoding.UTF8.GetString(buffer).Trim('\0');

            return message;
        }

        private async static Task AwaitMessages(TcpClient client)
        {
            var stream = client.GetStream();
            while (true)
            {
                if (!client.Connected)
                    return;

                var message = await ReadMessageAsync(stream);

                if (message.StartsWith(Constants.SystemMessageCaption))
                    ToSystemChat();

                Console.SetCursorPosition(0, Console.CursorTop);
                WriteFromRight(message);
                ToDefaultChat();
                Console.Write("Вы: ");
            }
        }

        private async static Task SendGreeting(TcpClient client, string username)
        {
            var greet = new Greeting() { Username = username };
            var message = Encoding.UTF8.GetBytes(greet.ToJson());
            var stream = client.GetStream();
            await stream.FlushAsync();
            await stream.WriteAsync(message);
            await stream.FlushAsync();
        }
    }
}
