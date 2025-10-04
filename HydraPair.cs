using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace HydraServer
{
    internal enum PairState { Stopped, Running, Error }

    internal sealed class HydraPair : IAsyncDisposable
    {
        public int Index { get; }
        public IPEndPoint RoEndPoint { get; }
        public IPEndPoint QryEndPoint { get; }
        public PairState State { get; private set; } = PairState.Stopped;

        Socket? _roListen, _qryListen;
        CancellationTokenSource? _cts;

        readonly ServerType _serverType;
        readonly bool _debug;
        public event Action<HydraPair>? StateChanged;
        public Action<string>? Logger { get; set; }

        int _activeClients;
        int GetServerClientCount() => _activeClients;

        public HydraPair(int index, IPAddress roIp, int roPort, IPAddress qryIp, int qryPort, ServerType st, bool debug)
        {
            Index = index;
            RoEndPoint = new IPEndPoint(roIp, roPort);
            QryEndPoint = new IPEndPoint(qryIp, qryPort);
            _serverType = st;
            _debug = debug;
        }

        public string Summary =>
            $"{Index,2}) RO:{RoEndPoint.Address}:{RoEndPoint.Port,-5}  <->  QRY:{QryEndPoint.Address}:{QryEndPoint.Port,-5}";

        static bool IsLocalAddress(IPAddress ip)
        {
            try
            {
                if (IPAddress.IsLoopback(ip)) return true;
                var addrs =
                    System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Select(a => a.Address)
                    .ToArray();
                return addrs.Any(a => a.Equals(ip));
            }
            catch { return false; }
        }

        static Socket TryBind(IPEndPoint requested, out IPEndPoint boundEp)
        {
            var s = new Socket(requested.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            try
            {
                s.Bind(requested);
                boundEp = (IPEndPoint)s.LocalEndPoint!;
                return s;
            }
            catch
            {
                s.Close();

                if (!IsLocalAddress(requested.Address))
                {
                    var any = new IPEndPoint(IPAddress.Any, requested.Port);
                    var s2 = new Socket(any.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    s2.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    s2.Bind(any);
                    boundEp = (IPEndPoint)s2.LocalEndPoint!;
                    return s2;
                }

                throw;
            }
        }

        public bool CanBindBoth()
        {
            try
            {
                IPEndPoint _;
                using var s1 = TryBind(RoEndPoint, out _);
                using var s2 = TryBind(QryEndPoint, out _);
                return true;
            }
            catch { return false; }
        }

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();

            _roListen = TryBind(RoEndPoint, out var roBound);
            _qryListen = TryBind(QryEndPoint, out var qryBound);

            if (!roBound.Address.Equals(RoEndPoint.Address))
                Logger?.Invoke($"[PAIR{Index}] RO bind em {roBound} (fallback de {RoEndPoint.Address})");
            if (!qryBound.Address.Equals(QryEndPoint.Address))
                Logger?.Invoke($"[PAIR{Index}] QRY bind em {qryBound} (fallback de {QryEndPoint.Address})");

            _roListen.Listen(100);
            _qryListen.Listen(100);

            State = PairState.Running;
            _ = AcceptLoop(_roListen, isRo: true, _cts.Token);
            _ = AcceptLoop(_qryListen, isRo: false, _cts.Token);
            StateChanged?.Invoke(this);
        }

        async System.Threading.Tasks.Task AcceptLoop(Socket listener, bool isRo, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var sock = await listener.AcceptAsync(ct).ConfigureAwait(false);
                    var tag = $"{(isRo ? "RO" : "QRY")}{Index}";

                    IRagnarokHandler handler = isRo
                        ? new RagnarokHandler_RO(_serverType)
                        : new QueryHandler();

                    var sess = new HydraSession(
                        tag, sock, handler, _debug,
                        logger: Logger,
                        getServerClientCount: () => _activeClients);

                    Interlocked.Increment(ref _activeClients);
                    sess.Start();
                    Logger?.Invoke($"[{tag}] conectado de {sock.RemoteEndPoint}");

                    // >>> NÃO chamar DisposeAsync aqui. Espere o loop fechar:
                    _ = sess.WhenClosed.ContinueWith(async _ =>
                    {
                        Interlocked.Decrement(ref _activeClients);
                        Logger?.Invoke($"[{tag}] desconectado");
                        await sess.DisposeAsync();
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Logger?.Invoke($"[PAIR{Index}] accept err: {ex.Message}"); }
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _roListen?.Close(); } catch { }
            try { _qryListen?.Close(); } catch { }
            _roListen = _qryListen = null;
            State = PairState.Stopped;
            StateChanged?.Invoke(this);
        }

        public async System.Threading.Tasks.ValueTask DisposeAsync()
        {
            Stop();
            await System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
