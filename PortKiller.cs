using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace HydraServer;

internal static class PortKiller
{
    public static void FreeRequestedPorts(IEnumerable<int> ports, Action<string>? logInfo = null, Action<string>? logErr = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var want = ports.Distinct().OrderBy(p => p).ToArray();
        var pidToPorts = new Dictionary<int, HashSet<int>>();

        foreach (var p in want)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", $"/c netstat -ano -p tcp | findstr :{p}")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var proc = Process.Start(psi)!;
                var outp = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(2000);
                foreach (var ln in outp.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!ln.Contains("LISTENING", StringComparison.OrdinalIgnoreCase)) continue;
                    var mt = Regex.Match(ln, @"LISTENING\s+(\d+)\s*$", RegexOptions.IgnoreCase);
                    if (!mt.Success) continue;
                    int pid = int.Parse(mt.Groups[1].Value);
                    if (pid == Environment.ProcessId || pid == 0) continue;
                    if (!pidToPorts.TryGetValue(pid, out var set)) pidToPorts[pid] = set = new();
                    set.Add(p);
                }
            }
            catch { /* ignore */ }
        }

        foreach (var kv in pidToPorts)
        {
            var portsStr = string.Join(",", kv.Value.OrderBy(x => x));
            logInfo?.Invoke($"[port-free] Matando PID {kv.Key} (ports: {portsStr})…");
            try
            {
                var psi = new ProcessStartInfo("taskkill", $"/F /PID {kv.Key}")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var proc = Process.Start(psi)!;
                proc.WaitForExit(2000);
            }
            catch { logErr?.Invoke($"Falha ao matar PID {kv.Key}"); }
            Thread.Sleep(200);
        }
    }
}
