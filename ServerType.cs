using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HydraServer
{
    // Representa 1 seção de servertypes.txt
    internal sealed class ServerType
    {
        public string? Name { get; init; }
        public string? MapLoaded { get; init; }
        public string? ConfirmLoad { get; init; }
        public string? ReceivedCharacterIdAndMap { get; init; }
        public string? AccountServerInfo { get; init; }
        public string? MapLogin { get; init; }
        public string? ReceivedCharacters { get; init; }
        public int CharBlockSize { get; init; } = 116;
        public string? SendCryptKeys { get; init; }
        public byte Mode { get; set; } // Adicionado para armazenar o valor de msg[2]

        public override string ToString() => Name ?? base.ToString()!;
    }

    internal sealed class ServerTypeDatabase
    {
        private readonly Dictionary<string, ServerType> _byName = new(StringComparer.OrdinalIgnoreCase);

        public static ServerTypeDatabase LoadFromFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("servertypes.txt não encontrado", path);
            var db = new ServerTypeDatabase();

            string? section = null;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    // fecha seção anterior
                    if (section != null) db.AddSection(section, dict);
                    section = line[1..^1].Trim();
                    dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line[..eq].Trim();
                var val = line[(eq + 1)..].Trim();
                dict[key] = val;
            }

            if (section != null) db.AddSection(section, dict);
            return db;
        }

        void AddSection(string name, Dictionary<string, string> kv)
        {
            kv.TryGetValue("map_loaded", out var mapLoaded);
            kv.TryGetValue("confirm_load", out var confirm);
            kv.TryGetValue("received_character_ID_and_Map", out var rcidmap);
            kv.TryGetValue("account_server_info", out var asi);
            kv.TryGetValue("map_login", out var mapLogin);
            kv.TryGetValue("received_characters", out var rchars);
            int charBlockSize = 116;
            if (kv.TryGetValue("charBlockSize", out var cbs) && int.TryParse(cbs, out var n) && n > 0) charBlockSize = n;

            var st = new ServerType
            {
                Name = name,
                MapLoaded = mapLoaded,
                ConfirmLoad = confirm,
                ReceivedCharacterIdAndMap = rcidmap,
                AccountServerInfo = asi,
                MapLogin = mapLogin,
                ReceivedCharacters = rchars,
                CharBlockSize = charBlockSize
            };

            _byName[name] = st;
        }

        public bool TryGet(string name, out ServerType st) => _byName.TryGetValue(name, out st!);
    }
}
