using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HydraServer
{
    /// Emulador de servidor RO (porte do Poseidon RagnarokServer.pm)
    internal sealed class RagnarokHandler_RO : IRagnarokHandler
    {
        // IDs/sessões “fake”, iguais ao Poseidon
        static readonly byte[] AccountID = Pack.U32(2000001);
        static readonly byte[] CharID = Pack.U32(100001);
        static readonly byte[] SessionID = Pack.U32(3000000000u);
        static readonly byte[] SessionID2 = Pack.U32(0xFF);

        const int POS_X = 221, POS_Y = 128;

        readonly ServerType _st;
        public RagnarokHandler_RO(ServerType st) => _st = st;

        // ---- estado por conexão ----
        sealed class RoState
        {
            public bool ConnectedToMap;
            public int ChallengeNum;
        }

        public void Tick(HydraSession session) { /* opcional */ }

        // ===================== PARSER =====================
        public async ValueTask<bool> OnClientDataAsync(HydraSession session, ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            var st = GetState(session);

            // trabalhe em cima de array, sem Span persistente (C#12 safe)
            var buf = data.ToArray();
            int off = 0, n = buf.Length;

            while (off + 2 <= n)
            {
                ushort op = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(buf, off, 2));
                int len = ReadLen(buf, off);
                if (len <= 0 || off + len > n) break;

                var msg = new byte[len];
                Buffer.BlockCopy(buf, off, msg, 0, len);
                off += len;

                session.Log($"[RO] <= {Hex(msg)}  (0x{op:X4}, {len} bytes)");
                await Handle(session, st, op, msg, ct);
            }
            return true;
        }

        // =================================================

        async Task Handle(HydraSession s, RoState st, ushort op, byte[] msg, CancellationToken ct)
        {
            string sw = op.ToString("X4");

            // ===== GameGuard replies (0228/09D0/02A7) -> publicar para o Query =====
            if (op == 0x09D0 || op == 0x0228 || op == 0x02A7)
            {
                var resp = BuildGGResponse(op, msg);
                PoseidonRegistry.ForPair(ExtractPairIndexFromTag(s.Tag)).SetResponse(resp, s.Log);
                s.Log("[RO] GG response registrada no PoseidonRegistry");
                return;
            }

            // ===== secure_login (01DB/0204) -> secure_login_reply (01DC) =====
            if (sw == "01DB" || sw == "0204")
            {
                // 01DC com length=0x14 e 4 dwords zero (igual Poseidon)
                var reply = Pack.Join(
                    Pack.U16(0x01DC),      // opcode
                    Pack.U16(0x14),        // length 20
                    Pack.U32(0),           // result
                    Pack.U32(0),           // flag
                    Pack.U32(0),
                    Pack.U32(0)
                );
                await Send(s, reply, ct);
                s.Log("[RO] => secure_login_reply (01DC)");

                // já manda “account server info” pra liberar a lista de servidores
                var asi = BuildAccountServerInfo(s);
                await Send(s, asi, ct);
                s.Log("[RO] => account_server_info enviado");
                return;
            }

            // ===== token ===== (0ACF/0C26 -> 0AE3)
            if (sw == "0ACF" || sw == "0C26")
            {
                var w = new Buf();
                w.U16(0x0AE3).U16(0x2F).S32(0)   // login_type
                 .Z(20, "S1000")                 // flag
                 .Z(0, "OpenkoreClientToken");   // login_token
                await Send(s, w.ToArray(), ct);
                s.Log("[RO] => token_reply (0AE3)");
                return;
            }

            // ===== gameguard challenge (0258) -> grant (0259) =====
            if (sw == "0258")
            {
                byte grant = (byte)(st.ChallengeNum == 0 ? 0x01 : 0x02); // 1: account, 2: char/map
                await Send(s, new byte[] { 0x59, 0x02, grant }, ct);     // 0x0259
                st.ChallengeNum++;
                s.Log($"[RO] 0258 -> 0259 (grant={grant})");
                return;
            }

            // ===== master_login -> account_server_info =====
            if (IsMasterLogin(sw))
            {
                await Send(s, BuildAccountServerInfo(s), ct);
                s.Log("[RO] => account_server_info (por master_login)");
                return;
            }

            // ===== server choice -> lista de personagens =====
            if (sw == "0065" || sw == "0275" || HeurServerChoice(msg))
            {
                await Send(s, BuildCharacterList(), ct);
                s.Log("[RO] => character_list");
                return;
            }

            if (sw == "09A1")
            {
                await Send(s, BuildCharacterList(), ct);
                s.Log("[RO] => character_list (09A1)");
                return;
            }

            // ===== character choose -> map select =====
            if (sw == "0066")
            {
                await Send(s, BuildCharMapSelect(s), ct);
                s.Log("[RO] => char_map_select");
                return;
            }

            // ===== map_login =====
            if (IsMapLogin(sw))
            {
                await HandleMapLogin(s, st, ct);
                return;
            }

            // ===== sync =====
            if (IsSync(sw))
            {
                await Send(s, Pack.Join(Pack.Bytes(0x7F, 0x00), Pack.U32((uint)Environment.TickCount)), ct);
                s.Log("[RO] => sync_reply");
                return;
            }

            // ===== outros pacotes “ping/quit” =====
            if (sw == "00B2") { await Send(s, Pack.Join(Pack.U16(0x00B3), Pack.U16(1)), ct); s.Log("[RO] => go_char"); return; }
            if (sw == "018A") { await Send(s, Pack.Join(Pack.U16(0x018B), Pack.U16(0)), ct); s.Log("[RO] => quit_ok"); return; }
            if (sw == "0B1C") { await Send(s, Pack.U16(0x0B1D), ct); s.Log("[RO] => pong"); return; }

            s.Log($"[RO] ignorado 0x{sw} ({msg.Length} bytes)");
        }

        async Task HandleMapLogin(HydraSession s, RoState st, CancellationToken ct)
        {
            await Send(s, Pack.Join(Pack.U16(0x0283), AccountID), ct); // map_login_ok (parcial)

            if ((_st.Name?.StartsWith("kRO", StringComparison.OrdinalIgnoreCase) ?? false))
                await Send(s, Pack.Join(Pack.U16(0x0ADE), Pack.U32(0)), ct);

            byte[] loaded;
            var ml = _st.MapLoaded?.ToUpperInvariant();
            if (ml == "0A18")
                loaded = Pack.Join(Pack.U16(0x0A18), Pack.U32((uint)Environment.TickCount), Coord(POS_X, POS_Y), Pack.Bytes(0, 0), Pack.U16(0), Pack.U8(1));
            else if (ml == "02EB")
                loaded = Pack.Join(Pack.U16(0x02EB), Pack.U32((uint)Environment.TickCount), Coord(POS_X, POS_Y), Pack.Bytes(0, 0));
            else
                loaded = Pack.Join(Pack.U16(0x0073), Pack.U32((uint)Environment.TickCount), Coord(POS_X, POS_Y), Pack.Bytes(0, 0));

            await Send(s, loaded, ct);

            // attack_range + stats (dummy)
            await Send(s, Pack.Join(Pack.U16(0x013A), Pack.U16(1)), ct);
            await Send(s, Pack.Join(Pack.U16(0x00BD), Pack.Zero(2 + 12 + 14 * 2)), ct);

            if (string.Equals(_st.ConfirmLoad, "0B1B", StringComparison.OrdinalIgnoreCase))
                await Send(s, Pack.U16(0x0B1B), ct);

            st.ConnectedToMap = true;
            s.Log("[RO] => map_loaded + stats + confirm_load");
        }

        byte[] BuildCharMapSelect(HydraSession s)
        {
            var (a, b, c, d, port) = BindIpPort(s);
            string map = "brasilis.gat";
            if (string.Equals(_st.ReceivedCharacterIdAndMap, "0AC5", StringComparison.OrdinalIgnoreCase))
                return Pack.Join(Pack.U16(0x0AC5), CharID, Pack.Z(16, map), Pack.Bytes(a, b, c, d), port, Pack.Zero(128));
            return Pack.Join(Pack.U16(0x0071), CharID, Pack.Z(16, map), Pack.Bytes(a, b, c, d), port);
        }

        byte[] BuildAccountServerInfo(HydraSession s)
        {
            string asi = _st.AccountServerInfo?.ToUpperInvariant() ?? "";
            var (a, b, c, d, port) = BindIpPort(s);
            byte sex = 1;
            var serverName = Pack.Z(20, "openkore.com.br");

            // fallback seguro caso a contagem não exista
            int usersCount = Math.Max(0, s.ServerClientCount - 1);
            var users32 = Pack.U32((uint)usersCount);

            if (asi == "0AC9" || asi == "0AAC")
                return Pack.Join(
                    Pack.U16(0x0AC9), Pack.U16(0xCF),
                    SessionID, AccountID, SessionID2,
                    Pack.Zero(4),
                    Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                    Pack.U8(sex), Pack.Zero(6),
                    serverName, users32, Pack.Bytes(0x80, 0x32),
                    Pack.ASCII($"{s.BindIp}:{s.BindPort}"),
                    Pack.Zero(114)
                );

            if (asi == "0B07" || asi == "0B04")
                return Pack.Join(
                    Pack.U16(0x0B07), Pack.U16(0xCF),
                    SessionID, AccountID, SessionID2,
                    Pack.Zero(4),
                    Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                    Pack.U8(sex),
                    Pack.Bytes(a, b, c, d), port,
                    serverName, users32, Pack.Zero(130)
                );

            if (asi == "0B60")
                return Pack.Join(
                    Pack.U16(0x0B60), Pack.U16(0xE4),
                    SessionID, AccountID, SessionID2,
                    Pack.Zero(4),
                    Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                    Pack.U8(sex), Pack.Zero(0x11),
                    Pack.Bytes(a, b, c, d), port,
                    serverName, Pack.Zero(2),
                    Pack.U16((ushort)Math.Min(ushort.MaxValue, usersCount)),
                    Pack.U16(0x6985),
                    Pack.Zero(128 + 4)
                );

            if (asi == "0AC4")
                return Pack.Join(
                    Pack.U16(0x0AC4), Pack.U16(0xE0),
                    SessionID, AccountID, SessionID2,
                    Pack.Zero(4),
                    Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                    Pack.U8(sex), Pack.Zero(0x11),
                    Pack.Bytes(a, b, c, d), port,
                    serverName, users32, Pack.Zero(130)
                );

            if (asi == "0276")
                return Pack.Join(
                    Pack.U16(0x0276), Pack.U16(0x63),
                    SessionID, AccountID, SessionID2,
                    Pack.Zero(30), Pack.U8(sex), Pack.Zero(4),
                    Pack.Bytes(a, b, c, d), port,
                    serverName, Pack.Zero(2), users32, Pack.Zero(6)
                );

            // default 0069
            return Pack.Join(
                Pack.U16(0x0069), Pack.U16(0x4F),
                SessionID, AccountID, SessionID2,
                Pack.Zero(30), Pack.U8(sex),
                Pack.Bytes(a, b, c, d), port, serverName, users32, Pack.Zero(2)
            );
        }

        byte[] BuildCharacterList()
        {
            int block = _st.CharBlockSize > 0 ? _st.CharBlockSize : 116;

            var blocks = new List<byte[]>
            {
                BuildCharBlock(block, "Celtos", 0),
                BuildCharBlock(block, "Celtos Dev", 1),
            };
            int payload = 0; foreach (var b in blocks) payload += b.Length;

            var rc = _st.ReceivedCharacters?.ToUpperInvariant();
            var w = new Buf();

            if (rc == "082D")
            {
                w.Bytes(AccountID).U16(0x082D).U16((ushort)(payload + 29))
                 .Bytes(0x02, 0x00, 0x00, 0x00, 0x02).Bytes(new byte[20]);
            }
            else if (rc == "099D")
            {
                w.U16(0x099D).U16((ushort)(payload + 4));
            }
            else if (rc == "0B72")
            {
                w.U16(0x0B72).U16((ushort)(payload + 4));
            }
            else
            {
                w.Bytes(AccountID).U16(0x006B).U16((ushort)(payload + 7)).Bytes(12, 0xFF, 0xFF);
            }

            foreach (var b in blocks) w.Bytes(b);
            return w.ToArray();
        }

        byte[] BuildCharBlock(int block, string name, int slot)
        {
            // formato “universal” 116 bytes (a4 V9 v V2 v14 Z24 C6 v2)
            var w = new Buf();
            w.Bytes(CharID);
            for (int i = 0; i < 9; i++) w.U32(0);
            w.U16(0);
            w.Bytes(new byte[8]);   // V2 fake
            w.Bytes(new byte[28]);  // v14 fake
            w.Z(24, name);
            w.Bytes(new byte[6]);   // C6
            w.U16((ushort)slot);
            w.U16(6);               // hairColor
            return w.ToArray();
        }

        // ===== helpers =====
        static RoState GetState(HydraSession s)
        {
            if (s.Items.TryGetValue("__RO", out var o) && o is RoState st) return st;
            st = new RoState(); s.Items["__RO"] = st; return st;
        }

        static async Task Send(HydraSession s, byte[] data, CancellationToken ct)
        {
            await s.SendAsync(data, ct);
            s.Log($"=> {Hex(data)}");
        }

        static string Hex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (var x in b) sb.Append(x.ToString("X2"));
            return sb.ToString();
        }

        static (byte, byte, byte, byte, byte[]) BindIpPort(HydraSession s)
        {
            var ip = (s.BindIp ?? "127.0.0.1").Replace("localhost", "127.0.0.1");
            byte a = 127, b = 0, c = 0, d = 1;
            var parts = ip.Split('.');
            if (parts.Length == 4 &&
                byte.TryParse(parts[0], out a) &&
                byte.TryParse(parts[1], out b) &&
                byte.TryParse(parts[2], out c) &&
                byte.TryParse(parts[3], out d))
            { /* ok */ }
            return (a, b, c, d, Pack.U16((ushort)s.BindPort));
        }

        static byte[] Coord(int x, int y)
        {
            byte b0 = (byte)(x & 0xFF), b1 = (byte)(y & 0xFF), b2 = (byte)(((x >> 4) & 0xF0) | ((y >> 8) & 0x0F));
            return new[] { b0, b1, b2 };
        }

        static bool IsMasterLogin(string sw) =>
            sw is "0064" or "01DD" or "01FA" or "0277" or "027C" or "02B0" or "0825" or "0987" or "0A76" or "0AAC" or "0B04";

        static bool HeurServerChoice(byte[] msg) => msg.Length >= 11;

        bool IsMapLogin(string sw) =>
            sw is "0072" or "009B" or "00F5" or "0436" or "022D" ||
            string.Equals(_st.MapLogin?.ToUpperInvariant(), sw, StringComparison.Ordinal);

        static bool IsSync(string sw) =>
            sw is "007E" or "035F" or "0089" or "0116" or "00A7" or "0360";

        // lê length no [off+2..off+3] (se inválido, limita em 1024)
        static int ReadLen(byte[] buf, int off)
        {
            if (off + 4 <= buf.Length)
            {
                ushort len = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(buf, off + 2, 2));
                if (len >= 2) return len;
            }
            return Math.Min(1024, buf.Length - off);
        }

        static byte[] BuildGGResponse(ushort op, byte[] msg)
        {
            ushort len = msg.Length >= 4
                ? BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(msg, 2, 2))
                : (ushort)0;

            if (len > 0 && len <= msg.Length)
                return Pack.Join(Pack.U16(op), new ReadOnlySpan<byte>(msg, 2, len - 2).ToArray());

            return Pack.U16(op);
        }

        static int ExtractPairIndexFromTag(string? tag)
        {
            if (string.IsNullOrEmpty(tag)) return 1;
            char c = tag![tag.Length - 1];
            return (c >= '1' && c <= '9') ? (c - '0') : 1;
        }

        // ===== empacotadores =====
        static class Pack
        {
            public static byte[] U8(byte v) => new[] { v };
            public static byte[] U16(ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, v); return b; }
            public static byte[] U32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return b; }
            public static byte[] Bytes(params byte[] v) => v;
            public static byte[] Zero(int n) => new byte[n];
            public static byte[] Z(int size, string s)
            {
                var raw = Encoding.ASCII.GetBytes(s ?? "");
                if (size <= 0) { var r = new byte[raw.Length + 1]; Buffer.BlockCopy(raw, 0, r, 0, raw.Length); return r; }
                var b = new byte[Math.Max(size, raw.Length + 1)];
                Buffer.BlockCopy(raw, 0, b, 0, Math.Min(raw.Length, b.Length - 1)); return b;
            }
            public static byte[] ASCII(string s) => Encoding.ASCII.GetBytes(s ?? "");
            public static byte[] Join(params byte[][] parts)
            {
                int n = 0; foreach (var p in parts) n += p.Length;
                var b = new byte[n]; int o = 0;
                foreach (var p in parts) { Buffer.BlockCopy(p, 0, b, o, p.Length); o += p.Length; }
                return b;
            }
        }

        sealed class Buf
        {
            byte[] _b = new byte[256]; int _n;
            void Need(int k) { if (_n + k <= _b.Length) return; int s = _b.Length; while (s < _n + k) s *= 2; Array.Resize(ref _b, s); }
            public Buf Bytes(params byte[] v) { Need(v.Length); Buffer.BlockCopy(v, 0, _b, _n, v.Length); _n += v.Length; return this; }
            public Buf U8(byte v) { Need(1); _b[_n++] = v; return this; }
            public Buf U16(ushort v) { Need(2); BinaryPrimitives.WriteUInt16LittleEndian(_b.AsSpan(_n, 2), v); _n += 2; return this; }
            public Buf U32(uint v) { Need(4); BinaryPrimitives.WriteUInt32LittleEndian(_b.AsSpan(_n, 4), v); _n += 4; return this; }
            public Buf S32(int v) { Need(4); BinaryPrimitives.WriteInt32LittleEndian(_b.AsSpan(_n, 4), v); _n += 4; return this; }
            public Buf Z(int size, string s) { var z = Pack.Z(size, s); return Bytes(z); }
            public byte[] ToArray() { var r = new byte[_n]; Buffer.BlockCopy(_b, 0, r, 0, _n); return r; }
        }
    }
}
