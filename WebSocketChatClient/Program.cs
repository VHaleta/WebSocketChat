using System.Net.WebSockets;
using System.Text;

namespace WebSocketChatClient
{
    internal class Program
    {
        static async Task Main()
        {
            Console.Write("Enter your nickname: ");
            var nickname = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(nickname))
            {
                Console.WriteLine("Nickname cannot be empty. Exiting...");
                return;
            }

            Console.WriteLine("Connecting to the chat...");

            using var webSocket = new ClientWebSocket();

            try
            {
                await webSocket.ConnectAsync(new Uri("ws://localhost:5000/"), CancellationToken.None);
                Console.WriteLine("Successfully connected to the server!");

                await SendMessageAsync(webSocket, nickname);

                _ = Task.Run(async () =>
                {
                    var buffer = new byte[1024];

                    while (webSocket.State == WebSocketState.Open)
                    {
                        try
                        {
                            var result = await webSocket.ReceiveAsync(
                                new ArraySegment<byte>(buffer),
                                CancellationToken.None
                            );

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                Console.WriteLine("Server closed the connection.");
                                await webSocket.CloseAsync(
                                    WebSocketCloseStatus.NormalClosure,
                                    "Server closed the connection",
                                    CancellationToken.None
                                );
                                break;
                            }
                            else
                            {
                                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                                Console.WriteLine(message);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error receiving message: {ex.Message}");
                            break;
                        }
                    }
                });

                Console.WriteLine("Type your message and press Enter to send. Type /exit to leave.");

                while (webSocket.State == WebSocketState.Open)
                {
                    var input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
                        break;

                    await SendMessageAsync(webSocket, input);
                }

                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client closed the connection",
                    CancellationToken.None
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not connect to the server: {ex.Message}");
            }

            Console.WriteLine("Client has exited.");
        }

        private static async Task SendMessageAsync(ClientWebSocket webSocket, string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }
}
