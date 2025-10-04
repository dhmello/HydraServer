using System.Net;
using System.Net.NetworkInformation;

namespace HydraServer;

internal sealed class HydraConfig
{
    public IPAddress RagnarokIp { get; init; } = IPAddress.Any;              // default 0.0.0.0
    public IPAddress QueryIp { get; init; } = IPAddress.Loopback;         // default 127.0.0.1
    public List<int> RoPorts { get; init; } = new();
    public List<int> QryPorts { get; init; } = new();
    public string? ServerType { get; init; }
    public string? FakeIp { get; init; }
    public bool Debug { get; init; }

    // Avisos estilo Perl (para você imprimir no log)
    public List<string> Warnings { get; } = new();

    public static HydraConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Arquivo de configuração não encontrado.", path);

        var lines = File.ReadAllLines(path);
        string? Get(string key)
            => lines.FirstOrDefault(l => l.TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
               ?.Split('=', 2)[1].Trim();

        // O Perl aceita *_ports OU *_port (CSV). Prioriza *_ports.
        string? roList = Get("ragnarokserver_ports") ?? Get("ragnarokserver_port");
        string? qsList = Get("queryserver_ports") ?? Get("queryserver_port");

        // Defaults de bind (aplicados somente se vazio)
        var roIpStr = Get("ragnarokserver_ip");
        var qsIpStr = Get("queryserver_ip");
        if (string.IsNullOrWhiteSpace(roIpStr)) roIpStr = "0.0.0.0";     // default Perl
        if (string.IsNullOrWhiteSpace(qsIpStr)) qsIpStr = "127.0.0.1";   // default Perl

        var cfg = new HydraConfig
        {
            RagnarokIp = ParseIp(roIpStr, IPAddress.Any),
            QueryIp = ParseIp(qsIpStr, IPAddress.Loopback),
            RoPorts = ParsePorts(roList),
            QryPorts = ParsePorts(qsList),
            ServerType = Get("server_type"),
            FakeIp = Get("fake_ip"),
            Debug = ToBool(Get("debug"))
        };

        // Avisos: IP pode não ser local (não força override)
        if (!IsLocalBindIp(cfg.RagnarokIp))
            cfg.Warnings.Add($"Config: ragnarokserver_ip '{roIpStr}' pode não ser IP local. Tentando mesmo assim.");
        if (!IsLocalBindIp(cfg.QueryIp))
            cfg.Warnings.Add($"Config: queryserver_ip '{qsIpStr}' pode não ser IP local. Tentando mesmo assim.");

        // Validações equivalentes ao Perl
        if (cfg.RoPorts.Count == 0)
            throw new InvalidOperationException("Config: ragnarokserver_ports vazio");
        if (cfg.QryPorts.Count == 0)
            throw new InvalidOperationException("Config: queryserver_ports vazio");
        if (cfg.RoPorts.Count != cfg.QryPorts.Count)
            throw new InvalidOperationException($"Config: quantidade de ragnarokserver_ports ({cfg.RoPorts.Count}) difere de queryserver_ports ({cfg.QryPorts.Count})");

        return cfg;
    }

    // ----------------- helpers -----------------

    static IPAddress ParseIp(string? s, IPAddress @default)
        => (!string.IsNullOrWhiteSpace(s) && IPAddress.TryParse(s, out var ip)) ? ip : @default;

    static List<int> ParsePorts(string? csv)
    {
        var res = new List<int>();
        if (string.IsNullOrWhiteSpace(csv)) return res;

        var seen = new HashSet<int>();
        foreach (var p in csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(p, out var n) && n > 0 && n < 65536 && seen.Add(n))
                res.Add(n);
        return res;
    }

    static bool ToBool(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return false;
        v = v.Trim();
        return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsLocalBindIp(IPAddress ip)
    {
        // 0.0.0.0 e 127.0.0.1 são "locais" por definição
        if (IPAddress.Any.Equals(ip) || IPAddress.Loopback.Equals(ip)) return true;

        // redes privadas comuns
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 10) return true;
            if (b[0] == 192 && b[1] == 168) return true;
            if (b[0] == 172 && (b[1] >= 16 && b[1] <= 31)) return true;
        }

        // Verifica IPs locais pelas interfaces ativas
        try
        {
            var addrs = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .ToArray();

            return addrs.Any(a => a.Equals(ip));
        }
        catch
        {
            return false;
        }
    }
}
