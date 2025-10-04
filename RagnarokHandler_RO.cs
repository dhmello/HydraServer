using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HydraServer
{
    /// Emula o RagnarokServer.pm (Poseidon). Também publica respostas GG para o Query.
    internal sealed class RagnarokHandler_RO : IRagnarokHandler
    {
        // IDs/sessões fake
        static readonly byte[] AccountID = Pack.U32(2000001);
        static readonly byte[] CharID = Pack.U32(100001);
        static readonly byte[] SessionID = Pack.U32(3000000000u);
        static readonly byte[] SessionID2 = Pack.U32(0xFF);
        const int POS_X = 221, POS_Y = 128;

        readonly ServerType _st;
        public RagnarokHandler_RO(ServerType st) => _st = st;

        sealed class RoState
        {
            public bool ConnectedToMap;
            public int ChallengeNum;
            public bool SentCharList; // evita 099D duplicado
        }

        public void Tick(HydraSession session) { /* opcional */ }

        public async ValueTask<bool> OnClientDataAsync(HydraSession session, ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            var st = GetState(session);
            var buf = data.ToArray();
            for (int off = 0, n = buf.Length; off + 2 <= n;)
            {
                ushort op = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(buf, off, 2));
                int len = ReadLenOrRest(op, buf, off, n);
                if (session.Debug) session.Log($"[RO] RX op=0x{op:X4} len={len}");
                var msg = new byte[len]; Buffer.BlockCopy(buf, off, msg, 0, len); off += len;
                await Handle(session, st, op, msg, ct);
            }
            return true;
        }

        async Task Handle(HydraSession s, RoState st, ushort op, byte[] msg, CancellationToken ct)
        {
            string sw = op.ToString("X4");

            // GameGuard: publicar pro Query
            if (op is 0x09D0 or 0x0228 or 0x02A7)
            { PoseidonRegistry.ForPair(ExtractPairIndexFromTag(s.Tag)).SetResponse(BuildGGResponse(op, msg), s.Log); return; }

            // secure_login (01DB/0204 -> 01DC)
            if (sw is "01DB" or "0204") { await Send(s, Pack.Join(Pack.U16(0x01DC), Pack.U16(0x14), Pack.Zero(0x11)), ct); return; }

            // Token Agency (0ACF/0C26 -> 0AE3 len 0x2F exato)
            if (sw is "0ACF" or "0C26") { await Send(s, BuildTokenAgencyResponseExact47(), ct); return; }

            // GG challenge atalho (0258 -> 0259)
            if (sw == "0258")
            {
                byte grant = (byte)(st.ChallengeNum == 0 ? 0x01 : 0x02);
                await Send(s, new byte[] { 0x59, 0x02, grant }, ct); // 0259
                st.ChallengeNum++; if (s.Debug) s.Log($"[RO] 0258 -> 0259 grant={grant}");
                return;
            }

            // Token login kRO Zero: responde 0x0AC4
            if (sw == "0825") { if (s.Debug) s.Log("[RO] 0825 -> 0AC4"); await Send(s, BuildAccountServerInfoForced(s, "0AC4"), ct); return; }

            // master_login -> account_server_info
            if (IsMasterLogin(sw)) { await Send(s, BuildAccountServerInfo(s), ct); return; }

            // escolha do servidor
            if (sw is "0065" or "0275")
            {
                var rc = _st.ReceivedCharacters?.Trim().ToUpperInvariant();
                if (rc is "099D" or "0B72") { await SendPreCharHandshakeAsync(s, ct); return; } // accountID, 082D, 09A0; espera 09A1
                if (!st.SentCharList) { var pkt = BuildCharacterList(s); await Send(s, pkt, ct); st.SentCharList = true; if (s.Debug) s.Log("[RO] char list enviada (fluxo direto)"); }
                return;
            }

            // cliente pediu a lista (fluxo Poseidon 082D/09A0/09A1)
            if (sw == "09A1")
            {
                if (!st.SentCharList && string.Equals(_st.ReceivedCharacters?.Trim(), "099D", StringComparison.OrdinalIgnoreCase))
                {
                    var pkt = BuildCharacterList_099D_155(s);
                    await Send(s, pkt, ct); st.SentCharList = true;
                    if (s.Debug) s.Log("[RO] 099D enviado após 09A1");
                }
                return;
            }

            // escolha do personagem -> map select
            if (sw == "0066") { await Send(s, BuildCharMapSelect(s), ct); return; }

            // map_login
            if (IsMapLogin(sw)) { await HandleMapLogin(s, st, ct); return; }

            // sync
            if (IsSync(sw)) { await Send(s, Pack.Join(Pack.Bytes(0x7F, 0x00), Pack.U32((uint)Environment.TickCount)), ct); return; }

            // ping/quit
            if (sw == "00B2") { await Send(s, Pack.Join(Pack.U16(0x00B3), Pack.U16(1)), ct); return; }
            if (sw == "018A") { await Send(s, Pack.Join(Pack.U16(0x018B), Pack.U16(0)), ct); return; }

            if (s.Debug) s.Log($"[RO] ignorado 0x{sw} ({msg.Length} bytes)");
        }

        async Task SendPreCharHandshakeAsync(HydraSession s, CancellationToken ct)
        {
            // 1) accountID
            await Send(s, AccountID, ct);

            // 2) 082D len=0x001D, payload: 02 00 00 02 02 + 20 zeros
            var w = new Buf();
            w.U16(0x082D).U16(0x001D).U8(0x02).U8(0x00).U8(0x00).U8(0x02).U8(0x02).Bytes(new byte[0x14]);
            await Send(s, w.ToArray(), ct);

            // 3) 09A0 u32(1)
            await Send(s, Pack.Join(Pack.U16(0x09A0), Pack.U32(1)), ct);

            if (s.Debug) s.Log("[RO] handshake pré-lista enviado; aguardando 09A1");
        }

        async Task HandleMapLogin(HydraSession s, RoState st, CancellationToken ct)
        {
            await Send(s, Pack.Join(Pack.U16(0x0283), AccountID), ct);
            if ((_st.Name?.StartsWith("kRO", StringComparison.OrdinalIgnoreCase) ?? false))
                await Send(s, Pack.Join(Pack.U16(0x0ADE), Pack.U32(0)), ct);

            byte[] loaded;
            var ml = _st.MapLoaded?.ToUpperInvariant();
            if (ml == "0A18") loaded = Pack.Join(Pack.U16(0x0A18), Pack.U32((uint)Environment.TickCount), Coord(POS_X, POS_Y), Pack.Bytes(0, 0), Pack.U16(0), Pack.U8(1));
            else if (ml == "02EB") loaded = Pack.Join(Pack.U16(0x02EB), Pack.U32((uint)Environment.TickCount), Coord(POS_X, POS_Y), Pack.Bytes(0, 0));
            else loaded = Pack.Join(Pack.U16(0x0073), Pack.U32((uint)Environment.TickCount), Coord(POS_X, POS_Y), Pack.Bytes(0, 0));
            await Send(s, loaded, ct);

            await Send(s, Pack.Join(Pack.U16(0x013A), Pack.U16(1)), ct);                 // attack_range
            await Send(s, Pack.Join(Pack.U16(0x00BD), Pack.Zero(2 + 12 + 14 * 2)), ct);  // stats dummy
            if (string.Equals(_st.ConfirmLoad, "0B1B", StringComparison.OrdinalIgnoreCase))
                await Send(s, Pack.U16(0x0B1B), ct);

            st.ConnectedToMap = true;
        }

        // ===== respostas =====

        static byte[] BuildTokenAgencyResponseExact47()
        {
            var buf = new byte[0x2F];
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), 0x0AE3);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), 0x002F);
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4, 4), 0);
            Encoding.ASCII.GetBytes("S1000").CopyTo(buf.AsSpan(8));                 // 8..27 total 20 (resto zero)
            Encoding.ASCII.GetBytes("OpenkoreClientToken").CopyTo(buf.AsSpan(28));  // 28..46 (19 bytes)
            return buf;
        }

        byte[] BuildCharMapSelect(HydraSession s)
        {
            var (a, b, c, d, port) = BindIpPort(s);
            string map = "brasilis.gat";
            return string.Equals(_st.ReceivedCharacterIdAndMap, "0AC5", StringComparison.OrdinalIgnoreCase)
                ? Pack.Join(Pack.U16(0x0AC5), CharID, Pack.Z(16, map), Pack.Bytes(a, b, c, d), port, Pack.Zero(128))
                : Pack.Join(Pack.U16(0x0071), CharID, Pack.Z(16, map), Pack.Bytes(a, b, c, d), port);
        }

        byte[] BuildAccountServerInfoForced(HydraSession s, string forceOpcode)
        {
            _ = forceOpcode ?? "";
            var (a, b, c, d, port) = BindIpPort(s);
            byte sex = 1;
            var serverName = Pack.Z(20, "openkore.com.br");
            var users = Pack.U32((uint)Math.Max(0, s.ServerClientCount - 1));
            string asi = forceOpcode.ToUpperInvariant();

            if (asi is "0AC9" or "0AAC")
                return Pack.Join(Pack.U16(0x0AC9), Pack.U16(0xCF), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex), Pack.Zero(6),
                                 serverName, users, Pack.Bytes(0x80, 0x32), Pack.ASCII($"{s.BindIp}:{s.BindPort}"), Pack.Zero(114));

            if (asi is "0B07" or "0B04")
                return Pack.Join(Pack.U16(0x0B07), Pack.U16(0xCF), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex),
                                 Pack.Bytes(a, b, c, d), port, serverName, users, Pack.Zero(130));

            if (asi == "0B60")
                return Pack.Join(Pack.U16(0x0B60), Pack.U16(0xE4), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex), Pack.Zero(0x11),
                                 Pack.Bytes(a, b, c, d), port, serverName, Pack.Zero(2),
                                 Pack.U16((ushort)Math.Min(ushort.MaxValue, Math.Max(0, s.ServerClientCount - 1))),
                                 Pack.U16(0x6985), Pack.Zero(128 + 4));

            if (asi == "0AC4")
                return Pack.Join(Pack.U16(0x0AC4), Pack.U16(0xE0), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex), Pack.Zero(0x11),
                                 Pack.Bytes(a, b, c, d), port, serverName, users, Pack.Zero(130));

            if (asi == "0276")
                return Pack.Join(Pack.U16(0x0276), Pack.U16(0x63), SessionID, AccountID, SessionID2, Pack.Zero(30), Pack.U8(sex), Pack.Zero(4),
                                 Pack.Bytes(a, b, c, d), port, serverName, Pack.Zero(2), users, Pack.Zero(6));

            // fallback 0069
            return Pack.Join(Pack.U16(0x0069), Pack.U16(0x4F), SessionID, AccountID, SessionID2, Pack.Zero(30), Pack.U8(sex),
                             Pack.Bytes(a, b, c, d), port, serverName, users, Pack.Zero(2));
        }

        byte[] BuildAccountServerInfo(HydraSession s)
        {
            string asi = _st.AccountServerInfo?.ToUpperInvariant() ?? "";
            var (a, b, c, d, port) = BindIpPort(s);
            byte sex = 1;
            var serverName = Pack.Z(20, "openkore.com.br");
            var users = Pack.U32((uint)Math.Max(0, s.ServerClientCount - 1));

            if (asi is "0AC9" or "0AAC")
                return Pack.Join(Pack.U16(0x0AC9), Pack.U16(0xCF), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex), Pack.Zero(6),
                                 serverName, users, Pack.Bytes(0x80, 0x32), Pack.ASCII($"{s.BindIp}:{s.BindPort}"), Pack.Zero(114));

            if (asi is "0B07" or "0B04")
                return Pack.Join(Pack.U16(0x0B07), Pack.U16(0xCF), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex),
                                 Pack.Bytes(a, b, c, d), port, serverName, users, Pack.Zero(130));

            if (asi == "0B60")
                return Pack.Join(Pack.U16(0x0B60), Pack.U16(0xE4), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex), Pack.Zero(0x11),
                                 Pack.Bytes(a, b, c, d), port, serverName, Pack.Zero(2),
                                 Pack.U16((ushort)Math.Min(ushort.MaxValue, Math.Max(0, s.ServerClientCount - 1))),
                                 Pack.U16(0x6985), Pack.Zero(128 + 4));

            if (asi == "0AC4")
                return Pack.Join(Pack.U16(0x0AC4), Pack.U16(0xE0), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex), Pack.Zero(0x11),
                                 Pack.Bytes(a, b, c, d), port, serverName, users, Pack.Zero(130));

            if (asi == "0276")
                return Pack.Join(Pack.U16(0x0276), Pack.U16(0x63), SessionID, AccountID, SessionID2, Pack.Zero(30), Pack.U8(sex), Pack.Zero(4),
                                 Pack.Bytes(a, b, c, d), port, serverName, Pack.Zero(2), users, Pack.Zero(6));

            // default 0069
            return Pack.Join(Pack.U16(0x0069), Pack.U16(0x4F), SessionID, AccountID, SessionID2, Pack.Zero(30), Pack.U8(sex),
                             Pack.Bytes(a, b, c, d), port, serverName, users, Pack.Zero(2));
        }

        // fallback para fluxos sem 082D/09A1 (ex.: 006B)
        byte[] BuildCharacterList(HydraSession s)
        {
            var c1 = BuildCharBlock116("Celtos", 0);
            var c2 = BuildCharBlock116("Celtos Dev", 1);
            int payload = c1.Length + c2.Length;

            var w = new Buf();
            w.Bytes(AccountID).U16(0x006B).U16((ushort)(payload + 7))
             .U8(12).U8(0xFF).U8(0xFF).Bytes(c1).Bytes(c2);
            if (s.Debug) s.Log($"[RO] ENVIANDO fallback 006B: payload={payload}");
            return w.ToArray();
        }

        // 099D (155) – sem count; len = 4 + 155*N
        byte[] BuildCharacterList_099D_155(HydraSession s)
        {
            var b1 = BuildCharBlock155("Celtos", 0);
            var b2 = BuildCharBlock155("Celtos Dev", 1);
            ushort lenField = (ushort)(b1.Length + b2.Length + 4); // 314
            var w = new Buf(); w.U16(0x099D).U16(lenField).Bytes(b1).Bytes(b2);
            if (s.Debug) s.Log($"[RO] 099D enviado (len={lenField}, chars=2)");
            return w.ToArray();
        }

        byte[] BuildCharBlock(int blockSize, string name, int slot)
        {
            var buf = new byte[blockSize];
            if (blockSize >= 4) Buffer.BlockCopy(CharID, 0, buf, 0, 4);
            const int nameFieldLen = 24;
            int nameOff = Math.Max(0, blockSize - nameFieldLen);
            var nameBytes = Encoding.ASCII.GetBytes(name ?? "");
            int copyLen = Math.Min(nameBytes.Length, Math.Max(0, nameFieldLen - 1));
            if (blockSize >= nameFieldLen) { Buffer.BlockCopy(nameBytes, 0, buf, nameOff, copyLen); buf[nameOff + copyLen] = 0; }
            int slotOff = nameOff - 2;
            if (slotOff >= 0) { ushort us = (ushort)slot; buf[slotOff] = (byte)(us & 0xFF); buf[slotOff + 1] = (byte)(us >> 8); }
            return buf;
        }

        byte[] BuildCharBlock155(string name, int slot)
        {
            const ushort hp = 5000, maxHp = 5000, sp = 100, maxSp = 100, level = 1, jobLevel = 1, walk = 150, jobId = 0;
            const byte hair = 5, sex = 1;
            var w = new Buf();
            w.Bytes(CharID)      // a4
             .Z(8, "").U32(0).Z(8, "")                 // Z8, V, Z8
             .U32(0).U32(0).U32(0).U32(0).U32(0).U32(0) // V6
             .U16(jobLevel)                              // v
             .U32(hp).U32(maxHp)                         // V2
             .U16(sp).U16(maxSp).U16(walk).U16(jobId)    // v4
             .U32(0)                                     // V
             .U16(level).U16(0).U16(0).U16(0).U16(0).U16(0).U16(0).U16(0).U16(hair) // v9
             .Z(24, name)                                // Z24
             .U8(1).U8(1).U8(1).U8(1).U8(1).U8(1).U8(0).U8(0) // C8
             .U16((ushort)slot)                          // v slot
             .Z(16, "")                                  // Z16
             .U32(0).U32(0).U32(0).U32(6)                // V4
             .U8(sex);                                   // C
            var bytes = w.ToArray();
            if (bytes.Length != 155) { var fix = new byte[155]; Buffer.BlockCopy(bytes, 0, fix, 0, Math.Min(bytes.Length, 155)); return fix; }
            return bytes;
        }

        byte[] BuildCharBlock116(string name, int slot)
        {
            var w = new Buf();
            w.Bytes(CharID)                     // a4
             .U32(1).U32(1).U32(1).U32(0).U32(0).U32(0).U32(0).U32(0).U32(0) // V9
             .U16(50)                           // jobLevel
             .U32(10000).U32(10000)             // hp/maxHp
             .U16(10000).U16(10000).U16(0x01BD).U16(0).U16(5).U16(0).U16(99).U16(0).U16(0).U16(0).U16(0).U16(0) // v14
             .Z(24, name)                        // Z24
             .U8(1).U8(1).U8(1).U8(1).U8(1).U8(1) // C6
             .U16((ushort)slot).U16(6);          // v2
            var bytes = w.ToArray();
            if (bytes.Length != 116) { var fix = new byte[116]; Buffer.BlockCopy(bytes, 0, fix, 0, Math.Min(bytes.Length, 116)); return fix; }
            return bytes;
        }

        // ===== helpers =====
        static RoState GetState(HydraSession s)
        {
            if (s.Items.TryGetValue("__RO", out var o) && o is RoState st) return st;
            s.Items["__RO"] = st = new RoState(); return st;
        }

        static async Task Send(HydraSession s, byte[] data, CancellationToken ct)
        { if (s.Debug) s.Log($"=> {Hex(data)}"); await s.SendAsync(data, ct); }

        static string Hex(byte[] b) { var sb = new StringBuilder(b.Length * 2); foreach (var x in b) sb.Append(x.ToString("X2")); return sb.ToString(); }

        static (byte, byte, byte, byte, byte[]) BindIpPort(HydraSession s)
        {
            var ip = (s.BindIp ?? "127.0.0.1").Replace("localhost", "127.0.0.1");
            byte a = 127, b = 0, c = 0, d = 1; var parts = ip.Split('.');
            if (parts.Length == 4 && byte.TryParse(parts[0], out a) && byte.TryParse(parts[1], out b) && byte.TryParse(parts[2], out c) && byte.TryParse(parts[3], out d)) { }
            return (a, b, c, d, Pack.U16((ushort)s.BindPort));
        }

        static byte[] Coord(int x, int y)
        { byte b0 = (byte)(x & 0xFF), b1 = (byte)(y & 0xFF), b2 = (byte)(((x >> 4) & 0xF0) | ((y >> 8) & 0x0F)); return new[] { b0, b1, b2 }; }

        static bool IsMasterLogin(string sw) =>
            sw is "0064" or "01DD" or "01FA" or "0277" or "027C" or "02B0" or "0987" or "0A76" or "0AAC" or "0B04";

        bool IsMapLogin(string sw) =>
            sw is "0072" or "009B" or "00F5" or "0436" or "022D" or "0B1C" ||
            string.Equals(_st.MapLogin?.ToUpperInvariant(), sw, StringComparison.Ordinal);

        static bool IsSync(string sw) => sw is "007E" or "035F" or "0089" or "0116" or "00A7" or "0360";

        static int ReadLenOrRest(ushort op, byte[] buf, int off, int nTot)
        {
            if (off + 4 <= nTot)
            {
                ushort len = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(buf, off + 2, 2));
                if (len >= 2 && off + len <= nTot) return len;
            }
            return nTot - off; // op sem length (ex.: 0x0C26)
        }

        static byte[] BuildGGResponse(ushort op, byte[] msg)
        {
            ushort len = msg.Length >= 4 ? BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(msg, 2, 2)) : (ushort)0;
            return (len > 0 && len <= msg.Length) ? Pack.Join(Pack.U16(op), new ReadOnlySpan<byte>(msg, 2, len - 2).ToArray())
                                                  : Pack.U16(op);
        }

        static int ExtractPairIndexFromTag(string? tag)
        { if (string.IsNullOrEmpty(tag)) return 1; char c = tag![tag.Length - 1]; return (c >= '1' && c <= '9') ? (c - '0') : 1; }

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
            { int n = 0; foreach (var p in parts) n += p.Length; var b = new byte[n]; int o = 0; foreach (var p in parts) { Buffer.BlockCopy(p, 0, b, o, p.Length); o += p.Length; } return b; }
        }

        sealed class Buf
        {
            byte[] _b = new byte[256]; int _n;
            void Need(int k) { if (_n + k <= _b.Length) return; int s = _b.Length; while (s < _n + k) s *= 2; Array.Resize(ref _b, s); }
            public Buf Bytes(params byte[] v) { Need(v.Length); Buffer.BlockCopy(v, 0, _b, _n, v.Length); _n += v.Length; return this; }
            public Buf U8(byte v) { Need(1); _b[_n++] = v; return this; }
            public Buf U16(ushort v) { Need(2); BinaryPrimitives.WriteUInt16LittleEndian(_b.AsSpan(_n, 2), v); _n += 2; return this; }
            public Buf U32(uint v) { Need(4); BinaryPrimitives.WriteUInt32LittleEndian(_b.AsSpan(_n, 4), v); _n += 4; return this; }
            public Buf Z(int size, string s) { var z = Pack.Z(size, s); return Bytes(z); }
            public byte[] ToArray() { var r = new byte[_n]; Buffer.BlockCopy(_b, 0, r, 0, _n); return r; }
        }
    }
}
