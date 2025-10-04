using System;
using System.Collections.Concurrent;

namespace HydraServer
{
    internal sealed class PoseidonRegistry
    {
        static readonly ConcurrentDictionary<int, PoseidonRegistry> _map = new();
        public static PoseidonRegistry ForPair(int idx) => _map.GetOrAdd(idx, _ => new PoseidonRegistry());

        readonly ConcurrentQueue<byte[]> _responses = new();

        public void SetResponse(byte[] resp, Action<string>? log = null)
        {
            _responses.Enqueue(resp);
            log?.Invoke("[POSEIDON] GG response enfileirada.");
        }

        public bool TryDequeue(out byte[]? resp) => _responses.TryDequeue(out resp);
    }
}
