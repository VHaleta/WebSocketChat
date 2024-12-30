using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace WebSocketChatServer
{
    internal class Program
    {
        private static readonly Dictionary<WebSocket, string> _clientNicknames = new();
        private static readonly List<WebSocket> _clients = new();

        static async Task Main()
        {
            Console.WriteLine("Starting server...");
            await RunServer();
        }

        private static async Task RunServer()
        {
            var httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://localhost:5000/");
            httpListener.Start();

            Console.WriteLine("Server is running at http://localhost:5000/");
            Console.WriteLine("Waiting for incoming connections...");

            while (true)
            {
                var context = await httpListener.GetContextAsync();

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(null);
                var webSocket = wsContext.WebSocket;

                lock (_clients)
                {
                    _clients.Add(webSocket);
                }

                Console.WriteLine("A client has connected!");

                _ = Task.Run(async () => await HandleClientAsync(webSocket));
            }
        }

        private static async Task HandleClientAsync(WebSocket client)
        {
            var buffer = new byte[1024];
            bool nicknameReceived = false;
            string? nickname = null;

            try
            {
                while (client.State == WebSocketState.Open)
                {
                    var result = await client.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("Client requested to close the connection.");
                        await DisconnectClient(client);
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    if (!nicknameReceived)
                    {
                        nickname = message.Trim();
                        nicknameReceived = true;

                        lock (_clientNicknames)
                        {
                            _clientNicknames[client] = nickname;
                        }

                        Console.WriteLine($"Client set their nickname: {nickname}");
                        BroadcastMessage($"[{nickname}] joined the chat", client);
                    }
                    else
                    {
                        var senderName = _clientNicknames.ContainsKey(client)
                            ? _clientNicknames[client]
                            : "Unknown";

                        var textToSend = $"[{senderName}]: {message}";
                        Console.WriteLine($"Received message: {textToSend}");
                        BroadcastMessage(textToSend, client);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while handling client: {ex.Message}");
                await DisconnectClient(client);
            }
        }

        private static async Task DisconnectClient(WebSocket client)
        {
            if (client == null) return;

            string? nickname = null;

            lock (_clientNicknames)
            {
                if (_clientNicknames.TryGetValue(client, out var foundNickname))
                {
                    nickname = foundNickname;
                }
            }

            if (client.State == WebSocketState.Open || client.State == WebSocketState.CloseReceived)
            {
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                        "Client closed the connection",
                                        CancellationToken.None);
            }

            lock (_clients)
            {
                _clients.Remove(client);
            }

            if (!string.IsNullOrWhiteSpace(nickname))
            {
                lock (_clientNicknames)
                {
                    _clientNicknames.Remove(client);
                }
            }

            if (!string.IsNullOrWhiteSpace(nickname))
            {
                BroadcastMessage($"[{nickname}] left the chat", client);
            }
        }

        private static void BroadcastMessage(string message, WebSocket? sender)
        {
            var msgBuffer = Encoding.UTF8.GetBytes(message);

            lock (_clients)
            {
                foreach (var client in _clients.ToArray())
                {
                    if (client == sender)
                        continue;

                    if (client.State == WebSocketState.Open)
                    {
                        client.SendAsync(
                            new ArraySegment<byte>(msgBuffer),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        ).Wait();
                    }
                }
            }
        }
    }
}
