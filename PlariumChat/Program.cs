using Shared;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlariumChat
{
    class Program
    {
        static void Main(string[] args)
        {
            var task = Task.Run(async () => await Process());
            task.ConfigureAwait(false).GetAwaiter().GetResult();

            Console.ReadLine();
        }

        public static async Task Process()
        {
            using var client = new TcpClient();
            Console.WriteLine("Enter username to start:");
            var username = Console.ReadLine();

            await client.ConnectAsync("localhost", 112);

            if (client.Connected)
            {
                await SendGreeting(client, username);
                var thread = new Thread(async () => await AwaitMessages(client));
                thread.Start();

                while (true)
                {
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

        private async static Task AwaitMessages(TcpClient client)
        {
            var stream = client.GetStream();
            while (true)
            {
                var buffer = new byte[256];
                await stream.FlushAsync();
                await stream.ReadAsync(buffer, 0, 256);
                await stream.FlushAsync();
                var message = Encoding.UTF8.GetString(buffer).Trim('\0');

                Console.SetCursorPosition(0, Console.CursorTop);
                Console.WriteLine($"\t{message}");
                Console.Write("Вы:");
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
