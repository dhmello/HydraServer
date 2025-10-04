using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HydraServer
{
    internal sealed class HydraSession : IAsyncDisposable
    {
        public string Tag { get; }
        public bool Debug { get; }
        public string BindIp { get; }
        public int BindPort { get; }
        public int ServerClientCount => _getServerClientCount?.Invoke() ?? 1;

        public readonly Dictionary<string, object?> Items = new();

        readonly Socket _sock;
        readonly IRagnarokHandler _handler;
        readonly CancellationTokenSource _cts = new();
        readonly Func<int>? _getServerClientCount;
        Action<string>? _logger;

        // Novo: sinaliza quando o loop terminou (para o AcceptLoop saber quando logar “desconectado”)
        readonly TaskCompletionSource _closedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task WhenClosed => _closedTcs.Task;

        public HydraSession(string tag, Socket sock, IRagnarokHandler handler, bool debug, Action<string>? logger = null, Func<int>? getServerClientCount = null)
        {
            Tag = tag;
            _sock = sock;
            _handler = handler;
            Debug = debug;
            _logger = logger;
            _getServerClientCount = getServerClientCount;

            var lep = (IPEndPoint)sock.LocalEndPoint!;
            BindIp = lep.Address.ToString();
            BindPort = lep.Port;
        }

        public void SetLogger(Action<string>? log) => _logger = log;

        public void Log(string msg)
        {
            try { _logger?.Invoke($"[{Tag}] {msg}"); } catch { }
        }

        public void Start()
        {
            _ = Task.Run(LoopAsync);
        }

        async Task LoopAsync()
        {
            var ct = _cts.Token;
            var buf = new byte[64 * 1024];

            try
            {
                if (Debug) Log("loop RX iniciado");
                while (!ct.IsCancellationRequested)
                {
                    int n = await _sock.ReceiveAsync(buf, SocketFlags.None, ct);
                    if (n <= 0) break;

                    if (Debug) Log($"<= {Hex(buf.AsSpan(0, n).ToArray())}");

                    var ok = await _handler.OnClientDataAsync(this, new ReadOnlyMemory<byte>(buf, 0, n), ct);
                    if (!ok) break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log($"erro RX: {ex.Message}");
            }
            finally
            {
                try { _sock.Shutdown(SocketShutdown.Both); } catch { }
                try { _sock.Close(); } catch { }
                if (Debug) Log("loop RX finalizado");
                _closedTcs.TrySetResult();
            }
        }

        public async Task SendAsync(byte[] data, CancellationToken ct)
        {
            await _sock.SendAsync(data, SocketFlags.None, ct);
            if (Debug) Log($"=> {Hex(data)}");
        }

        static string Hex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (var x in b) sb.Append(x.ToString("X2"));
            return sb.ToString();
        }

        public ValueTask DisposeAsync()
        {
            try { _cts.Cancel(); } catch { }
            try { _sock?.Dispose(); } catch { }
            return ValueTask.CompletedTask;
        }
    }
}
