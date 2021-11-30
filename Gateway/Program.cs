using Gateway.Settings;
using Newtonsoft.Json;
using Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            GatewaySettings = ConfigurationHelper.GetSettings<GatewaySettings>();

            try
            {
                Listen();
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

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            foreach (var c in Connections.Values)
            {
                try
                {
                    c.Dispose();
                }
                catch { }
            }
        }

        public static void Listen()
        {
            var ip = new IPAddress(GatewaySettings.Host);
            var listener = new TcpListener(ip, GatewaySettings.Port);

            listener.Start();
            Console.WriteLine("Сервер чата запущен.");
            while (true)
            {
                var thread = new Thread(async () =>
                {
                    using var client = await listener.AcceptTcpClientAsync();
                    var username = await ReadGreetingAsync(client);
                    await SendGreetingAsync(client);

                    Connections.GetOrAdd(username, client);
                    await ReadClientDataAsync(client, username);
                });

                thread.Start();
            }
        }

        private async static Task SendGreetingAsync(TcpClient client)
        {
            var messages = Messages.ToArray().Select(x => $"{x.Key}:{x.Value}");
            var message = string.Join("\n", messages);
            message = string.IsNullOrWhiteSpace(message) ? $"{Constants.SystemMessageCaption}: Чат пуст." : message;

            await SendMessage(client.GetStream(), message);
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
            var message = $"{greet.Username} вошел в чат.";

            SendToOthers(new KeyValuePair<string, string>(Constants.SystemMessageCaption, message));

            return greet.Username;
        }

        private static async Task ReadClientDataAsync(TcpClient client, string username)
        {
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
                    SendToOthers(new KeyValuePair<string, string>(username, message));
                }
            }
            catch (Exception e)
            {
                var message = $"{username} вышел из чата.";
                Console.WriteLine($"{Constants.SystemMessageCaption} -- {message}");
                Connections.TryRemove(username, out var conn);
                conn?.Dispose();
                SendToOthers(new KeyValuePair<string, string>(Constants.SystemMessageCaption, message));
            }

            void Enqueue(string message)
            {
                if (Messages.Count >= GatewaySettings.HistorySize)
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
                if (Connections.TryGetValue(username, out var connection) && message.Key != username)
                    await SendMessage(connection.GetStream(), $"{message.Key}: {message.Value}");
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
    }
}
