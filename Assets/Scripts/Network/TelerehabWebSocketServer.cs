using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Network
{
    /// <summary>
    ///     Flat DTO for every command the dashboard can send. JsonUtility leaves fields
    ///     not present in the message at their defaults, so one DTO covers all actions.
    /// </summary>
    [Serializable]
    public class TelerehabCommandMessage
    {
        public string type;
        public string action;
        public string scene;

        // ping payload (clock-sync): echoed back in the pong response
        public long pingId;

        // configure_session payload
        public string patientId;
        public string exerciseClass;
        public string mode;
        public bool zed = true;
        public bool fsr = true;
        public bool eeg;
        public string fsrConnType;
        public string fsrUsbPort;
        public string fsrUri;
        public string fsrApiKey;
        public string eegComPort;
    }

    /// <summary>
    /// WebSocket server that broadcasts live telerehabilitation data to Flutter dashboards.
    /// Listens on ws://localhost:8765 — no packages required.
    ///
    /// Implemented on a raw TcpListener with a manual RFC 6455 handshake because Mono
    /// (Unity's scripting runtime) does not support HttpListenerContext.AcceptWebSocketAsync:
    /// the call stalls the accept loop on the first client and every later connection
    /// piles up in the TCP backlog until connects are refused.
    /// </summary>
    [DisallowMultipleComponent]
    public class TelerehabWebSocketServer : MonoBehaviour
    {
        private const string WsMagicGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        [Header("Server")]
        public int port = 8765;
        public bool autoStart = true;

        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<TcpClient, ClientConn> _clients = new();

        private class ClientConn
        {
            public TcpClient Tcp;
            public NetworkStream Stream;
            public readonly object SendLock = new();
        }

        public static TelerehabWebSocketServer Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (autoStart) StartServer();
        }

        public void StartServer()
        {
            if (_listener != null) return;
            _cts = new CancellationTokenSource();

            try
            {
                _listener = new TcpListener(IPAddress.Loopback, port);
                _listener.Start();
                Debug.Log($"[TelerehabWS] Server started on ws://localhost:{port}");
                Task.Run(() => AcceptLoop(_cts.Token));
            }
            catch (Exception e)
            {
                _listener = null;
                Debug.LogError($"[TelerehabWS] Failed to start on port {port}: {e.Message} — " +
                               "another process already holds the port (a second Unity instance, " +
                               "the standalone build, or a previous Play session that didn't clean up). " +
                               "Close it or restart the editor.");
            }
        }

        public void StopServer()
        {
            _cts?.Cancel();

            foreach (var kv in _clients)
            {
                try { kv.Key.Close(); } catch { /* already gone */ }
            }
            _clients.Clear();

            try { _listener?.Stop(); } catch { /* already gone */ }
            _listener = null;
            Debug.Log("[TelerehabWS] Server stopped");
        }

        /// <summary>Broadcast a JSON string to all connected Flutter clients.</summary>
        public void Broadcast(string json)
        {
            if (_clients.IsEmpty) return;
            var frame = EncodeFrame(0x1, Encoding.UTF8.GetBytes(json));

            // Off the main thread so a slow client can never stall Unity.
            Task.Run(() =>
            {
                foreach (var kv in _clients)
                {
                    var c = kv.Value;
                    try
                    {
                        lock (c.SendLock) { c.Stream.Write(frame, 0, frame.Length); }
                    }
                    catch
                    {
                        Drop(c);
                    }
                }
            });
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient tcp;
                try
                {
                    tcp = await _listener.AcceptTcpClientAsync();
                }
                catch
                {
                    if (ct.IsCancellationRequested) break;
                    continue;
                }
                // Handshake runs per client so one bad connection never blocks the loop.
                _ = Task.Run(() => HandleClient(tcp, ct));
            }
        }

        private async Task HandleClient(TcpClient tcp, CancellationToken ct)
        {
            var conn = new ClientConn { Tcp = tcp };
            try
            {
                tcp.NoDelay = true;
                conn.Stream = tcp.GetStream();

                if (!await DoHandshake(conn.Stream, ct))
                {
                    tcp.Close();
                    return;
                }

                Debug.Log($"[TelerehabWS] Client connected from {tcp.Client.RemoteEndPoint}");
                _clients[tcp] = conn;
                await ReadLoop(conn, ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning($"[TelerehabWS] Client error: {e.Message}");
            }
            finally
            {
                Drop(conn);
            }
        }

        private static async Task<bool> DoHandshake(NetworkStream stream, CancellationToken ct)
        {
            var buf = new byte[8192];
            int len = 0;
            while (!HeaderEnd(buf, len))
            {
                if (len == buf.Length) return false; // oversized request
                int n = await stream.ReadAsync(buf, len, buf.Length - len, ct);
                if (n <= 0) return false;
                len += n;
            }

            var request = Encoding.ASCII.GetString(buf, 0, len);
            var key = ExtractHeader(request, "Sec-WebSocket-Key");
            if (key == null || !request.StartsWith("GET", StringComparison.Ordinal))
            {
                var bad = Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(bad, 0, bad.Length, ct);
                return false;
            }

            string accept;
            using (var sha1 = SHA1.Create())
            {
                accept = Convert.ToBase64String(
                    sha1.ComputeHash(Encoding.ASCII.GetBytes(key + WsMagicGuid)));
            }

            var resp = Encoding.ASCII.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Accept: " + accept + "\r\n\r\n");
            await stream.WriteAsync(resp, 0, resp.Length, ct);
            return true;
        }

        private async Task ReadLoop(ClientConn c, CancellationToken ct)
        {
            var header = new byte[8];
            var mask = new byte[4];
            var message = new MemoryStream();

            while (!ct.IsCancellationRequested)
            {
                if (!await ReadExact(c.Stream, header, 2, ct)) return;
                bool fin = (header[0] & 0x80) != 0;
                int opcode = header[0] & 0x0F;
                bool masked = (header[1] & 0x80) != 0;
                long payloadLen = header[1] & 0x7F;

                if (payloadLen == 126)
                {
                    if (!await ReadExact(c.Stream, header, 2, ct)) return;
                    payloadLen = (header[0] << 8) | header[1];
                }
                else if (payloadLen == 127)
                {
                    if (!await ReadExact(c.Stream, header, 8, ct)) return;
                    payloadLen = 0;
                    for (int i = 0; i < 8; i++) payloadLen = (payloadLen << 8) | header[i];
                }
                if (payloadLen < 0 || payloadLen > (1 << 20)) return; // 1 MB sanity cap

                if (masked && !await ReadExact(c.Stream, mask, 4, ct)) return;

                var payload = new byte[payloadLen];
                if (payloadLen > 0 && !await ReadExact(c.Stream, payload, (int)payloadLen, ct)) return;
                if (masked)
                {
                    for (int i = 0; i < payload.Length; i++) payload[i] ^= mask[i % 4];
                }

                switch (opcode)
                {
                    case 0x8: // close → echo close, then drop
                        SendFrame(c, 0x8, payload);
                        return;
                    case 0x9: // ping → pong
                        SendFrame(c, 0xA, payload);
                        break;
                    case 0x1: // text (possibly fragmented)
                    case 0x0: // continuation
                        message.Write(payload, 0, payload.Length);
                        if (fin)
                        {
                            var msg = Encoding.UTF8.GetString(message.ToArray());
                            message.SetLength(0);
                            // Dispatch on the Unity main thread
                            TelerehabDispatcher.Enqueue(() => HandleCommand(msg));
                        }
                        break;
                    // binary (0x2) and pong (0xA) are ignored
                }
            }
        }

        private void SendFrame(ClientConn c, int opcode, byte[] payload)
        {
            try
            {
                var frame = EncodeFrame(opcode, payload ?? Array.Empty<byte>());
                lock (c.SendLock) { c.Stream.Write(frame, 0, frame.Length); }
            }
            catch
            {
                Drop(c);
            }
        }

        /// <summary>Server-to-client frames are unmasked per RFC 6455.</summary>
        private static byte[] EncodeFrame(int opcode, byte[] payload)
        {
            int len = payload.Length;
            using var ms = new MemoryStream(len + 10);
            ms.WriteByte((byte)(0x80 | opcode)); // FIN + opcode
            if (len < 126)
            {
                ms.WriteByte((byte)len);
            }
            else if (len <= ushort.MaxValue)
            {
                ms.WriteByte(126);
                ms.WriteByte((byte)(len >> 8));
                ms.WriteByte((byte)len);
            }
            else
            {
                ms.WriteByte(127);
                for (int i = 7; i >= 0; i--) ms.WriteByte((byte)((long)len >> (8 * i)));
            }
            ms.Write(payload, 0, len);
            return ms.ToArray();
        }

        private static async Task<bool> ReadExact(NetworkStream s, byte[] buf, int count, CancellationToken ct)
        {
            int off = 0;
            while (off < count)
            {
                int n = await s.ReadAsync(buf, off, count - off, ct);
                if (n <= 0) return false;
                off += n;
            }
            return true;
        }

        private static bool HeaderEnd(byte[] buf, int len)
        {
            for (int i = 3; i < len; i++)
            {
                if (buf[i - 3] == '\r' && buf[i - 2] == '\n' &&
                    buf[i - 1] == '\r' && buf[i] == '\n') return true;
            }
            return false;
        }

        private static string ExtractHeader(string request, string name)
        {
            foreach (var line in request.Split('\n'))
            {
                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                if (line.Substring(0, colon).Trim()
                        .Equals(name, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(colon + 1).Trim();
            }
            return null;
        }

        private void Drop(ClientConn c)
        {
            if (c?.Tcp == null) return;
            _clients.TryRemove(c.Tcp, out _);
            try { c.Tcp.Close(); } catch { /* already gone */ }
        }

        private void HandleCommand(string json)
        {
            TelerehabCommandMessage msg;
            try
            {
                msg = JsonUtility.FromJson<TelerehabCommandMessage>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TelerehabWS] Command parse error: {e.Message} in {json}");
                return;
            }

            if (msg == null || string.IsNullOrEmpty(msg.action)) return;
            Debug.Log($"[TelerehabWS] Command: {msg.action}");

            switch (msg.action)
            {
                case "start_recording":
                    TelerehabCommandBus.Raise(TelerehabCommand.StartRecording);
                    break;
                case "stop_recording":
                    TelerehabCommandBus.Raise(TelerehabCommand.StopRecording);
                    break;
                case "pause_recording":
                    TelerehabCommandBus.Raise(TelerehabCommand.PauseRecording);
                    break;
                case "mark_event":
                    TelerehabCommandBus.Raise(TelerehabCommand.MarkEvent);
                    break;
                case "rebaseline":
                    TelerehabCommandBus.Raise(TelerehabCommand.Rebaseline);
                    break;
                case "ping":
                    // Runs on the main thread (dispatched), so reading the record
                    // clock is safe. Echo the id + Unity's record-clock ms so the
                    // dashboard can measure the offset and align its markers.
                    Broadcast("{\"type\":\"pong\",\"pingId\":" + msg.pingId +
                              ",\"unityRecMs\":" + TelerehabBroadcaster.RecordingClockMs + "}");
                    break;
                case "set_rest_position":
                    TelerehabCommandBus.Raise(TelerehabCommand.SetRestPosition);
                    break;
                case "start_exercise":
                    TelerehabCommandBus.Raise(TelerehabCommand.StartExercise, json);
                    break;
                case "load_scene":
                    TelerehabCommandBus.Raise(TelerehabCommand.LoadScene, json);
                    break;
                case "configure_session":
                    RemoteSessionController.Configure(msg);
                    Broadcast("{\"type\":\"ack\",\"action\":\"configure_session\",\"ok\":true}");
                    break;
                case "start_session":
                    var error = RemoteSessionController.StartSession();
                    Broadcast(error == null
                        ? "{\"type\":\"ack\",\"action\":\"start_session\",\"ok\":true}"
                        : "{\"type\":\"ack\",\"action\":\"start_session\",\"ok\":false,\"error\":\"" +
                          EscapeJson(error) + "\"}");
                    break;
                default:
                    Debug.LogWarning($"[TelerehabWS] Unknown action: {msg.action}");
                    break;
            }
        }

        public static string EscapeJson(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        private void OnDestroy()
        {
            StopServer();
            // Only the owning instance may clear the singleton — a destroyed
            // duplicate must not null out the live server's reference.
            if (Instance == this) Instance = null;
        }
    }
}
