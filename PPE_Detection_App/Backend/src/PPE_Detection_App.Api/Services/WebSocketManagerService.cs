using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace PPE_Detection_App.Api.Services
{
    public class WebSocketManagerService
    {
        private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
        private readonly ILogger<WebSocketManagerService> _logger;

        public WebSocketManagerService(ILogger<WebSocketManagerService> logger)
        {
            _logger = logger;
        }

        public string AddSocket(WebSocket socket)
        {
            var socketId = Guid.NewGuid().ToString();
            _sockets.TryAdd(socketId, socket);
            _logger.LogInformation($"WebSocket {socketId} added. Total connections: {_sockets.Count}");
            return socketId;
        }

        public async Task RemoveSocket(string socketId)
        {
            if (_sockets.TryRemove(socketId, out var socket))
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed by server",
                        CancellationToken.None);
                }
                socket.Dispose();
                _logger.LogInformation($"WebSocket {socketId} removed. Total connections: {_sockets.Count}");
            }
        }

        public async Task HandleWebSocketAsync(WebSocket webSocket)
        {
            var socketId = AddSocket(webSocket);
            _logger.LogInformation($"WebSocket connection {socketId} established");

            var buffer = new byte[1024 * 4];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation($"WebSocket {socketId} close message received");
                        break;
                    }

                    // Echo back if needed (for ping/pong)
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        _logger.LogDebug($"Received message from {socketId}: {message}");
                    }
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, $"WebSocket {socketId} connection error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error in WebSocket {socketId}");
            }
            finally
            {
                await RemoveSocket(socketId);
            }
        }

        public async Task BroadcastMessage(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var tasks = new List<Task>();
            var deadSockets = new List<string>();

            foreach (var kvp in _sockets)
            {
                var socket = kvp.Value;
                var socketId = kvp.Key;

                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        tasks.Add(socket.SendAsync(
                            new ArraySegment<byte>(buffer),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to send to socket {socketId}");
                        deadSockets.Add(socketId);
                    }
                }
                else
                {
                    deadSockets.Add(socketId);
                }
            }

            // Clean up dead sockets
            foreach (var socketId in deadSockets)
            {
                await RemoveSocket(socketId);
            }

            if (tasks.Any())
            {
                await Task.WhenAll(tasks);
            }
        }

        public int GetConnectionCount() => _sockets.Count;

        public IEnumerable<string> GetActiveConnections() => _sockets.Keys;
    }
}