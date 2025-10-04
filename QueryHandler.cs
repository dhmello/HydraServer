using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HydraServer
{
    internal sealed class QueryHandler : IRagnarokHandler
    {
        public void Tick(HydraSession session) { }

        public async ValueTask<bool> OnClientDataAsync(HydraSession s, ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            s.Log($"[QRY] {data.Length} bytes recebidos");
            // Se quiser responder algo, faça aqui.
            // Exemplo: se houver resposta GG pendente:
            if (PoseidonRegistry.ForPair(ExtractPairIndexFromTag(s.Tag)).TryDequeue(out var resp) && resp != null)
            {
                await s.SendAsync(resp, ct);
                s.Log("[QRY] Enviado GG reply para o cliente Poseidon.");
            }
            return true;
        }

        static int ExtractPairIndexFromTag(string? tag)
        {
            if (string.IsNullOrEmpty(tag)) return 1;
            char c = tag![tag.Length - 1];
            return (c >= '1' && c <= '9') ? (c - '0') : 1;
        }
    }
}
