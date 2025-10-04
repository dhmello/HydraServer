using System;

namespace HydraServer
{
    // Guarda a última resposta de GameGuard para um par.
    internal sealed class PoseidonROServer
    {
        byte[]? _lastResponse;
        readonly object _lock = new();

        public void SetResponse(byte[] packet, Action<string>? log = null)
        {
            lock (_lock) _lastResponse = packet;
            log?.Invoke($"[GG] resposta armazenada ({packet.Length} bytes)");
        }

        public byte[]? TryConsumeResponse(Action<string>? log = null)
        {
            lock (_lock)
            {
                if (_lastResponse == null) return null;
                var r = _lastResponse;
                _lastResponse = null;
                log?.Invoke("[GG] resposta entregue ao Query (consumida)");
                return r;
            }
        }
    }
}
