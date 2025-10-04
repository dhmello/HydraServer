namespace HydraServer;

internal static class HydraPairRegistry
{
    // pairIndex -> callback que envia bytes ao RO
    static readonly Dictionary<int, Func<byte[], Task>> _sendToRo = new();

    public static void RegisterSendToRo(int pairIndex, Func<byte[], Task> sender) =>
        _sendToRo[pairIndex] = sender;

    public static Func<byte[], Task>? GetSendToRo(int pairIndex) =>
        _sendToRo.TryGetValue(pairIndex, out var f) ? f : null;
}
