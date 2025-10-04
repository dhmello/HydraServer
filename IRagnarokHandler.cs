using System;
using System.Threading;
using System.Threading.Tasks;

namespace HydraServer
{
    internal interface IRagnarokHandler
    {
        void Tick(HydraSession session);
        ValueTask<bool> OnClientDataAsync(HydraSession session, ReadOnlyMemory<byte> data, CancellationToken ct);
    }
}
