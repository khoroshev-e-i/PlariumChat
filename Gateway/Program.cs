using Gateway.Settings;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gateway
{
    class Program
    {
        private static readonly ConcurrentDictionary<string, TcpClient> Connections = new ConcurrentDictionary<string, TcpClient>();
        private static readonly ConcurrentQueue<KeyValuePair<string, string>> Messages = new ConcurrentQueue<KeyValuePair<string, string>>();
        private static GatewaySettings GatewaySettings = new GatewaySettings();

        static void Main(string[] args)
        {
            var configuration = (IConfiguration)new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appsettings.json", false)
                .Build();
            configuration.Bind(nameof(GatewaySettings), GatewaySettings);

            try
            {

               Task.Run(async () => await ListenAsync()).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch { }
            finally
            {
                foreach (var connection in Connections)
                {
                    connection.Value.Dispose();
                }
            }
        }

        public async static Task ListenAsync()
        {
            var ip = new IPAddress(GatewaySettings.Host);
            var listener = new TcpListener(ip, GatewaySettings.Port);

            listener.Start();
            while (true)
            {
                var thread = new Thread(async () =>
                {
                    using var client = await listener.AcceptTcpClientAsync();
                    var username = await ReadGreetingAsync(client);

                    Connections.GetOrAdd(username, client);
                    await ReadClientDataAsync(client, username);
                });

                thread.Start();
            }

        }

        private async static Task<string> ReadGreetingAsync(TcpClient client)
        {
            var stream = client.GetStream();
            var buffer = new byte[GatewaySettings.BufferSize];
            await stream.FlushAsync();
            await stream.ReadAsync(buffer);
            await stream.FlushAsync();

            var serialized = Encoding.UTF8.GetString(buffer);
            var greet = JsonConvert.DeserializeObject<Greeting>(serialized);

            Console.WriteLine($"[System] -- {greet.Username} вошел в чат.");

            return greet.Username;
        }

        private static async Task ReadClientDataAsync(TcpClient client, string username)
        {
            var counter = 0;
            var stream = client.GetStream();

            try
            {
                while (true)
                {
                    var bytes = new byte[GatewaySettings.BufferSize];
                    var bytesCount = await stream.ReadAsync(bytes, 0, bytes.Length);
                    if (bytesCount == 0)
                        return;

                    var message = Encoding.UTF8.GetString(bytes, 0, bytesCount);

                    Enqueue(message);

                    Console.WriteLine($"\t{username}: {message}");
                    SendToOthers(new KeyValuePair<string, string>(username, message));

                    //// Send back a response.
                    //stream.Write(msg, 0, msg.Length);
                    //Console.WriteLine("Sent: {0}", data);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при чтении сообщения от клиента: {}");
            }

            void Enqueue(string message)
            {
                if (Messages.Count == GatewaySettings.HistorySize)
                {
                    Messages.TryDequeue(out _);
                }
                Messages.Enqueue(new KeyValuePair<string, string>(username, message));
            }
        }

        private static void SendToOthers(KeyValuePair<string, string> message)
        {
            Parallel.ForEach(Connections.Keys, async (username) =>
            {
                if (Connections.TryGetValue(username, out var connection))
                    await SendMessage(connection.GetStream(), $"\t{message.Key}: {message.Value}");
            });
        }

        private static async Task SendMessage(Stream stream, string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var length = bytes.Length > GatewaySettings.BufferSize
                ? GatewaySettings.BufferSize 
                : bytes.Length;

            await stream.FlushAsync();
            await stream.WriteAsync(bytes, 0, length);
            await stream.FlushAsync();
        }

        private static void OnClientConnected(IAsyncResult asyncResult)
        {
            var listener = asyncResult.AsyncState;

        }
    }
}
