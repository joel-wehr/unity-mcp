using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using UnityEngine;

namespace UnityMcp.Editor.Core
{
    /// <summary>
    /// WebSocket server that listens for MCP client connections.
    /// Handles WebSocket handshake and message framing.
    /// </summary>
    public class McpWebSocketServer : IDisposable
    {
        private TcpListener _listener;
        private Thread _listenerThread;
        private readonly List<McpClientConnection> _clients = new List<McpClientConnection>();
        private bool _isRunning;
        private readonly int _port;
        private readonly string _path;

        public event Action<McpClientConnection, string> OnMessageReceived;
        public event Action<McpClientConnection> OnClientConnected;
        public event Action<McpClientConnection> OnClientDisconnected;

        public bool IsRunning => _isRunning;
        public int Port => _port;
        public int ClientCount => _clients.Count;

        public McpWebSocketServer(int port = 8090, string path = "/McpUnity")
        {
            _port = port;
            _path = path;
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                _listenerThread = new Thread(ListenForClients)
                {
                    IsBackground = true,
                    Name = "MCP WebSocket Listener"
                };
                _listenerThread.Start();

                Debug.Log($"[MCP] WebSocket server started on port {_port}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Failed to start WebSocket server: {ex.Message}");
                _isRunning = false;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;

            lock (_clients)
            {
                foreach (var client in _clients)
                {
                    client.Close();
                }
                _clients.Clear();
            }

            _listener?.Stop();
            _listenerThread?.Join(1000);

            Debug.Log("[MCP] WebSocket server stopped");
        }

        public void SendToAll(string message)
        {
            lock (_clients)
            {
                foreach (var client in _clients)
                {
                    client.Send(message);
                }
            }
        }

        public void Send(McpClientConnection client, string message)
        {
            client?.Send(message);
        }

        private void ListenForClients()
        {
            while (_isRunning)
            {
                try
                {
                    if (_listener.Pending())
                    {
                        var tcpClient = _listener.AcceptTcpClient();
                        var thread = new Thread(() => HandleClient(tcpClient))
                        {
                            IsBackground = true,
                            Name = "MCP Client Handler"
                        };
                        thread.Start();
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Debug.LogError($"[MCP] Error accepting client: {ex.Message}");
                    }
                }
            }
        }

