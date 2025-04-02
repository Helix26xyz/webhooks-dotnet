using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static async Task ConnectWebSocketAsync(string uri)
    {
        using (ClientWebSocket webSocket = new ClientWebSocket())
        {
            try
            {
                await webSocket.ConnectAsync(new Uri(uri), CancellationToken.None);
                Console.WriteLine("Connected to WebSocket server");

                await ReceiveMessages(webSocket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
        }
    }

    private static async Task ReceiveMessages(ClientWebSocket webSocket)
    {
        byte[] buffer = new byte[1024 * 4];

        while (webSocket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"Received: {message}");
        }
    }

    static async Task Main(string[] args)
    {
        string uri = "ws://localhost:5001/websocket";
        await ConnectWebSocketAsync(uri);
    }
}