        private void HandleClient(TcpClient tcpClient)
        {
            McpClientConnection client = null;

            try
            {
                var stream = tcpClient.GetStream();

                // Read HTTP request for WebSocket handshake
                var buffer = new byte[4096];
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // Parse request and perform WebSocket handshake
                if (TryWebSocketHandshake(request, stream, out var clientName))
                {
                    client = new McpClientConnection(tcpClient, stream, clientName);

                    lock (_clients)
                    {
                        _clients.Add(client);
                    }

                    // Notify on main thread
                    McpUnityBridge.EnqueueMainThread(() => OnClientConnected?.Invoke(client));

                    Debug.Log($"[MCP] Client connected: {clientName}");

                    // Message loop
                    while (_isRunning && client.IsConnected)
                    {
                        var message = client.ReadMessage();
                        if (message != null)
                        {
                            McpUnityBridge.EnqueueMainThread(() => OnMessageReceived?.Invoke(client, message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Debug.LogWarning($"[MCP] Client error: {ex.Message}");
                }
            }
            finally
            {
                if (client != null)
                {
                    lock (_clients)
                    {
                        _clients.Remove(client);
                    }

                    McpUnityBridge.EnqueueMainThread(() => OnClientDisconnected?.Invoke(client));
                    client.Close();

                    Debug.Log($"[MCP] Client disconnected: {client.Name}");
                }

                tcpClient?.Close();
            }
        }

        private bool TryWebSocketHandshake(string request, NetworkStream stream, out string clientName)
        {
            clientName = "Unknown";

            // Check if this is a WebSocket upgrade request
            if (!request.Contains("Upgrade: websocket"))
            {
                return false;
            }

            // Check path
            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
            var requestLine = lines[0];
            if (!requestLine.Contains(_path))
            {
                return false;
            }

            // Extract Sec-WebSocket-Key
            string webSocketKey = null;
            foreach (var line in lines)
            {
                if (line.StartsWith("Sec-WebSocket-Key:"))
                {
                    webSocketKey = line.Substring(18).Trim();
                }
                else if (line.StartsWith("X-Client-Name:"))
                {
                    clientName = line.Substring(14).Trim();
                    if (string.IsNullOrEmpty(clientName))
                    {
                        clientName = "MCP Client";
                    }
                }
                else if (line.StartsWith("Origin:") && clientName == "Unknown")
                {
                    var origin = line.Substring(7).Trim();
                    if (!string.IsNullOrEmpty(origin))
                    {
                        clientName = origin;
                    }
                }
            }

            if (string.IsNullOrEmpty(webSocketKey))
            {
                return false;
            }

            // Generate accept key
            var acceptKey = GenerateWebSocketAcceptKey(webSocketKey);

            // Send handshake response
            var response = "HTTP/1.1 101 Switching Protocols\r\n" +
                          "Upgrade: websocket\r\n" +
                          "Connection: Upgrade\r\n" +
                          $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

            var responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);

            return true;
        }

        private string GenerateWebSocketAcceptKey(string key)
        {
            const string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            var combined = key + guid;
            var hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(combined));
            return Convert.ToBase64String(hash);
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// Represents a connected MCP client.
    /// </summary>
    public class McpClientConnection
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly object _sendLock = new object();

        public string Name { get; }
        public bool IsConnected => _tcpClient?.Connected ?? false;

        public McpClientConnection(TcpClient tcpClient, NetworkStream stream, string name)
        {
            _tcpClient = tcpClient;
            _stream = stream;
            Name = name;
        }

        public void Send(string message)
        {
            if (!IsConnected) return;

            try
            {
                lock (_sendLock)
                {
                    var messageBytes = Encoding.UTF8.GetBytes(message);
                    var frame = CreateWebSocketFrame(messageBytes);
                    _stream.Write(frame, 0, frame.Length);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP] Send error: {ex.Message}");
            }
        }

        public string ReadMessage()
        {
            try
            {
                if (!_stream.DataAvailable)
                {
                    Thread.Sleep(10);
                    return null;
                }

                var header = new byte[2];
                var bytesRead = _stream.Read(header, 0, 2);
                if (bytesRead < 2) return null;

                var fin = (header[0] & 0x80) != 0;
                var opcode = header[0] & 0x0F;
                var masked = (header[1] & 0x80) != 0;
                var payloadLen = header[1] & 0x7F;

                // Handle close frame
                if (opcode == 8)
                {
                    return null;
                }

                // Handle ping frame
                if (opcode == 9)
                {
                    // Send pong
                    return null;
                }

                // Extended payload length
                ulong actualLength = (ulong)payloadLen;
                if (payloadLen == 126)
                {
                    var extLen = new byte[2];
                    _stream.Read(extLen, 0, 2);
                    actualLength = (ulong)((extLen[0] << 8) | extLen[1]);
                }
                else if (payloadLen == 127)
                {
                    var extLen = new byte[8];
                    _stream.Read(extLen, 0, 8);
                    actualLength = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        actualLength = (actualLength << 8) | extLen[i];
                    }
                }

                // Read mask key
                byte[] maskKey = null;
                if (masked)
                {
                    maskKey = new byte[4];
                    _stream.Read(maskKey, 0, 4);
                }

                // Read payload
                var payload = new byte[actualLength];
                ulong totalRead = 0;
                while (totalRead < actualLength)
                {
                    var toRead = (int)Math.Min(4096, actualLength - totalRead);
                    var read = _stream.Read(payload, (int)totalRead, toRead);
                    if (read == 0) break;
                    totalRead += (ulong)read;
                }

                // Unmask
                if (masked && maskKey != null)
                {
                    for (ulong i = 0; i < actualLength; i++)
                    {
                        payload[i] ^= maskKey[i % 4];
                    }
                }

                return Encoding.UTF8.GetString(payload);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private byte[] CreateWebSocketFrame(byte[] payload)
        {
            var frameLength = payload.Length;
            byte[] frame;

            if (frameLength <= 125)
            {
                frame = new byte[2 + frameLength];
                frame[0] = 0x81; // FIN + Text
                frame[1] = (byte)frameLength;
                Array.Copy(payload, 0, frame, 2, frameLength);
            }
            else if (frameLength <= 65535)
            {
                frame = new byte[4 + frameLength];
                frame[0] = 0x81;
                frame[1] = 126;
                frame[2] = (byte)((frameLength >> 8) & 0xFF);
                frame[3] = (byte)(frameLength & 0xFF);
                Array.Copy(payload, 0, frame, 4, frameLength);
            }
            else
            {
                frame = new byte[10 + frameLength];
                frame[0] = 0x81;
                frame[1] = 127;
                var len = (ulong)frameLength;
                for (int i = 0; i < 8; i++)
                {
                    frame[9 - i] = (byte)(len & 0xFF);
                    len >>= 8;
                }
                Array.Copy(payload, 0, frame, 10, frameLength);
            }

            return frame;
        }

        public void Close()
        {
            try
            {
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch { }
        }
    }
}
