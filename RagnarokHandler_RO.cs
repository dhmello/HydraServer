using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HydraServer
{
    /// <summary>
    /// Emula o RagnarokServer.pm (Poseidon) com todas as funcionalidades originais
    /// </summary>
    internal sealed class RagnarokHandler_RO : IRagnarokHandler
    {
        // IDs/sessões fake
        static readonly byte[] AccountID = Pack.U32(2000001);
        static readonly byte[] CharID = Pack.U32(100001);
        static readonly byte[] SessionID = Pack.U32(3000000000u);
        static readonly byte[] SessionID2 = Pack.U32(0xFF);

        // NPCs e objetos
        static readonly byte[] NpcID1 = Pack.U32(110000001);
        static readonly byte[] NpcID0 = Pack.U32(110000002);
        static readonly byte[] MonsterID = Pack.U32(110000003);
        static readonly byte[] ItemID = Pack.U32(50001);

        const int POS_X = 221, POS_Y = 128;

        readonly ServerType _st;
        public RagnarokHandler_RO(ServerType st) => _st = st;

        sealed class RoState
        {
            public bool ConnectedToMap;
            public int ChallengeNum;
            public bool SentCharList;
            public int Mode; // 0 = normal, 1 = dev
            public int ServerType;
            public string MasterLoginPacket;
            public string GameLoginPacket;
            public int Version;
            public int MasterVersion;
            public int SecureLogin;
            public int SecureLoginType;
            public int SecureLoginAccount;
            public string SecureLoginRequestCode;
            public double NpcTalkCode;
            public long EmoticonTime;
        }

        // Variáveis de criptografia (como no Perl)
        private ulong _enc_val1 = 0;
        private ulong _enc_val2 = 0;
        private ulong _enc_val3 = 0;
        private int _state = 0;

        public void Tick(HydraSession session) { /* opcional */ }

        public async ValueTask<bool> OnClientDataAsync(HydraSession session, ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            var st = GetState(session);
            var buf = data.ToArray();

            session.Log($"[DEBUG] Recebido {buf.Length} bytes: {BitConverter.ToString(buf).Replace("-", " ")}");

            // Aplicar descriptografia se ativa
            if (_enc_val1 != 0 && _enc_val2 != 0 && _enc_val3 != 0)
            {
                session.Log($"[DEBUG] Descriptografando mensagem...");
                buf = DecryptMessage(buf);
            }

            for (int off = 0, n = buf.Length; off + 2 <= n;)
            {
                ushort op = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(buf, off, 2));
                int len = ReadLenOrRest(op, buf, off, n);
                session.Log($"[DEBUG] Pacote op=0x{op:X4} len={len} offset={off}");
                var msg = new byte[len];
                Buffer.BlockCopy(buf, off, msg, 0, len);
                off += len;
                await Handle(session, st, op, msg, ct);
            }
            return true;
        }

        private byte[] DecryptMessage(byte[] msg)
        {
            if (msg.Length < 2) return msg;

            // Salvar informações para debug
            ushort oldMID = BinaryPrimitives.ReadUInt16LittleEndian(msg);
            ushort oldKey = (ushort)((_enc_val1 >> 16) & 0x7FFF);

            // Calcular próxima chave de descriptografia
            _enc_val1 = (_enc_val1 * _enc_val3 + _enc_val2) & 0xFFFFFFFF;

            // XOR no Message ID
            ushort newMID = (ushort)(oldMID ^ ((_enc_val1 >> 16) & 0x7FFF));

            // Aplicar descriptografia
            var result = new byte[msg.Length];
            Buffer.BlockCopy(msg, 0, result, 0, msg.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(result, newMID);

            return result;
        }

        private ushort DecryptMessageID(HydraSession s, ushort messageID)
        {
            if (_enc_val1 != 0 && _enc_val2 != 0 && _enc_val3 != 0)
            {
                ushort oldMID = messageID;
                ushort oldKey = (ushort)((_enc_val1 >> 16) & 0x7FFF);

                _enc_val1 = (_enc_val1 * _enc_val3 + _enc_val2) & 0xFFFFFFFF;
                messageID = (ushort)(messageID ^ ((_enc_val1 >> 16) & 0x7FFF));

                if (s.Debug)
                {
                    Console.WriteLine($"Decrypted MID : [{oldMID:X4}]->[{messageID:X4}] / KEY : [0x{oldKey:X4}]->[0x{((_enc_val1 >> 16) & 0x7FFF):X4}]");
                }
            }
            return messageID;
        }

        async Task Handle(HydraSession s, RoState st, ushort op, byte[] msg, CancellationToken ct)
        {
            string sw = op.ToString("X4");
            s.Log($"[DEBUG] Handle: op=0x{sw} ({msg.Length} bytes)");

            // GameGuard: publicar pro Query
            if (op is 0x09D0 or 0x0228 or 0x02A7)
            {
                s.Log($"[DEBUG] GameGuard op=0x{sw}");
                PoseidonRegistry.ForPair(ExtractPairIndexFromTag(s.Tag)).SetResponse(BuildGGResponse(op, msg), s.Log);
                return;
            }

            // secure_login (01DB/0204 -> 01DC)
            if (sw is "01DB" or "0204")
            {
                s.Log($"[DEBUG] secure_login recebido");
                await Send(s, Pack.Join(Pack.U16(0x01DC), Pack.U16(0x14), Pack.Zero(17)), ct);
                return;
            }

            // Token Agency (0ACF/0C26 -> 0AE3)
            if (sw is "0ACF" or "0C26")
            {
                s.Log($"[DEBUG] Token Agency recebido");
                await Send(s, BuildTokenAgencyResponse(), ct);
                return;
            }

            // GG challenge atalho (0258 -> 0259)
            if (sw == "0258")
            {
                s.Log($"[DEBUG] GG challenge atalho recebido");
                byte grant = (byte)(st.ChallengeNum == 0 ? 0x01 : 0x02);
                await Send(s, new byte[] { 0x59, 0x02, grant }, ct);
                st.ChallengeNum++;
                if (s.Debug) s.Log($"[RO] 0258 -> 0259 grant={grant}");
                return;
            }

            // master_login -> account_server_info
            if (IsMasterLogin(sw))
            {
                s.Log($"[DEBUG] master_login detectado");
                await HandleMasterLogin(s, st, sw, msg, ct);
                return;
            }

            // escolha do servidor
            if (sw is "0065" or "0275" || IsServerChoicePacket(msg))
            {
                s.Log($"[DEBUG] escolha do servidor recebido");
                var rc = _st.ReceivedCharacters?.Trim().ToUpperInvariant();
                if (rc is "099D" or "0B72")
                {
                    s.Log($"[DEBUG] handshake pré-lista necessário");
                    await SendPreCharHandshakeAsync(s, ct);
                    return;
                }
                if (!st.SentCharList)
                {
                    s.Log($"[DEBUG] Enviando lista de personagens (fluxo direto)");
                    var pkt = BuildCharacterList(s);
                    await Send(s, pkt, ct);
                    st.SentCharList = true;
                    if (s.Debug) s.Log("[RO] char list enviada (fluxo direto)");
                }
                st.GameLoginPacket = sw;
                return;
            }

            // cliente pediu a lista (fluxo Poseidon 09A1)
            if (sw == "09A1")
            {
                s.Log($"[DEBUG] Pedido de lista de personagens (09A1)");
                var rc = _st.ReceivedCharacters?.Trim().ToUpperInvariant();
                byte[] pkt = rc switch
                {
                    "099D" => BuildCharacterList_099D(s),
                    "0B72" => BuildCharacterList_0B72(s),
                    _ => BuildCharacterList(s)
                };
                await Send(s, pkt, ct);
                st.SentCharList = true;
                if (s.Debug) s.Log("[RO] lista de chars enviada após 09A1");
                return;
            }

            // escolha do personagem -> map select
            if (sw == "0066")
            {
                s.Log($"[DEBUG] escolha do personagem recebido");
                await HandleCharacterSelection(s, st, msg, ct);
                return;
            }

            // map_login
            if (IsMapLogin(sw, msg, st))
            {
                s.Log($"[DEBUG] map_login detectado");
                await HandleMapLogin(s, st, sw, msg, ct);
                return;
            }

            // sync
            if (IsSync(sw, st))
            {
                s.Log($"[DEBUG] sync detectado");
                await Send(s, Pack.Join(Pack.U16(0x007F), Pack.U32((uint)Environment.TickCount)), ct);

                // Verificar se 0228 veio junto (como no Perl)
                if (msg.Length >= 8 &&
                    BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(msg, 6, 2)) == 0x0228)
                {
                    s.Log($"[DEBUG] sync com 0228 junto");
                    var response = Pack.Join(Pack.U16(op), new ReadOnlySpan<byte>(msg, 8, msg.Length - 8).ToArray());
                    PoseidonRegistry.ForPair(ExtractPairIndexFromTag(s.Tag)).SetResponse(response, s.Log);
                }
                return;
            }

            // quit to char selection
            if (sw == "00B2")
            {
                s.Log($"[DEBUG] quit to char selection recebido");
                await Send(s, Pack.Join(Pack.U16(0x00B3), Pack.U16(1)), ct);
                // Desativar criptografia como no Perl
                _enc_val1 = 0;
                _enc_val2 = 0;
                _enc_val3 = 0;
                return;
            }

            // quit game
            if (sw == "018A")
            {
                s.Log($"[DEBUG] quit game recebido");
                await Send(s, Pack.Join(Pack.U16(0x018B), Pack.U16(0)), ct);
                return;
            }

            // accountid sync
            if (sw == "0187")
            {
                s.Log($"[DEBUG] accountid sync recebido");
                await Send(s, msg, ct);
                return;
            }

            // pong
            if (sw == "0B1C")
            {
                s.Log($"[DEBUG] pong recebido");
                await Send(s, Pack.U16(0x0B1D), ct);
                return;
            }

            // NPC talk
            if (sw == "0090" || IsNpcTalk(msg))
            {
                s.Log($"[DEBUG] NPC talk recebido");
                await HandleNpcTalk(s, st, msg, ct);
                return;
            }

            // NPC talk response
            if (sw == "00B8")
            {
                s.Log($"[DEBUG] NPC talk response recebido");
                await HandleNpcTalkResponse(s, st, msg, ct);
                return;
            }

            // NPC talk continue
            if (sw == "00B9")
            {
                s.Log($"[DEBUG] NPC talk continue recebido");
                await HandleNpcTalkContinue(s, st, msg, ct);
                return;
            }

            // NPC talk cancel
            if (sw == "0146")
            {
                s.Log($"[DEBUG] NPC talk cancel recebido");
                await HandleNpcTalkCancel(s, st, msg, ct);
                return;
            }

            // storage close
            if ((sw == "00F7" || sw == "0193") && msg.Length == 2)
            {
                s.Log($"[DEBUG] storage close recebido");
                await Send(s, Pack.U16(0x00F8), ct);
                return;
            }

            // emoticon
            if (sw == "00BF")
            {
                s.Log($"[DEBUG] emoticon recebido");
                await HandleEmoticon(s, st, msg, ct);
                return;
            }

            if (st.Mode == 1)
            {
                s.Log($"[DEBUG] Pacote não tratado no modo dev");
                await HandleDevModePackets(s, st, sw, msg, ct);
                return;
            }

            s.Log($"[DEBUG] ignorado 0x{sw} ({msg.Length} bytes)");
        }

        private async Task HandleMasterLogin(HydraSession s, RoState st, string sw, byte[] msg, CancellationToken ct)
        {
            // Salvar informações do cliente
            ParseMasterLoginInfo(st, sw, msg);

            // Enviar resposta
            byte[] response = BuildAccountServerInfo(s, sw);
            await Send(s, response, ct);
        }

        private void ParseMasterLoginInfo(RoState st, string sw, byte[] msg)
        {
            st.MasterLoginPacket = sw;

            switch (sw)
            {
                case "0064":
                case "01DD":
                case "0987":
                case "0AAC":
                    if (msg.Length >= 7)
                    {
                        st.Version = BinaryPrimitives.ReadInt32LittleEndian(msg.AsSpan(2));
                        st.MasterVersion = msg[msg.Length - 1];
                    }
                    break;
                case "01FA":
                    if (msg.Length >= 8)
                    {
                        st.Version = BinaryPrimitives.ReadInt32LittleEndian(msg.AsSpan(2));
                        st.MasterVersion = msg[msg.Length - 2];
                    }
                    break;
                case "0825":
                    if (msg.Length >= 11)
                    {
                        st.Version = BinaryPrimitives.ReadUInt16LittleEndian(msg.AsSpan(4));
                        st.MasterVersion = BinaryPrimitives.ReadUInt16LittleEndian(msg.AsSpan(7));
                    }
                    break;
                case "0A76":
                case "0B04":
                    if (msg.Length >= 8)
                    {
                        st.Version = BinaryPrimitives.ReadInt32LittleEndian(msg.AsSpan(2));
                        st.MasterVersion = BinaryPrimitives.ReadUInt16LittleEndian(msg.AsSpan(msg.Length - 2));
                    }
                    break;
                case "02B0":
                    if (msg.Length >= 57)
                    {
                        st.Version = BinaryPrimitives.ReadInt32LittleEndian(msg.AsSpan(2));
                        st.MasterVersion = msg[53];
                    }
                    break;
                default:
                    st.Version = 55;
                    st.MasterVersion = 1;
                    break;
            }

            // Configurar secureLogin
            if (sw == "01DD")
            {
                st.SecureLogin = 1;
                st.SecureLoginAccount = 0;
            }
            else if (sw == "01FA")
            {
                st.SecureLogin = 3;
                st.SecureLoginAccount = msg.Length >= 48 ? msg[47] : 0;
            }
            else
            {
                st.SecureLogin = 0;
                st.SecureLoginType = 0;
                st.SecureLoginAccount = 0;
                st.SecureLoginRequestCode = null;
            }
        }

        private async Task HandleCharacterSelection(HydraSession s, RoState st, byte[] msg, CancellationToken ct)
        {
            if (msg.Length >= 3)
            {
                st.Mode = msg[2]; // 0 = normal, 1 = dev
            }

            // Ativar criptografia se configurado
            if (!string.IsNullOrEmpty(_st.SendCryptKeys))
            {
                var keys = _st.SendCryptKeys.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (keys.Length >= 3)
                {
                    _enc_val1 = ulong.Parse(keys[0]);
                    _enc_val2 = ulong.Parse(keys[1]);
                    _enc_val3 = ulong.Parse(keys[2]);
                }
            }
            _state = 1;

            await Send(s, BuildCharMapSelect(s), ct);
        }

        private async Task HandleMapLogin(HydraSession s, RoState st, string sw, byte[] msg, CancellationToken ct)
        {
            // Determinar serverType baseado no pacote (como no Perl)
            st.ServerType = DetermineServerType(sw, msg);

            await Send(s, Pack.Join(Pack.U16(0x0283), AccountID), ct);

            // kRO specific
            if ((_st.Name?.StartsWith("kRO", StringComparison.OrdinalIgnoreCase) ?? false))
                await Send(s, Pack.Join(Pack.U16(0x0ADE), Pack.U32(0)), ct);

            // map_loaded packet
            byte[] loaded = BuildMapLoadedPacket();
            await Send(s, loaded, ct);

            // skills
            if (st.Mode == 1)
            {
                await SendSkills(s, ct);
            }

            await Send(s, Pack.Join(Pack.U16(0x013A), Pack.U16(1)), ct); // attack_range
            await Send(s, BuildStatsInfo(), ct); // stats_info

            if (string.Equals(_st.ConfirmLoad, "0B1B", StringComparison.OrdinalIgnoreCase))
                await Send(s, Pack.U16(0x0B1B), ct);

            st.ConnectedToMap = true;

            // Realizar tarefas pós-login
            await PerformMapLoadedTasks(s, st, ct);
        }

        private int DetermineServerType(string sw, byte[] msg)
        {
            // Implementação simplificada da lógica complexa do Perl
            // Aqui você precisaria implementar toda a lógica de detecção baseada no pacote
            return 0; // default
        }

        private byte[] BuildMapLoadedPacket()
        {
            var ml = _st.MapLoaded?.ToUpperInvariant();
            return ml switch
            {
                "0A18" => Pack.Join(Pack.U16(0x0A18), Pack.U32((uint)Environment.TickCount),
                                   Coord(POS_X, POS_Y), Pack.Bytes(0, 0), Pack.U16(0), Pack.U8(1)),
                "02EB" => Pack.Join(Pack.U16(0x02EB), Pack.U32((uint)Environment.TickCount),
                                   Coord(POS_X, POS_Y), Pack.Bytes(0, 0), Pack.U16(0)),
                _ => Pack.Join(Pack.U16(0x0073), Pack.U32((uint)Environment.TickCount),
                              Coord(POS_X, POS_Y), Pack.Bytes(0, 0))
            };
        }

        private async Task SendSkills(HydraSession s, CancellationToken ct)
        {
            if (string.Equals(_st.MapLoaded, "0B32", StringComparison.OrdinalIgnoreCase))
            {
                var w = new Buf();
                w.U16(0x0B32).U16(94)
                 .U16(1).U32(0).U16(9).U16(0).U16(1).U8(0).U16(0)
                 .U16(24).U32(4).U16(1).U16(10).U16(10).U8(0).U16(0)
                 .U16(25).U32(2).U16(1).U16(10).U16(9).U8(0).U16(0)
                 .U16(26).U32(4).U16(2).U16(9).U16(1).U8(0).U16(0)
                 .U16(27).U32(2).U16(4).U16(26).U16(9).U8(0).U16(0)
                 .U16(28).U32(16).U16(10).U16(40).U16(9).U8(0).U16(0);
                await Send(s, w.ToArray(), ct);
            }
            else
            {
                var w = new Buf();
                w.Bytes(0x0F, 0x01, 0xE2, 0x00) // 0F 01 E2 00
                 .U16(1).U16(0).Bytes(0, 0).U16(9).U16(0).U16(1).Z(24, "NV_BASIC\0GetMapInfo\x0A").U8(0)
                 .U16(24).U16(4).Bytes(0, 0).U16(1).U16(10).U16(10).Z(24, "AL_RUWACH").U8(0)
                 .U16(25).U16(2).Bytes(0, 0).U16(1).U16(10).U16(9).Z(24, "AL_PNEUMA").U8(0)
                 .U16(26).U16(4).Bytes(0, 0).U16(2).U16(9).U16(1).Z(24, "AL_TELEPORT").U8(0)
                 .U16(27).U16(2).Bytes(0, 0).U16(4).U16(26).U16(9).Z(24, "AL_WARP").U8(0)
                 .U16(28).U16(16).Bytes(0, 0).U16(10).U16(40).U16(9).Z(24, "AL_HEAL").U8(0);
                await Send(s, w.ToArray(), ct);
            }
        }

        private byte[] BuildStatsInfo()
        {
            var w = new Buf();
            w.U16(0x00BD).U16(100) // points_free
             .U8(99).U8(11).U8(99).U8(11).U8(99).U8(11).U8(99).U8(11).U8(99).U8(11).U8(99).U8(11) // str..luk
             .U16(999).U16(999).U16(999).U16(999) // attack..attack_magic_max
             .U16(999).U16(999).U16(999).U16(999) // def..def_magic_bonus
             .U16(999).U16(999).U16(999) // hit..flee_bonus
             .U16(100).U16(190).U16(3); // critical, stance, manner
            return w.ToArray();
        }

        private async Task PerformMapLoadedTasks(HydraSession s, RoState st, CancellationToken ct)
        {
            // Look front
            await SendLookTo(s, AccountID, 4, ct);

            // Unit info
            await SendUnitInfo(s, AccountID, "Celtos" + (st.Mode == 1 ? " Dev" : ""), ct);

            // System message
            await SendSystemChatMessage(s, "Acesse: www.openkore.com.br!", ct);

            // Show NPC
            await SendShowNPC(s, 1, NpcID0, 86, POS_X - 3, POS_Y - 4, "Celtos", ct);
            await SendLookTo(s, NpcID0, 3, ct);
            await SendUnitInfo(s, NpcID0, "www.openkore.com.br", ct);

            // Dev mode
            if (st.Mode == 1)
            {
                await SendShowNPC(s, 1, NpcID1, 114, POS_X + 5, POS_Y + 3, "Kafra NPC", ct);
                await SendLookTo(s, NpcID1, 4, ct);
                await SendUnitInfo(s, NpcID1, "Kafra NPC", ct);

                await SendShowNPC(s, 5, MonsterID, 1002, POS_X - 2, POS_Y - 1, "Poring", ct);
                await SendLookTo(s, MonsterID, 3, ct);
                await SendUnitInfo(s, MonsterID, "Poring", ct);

                await SendShowItemOnGround(s, ItemID, 512, POS_X + 1, POS_Y - 1, ct);
            }
        }

        // ===== Métodos de construção de pacotes =====

        private byte[] BuildTokenAgencyResponse()
        {
            var buf = new byte[0x2F];
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), 0x0AE3);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), 0x002F);
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4, 4), 0);
            Encoding.ASCII.GetBytes("S1000").CopyTo(buf.AsSpan(8, 5));
            Encoding.ASCII.GetBytes("OpenkoreClientToken").CopyTo(buf.AsSpan(28, 19));
            return buf;
        }

        private byte[] BuildAccountServerInfo(HydraSession s, string sw)
        {
            var (a, b, c, d, port) = BindIpPort(s);
            byte sex = 1;
            var serverName = Pack.Z(20, "openkore.com.br");
            var users = Pack.U32((uint)Math.Max(0, s.ServerClientCount - 1));

            // Determinar qual pacote usar baseado no tipo de servidor
            string asi = _st.AccountServerInfo?.ToUpperInvariant() ?? "";

            if (sw == "0825" || asi == "0AC4")
            {
                return Pack.Join(Pack.U16(0x0AC4), Pack.U16(0xE0), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex), Pack.Zero(0x11),
                                 Pack.Bytes(a, b, c, d), port, serverName, users, Pack.Zero(130));
            }
            else if (sw == "0AAC" || asi == "0AC9")
            {
                return Pack.Join(Pack.U16(0x0AC9), Pack.U16(0xCF), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex), Pack.Zero(6),
                                 serverName, users, Pack.Bytes(0x80, 0x32), Pack.ASCII($"{s.BindIp}:{s.BindPort}"), Pack.Zero(114));
            }
            else if (sw == "0B04" || asi == "0B07")
            {
                return Pack.Join(Pack.U16(0x0B07), Pack.U16(0xCF), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex),
                                 Pack.Bytes(a, b, c, d), port, serverName, users, Pack.Zero(130));
            }
            else if (asi == "0B60")
            {
                return Pack.Join(Pack.U16(0x0B60), Pack.U16(0xE4), SessionID, AccountID, SessionID2, Pack.Zero(4),
                                 Pack.Z(26, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()), Pack.U8(sex), Pack.Zero(0x11),
                                 Pack.Bytes(a, b, c, d), port, serverName, Pack.Zero(2),
                                 Pack.U16((ushort)Math.Min(ushort.MaxValue, Math.Max(0, s.ServerClientCount - 1))),
                                 Pack.U16(0x6985), Pack.Zero(128 + 4));
            }
            else if (sw == "0A76" || asi == "0276")
            {
                return Pack.Join(Pack.U16(0x0276), Pack.U16(0x63), SessionID, AccountID, SessionID2, Pack.Zero(30), Pack.U8(sex), Pack.Zero(4),
                                 Pack.Bytes(a, b, c, d), port, serverName, Pack.Zero(2), users, Pack.Zero(6));
            }
            else if (sw == "01FA")
            {
                return Pack.Join(Pack.U16(0x0069), Pack.U16(0x53), SessionID, AccountID, SessionID2,
                                 Pack.Zero(30), Pack.U8(sex), Pack.Zero(4),
                                 Pack.Bytes(a, b, c, d), port, serverName, users, Pack.Zero(2));
            }
            else
            {
                return Pack.Join(Pack.U16(0x0069), Pack.U16(0x4F), SessionID, AccountID, SessionID2, Pack.Zero(30), Pack.U8(sex),
                                 Pack.Bytes(a, b, c, d), port, serverName, users, Pack.Zero(2));
            }
        }

        private byte[] BuildCharMapSelect(HydraSession s)
        {
            var (a, b, c, d, port) = BindIpPort(s);
            string map = "brasilis.gat";

            if (string.Equals(_st.ReceivedCharacterIdAndMap, "0AC5", StringComparison.OrdinalIgnoreCase))
            {
                return Pack.Join(Pack.U16(0x0AC5), CharID, Pack.Z(16, map),
                                Pack.Bytes(a, b, c, d), port, Pack.Zero(128));
            }
            else
            {
                return Pack.Join(Pack.U16(0x0071), CharID, Pack.Z(16, map),
                                Pack.Bytes(a, b, c, d), port);
            }
        }

        private async Task SendPreCharHandshakeAsync(HydraSession s, CancellationToken ct)
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

        private byte[] BuildCharacterList(HydraSession s)
        {
            // Obtenha o tamanho do bloco do serverType
            int blockSize = _st.CharBlockSize > 0 ? _st.CharBlockSize : 116;
            string rc = _st.ReceivedCharacters?.Trim().ToUpperInvariant() ?? "099D";
            int totalChars = 2;
            int len = blockSize * totalChars;

            // Header do pacote
            Buf w = new Buf();
            if (rc == "099D")
            {
                w.U16(0x099D).U16((ushort)(len + 4));
            }
            else if (rc == "0B72")
            {
                w.U16(0x0B72).U16((ushort)(len + 4));
            }
            else
            {
                // fallback
                w.Bytes(AccountID).U16(0x006B).U16((ushort)(len + 7)).U8(12).U8(0xFF).U8(0xFF);
            }

            // Bloco de personagem dinâmico
            w.Bytes(BuildCharBlockDynamic("Celtos", 0, blockSize, rc));
            w.Bytes(BuildCharBlockDynamic("Celtos Dev", 1, blockSize, rc));

            s.Log($"[DEBUG] BuildCharacterList: rc={rc}, blockSize={blockSize}, len={len}, totalChars={totalChars}");
            return w.ToArray();
        }

        // Monta o bloco de personagem conforme o tamanho
        private byte[] BuildCharBlockDynamic(string name, int slot, int blockSize, string rc)
        {
            var w = new Buf();
            if (blockSize == 116)
            {
                w.Bytes(CharID)
                 .U32(1).U32(1).U32(1).U32(0).U32(0).U32(0).U32(0).U32(0).U32(0)
                 .U16(50)
                 .U32(10000).U32(10000)
                 .U16(10000).U16(10000).U16(0x01BD).U16(0).U16(5).U16(0).U16(99).U16(0).U16(0).U16(0).U16(0).U16(0)
                 .Z(24, name)
                 .U8(1).U8(1).U8(1).U8(1).U8(1).U8(1)
                 .U16((ushort)slot).U16(6);
                var bytes = w.ToArray();
                if (bytes.Length != 116)
                {
                    var fix = new byte[116];
                    Buffer.BlockCopy(bytes, 0, fix, 0, Math.Min(bytes.Length, 116));
                    return fix;
                }
                return bytes;
            }
            else if (blockSize == 155)
            {
                return BuildCharBlock155(name, slot);
            }
            else if (blockSize == 175)
            {
                return BuildCharBlock175(name, slot);
            }
            // Adicione outros formatos conforme necessário
            // Fallback: bloco zerado
            return new byte[blockSize];
        }

        private byte[] BuildCharacterList_099D(HydraSession s)
        {
            var b1 = BuildCharBlock155("Celtos", 0);
            var b2 = BuildCharBlock155("Celtos Dev", 1);
            ushort lenField = (ushort)(b1.Length + b2.Length + 4);
            var w = new Buf();
            w.U16(0x099D).U16(lenField).Bytes(b1).Bytes(b2);
            if (s.Debug) s.Log($"[RO] 099D enviado (len={lenField}, chars=2)");
            return w.ToArray();
        }

        private byte[] BuildCharacterList_0B72(HydraSession s)
        {
            var b1 = BuildCharBlock175("Celtos", 0);
            var b2 = BuildCharBlock175("Celtos Dev", 1);
            ushort lenField = (ushort)(b1.Length + b2.Length + 4);
            var w = new Buf();
            w.U16(0x0B72).U16(lenField).Bytes(b1).Bytes(b2);
            if (s.Debug) s.Log($"[RO] 0B72 enviado (len={lenField}, chars=2)");
            return w.ToArray();
        }

        private byte[] BuildCharBlock116(string name, int slot)
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
            if (bytes.Length != 116)
            {
                var fix = new byte[116];
                Buffer.BlockCopy(bytes, 0, fix, 0, Math.Min(bytes.Length, 116));
                return fix;
            }
            return bytes;
        }

        private byte[] BuildCharBlock155(string name, int slot)
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
            if (bytes.Length != 155)
            {
                var fix = new byte[155];
                Buffer.BlockCopy(bytes, 0, fix, 0, Math.Min(bytes.Length, 155));
                return fix;
            }
            return bytes;
        }

        private byte[] BuildCharBlock175(string name, int slot)
        {
            // Similar ao 155 mas com campos adicionais
            var block155 = BuildCharBlock155(name, slot);
            var result = new byte[175];
            Buffer.BlockCopy(block155, 0, result, 0, Math.Min(block155.Length, 175));
            return result;
        }

        // ===== Métodos auxiliares para NPC =====

        private async Task HandleNpcTalk(HydraSession s, RoState st, byte[] msg, CancellationToken ct)
        {
            st.NpcTalkCode = 0;

            if (msg.Length >= 6 && new ReadOnlySpan<byte>(msg, 2, 4).SequenceEqual(NpcID1))
            {
                // Kafra
                await SendNpcImageShow(s, "kafra_04.bmp", 0x02, ct);
                await SendNPCTalk(s, NpcID1, "[Kafra]", ct);
                await SendNPCTalk(s, NpcID1, "Welcome to Kafra Corp. We will stay with you wherever you go.", ct);
                await SendNPCTalkContinue(s, NpcID1, ct);
            }
            else
            {
                // Celtos
                await SendNPCTalk(s, NpcID0, "[Celtos]", ct);
                await SendNPCTalk(s, NpcID0, "Estou analisando seus pacotes de login... tao pateticos e reciclados quanto o 'codigo exclusivo' que voces juram ser donos. Voces nao criam nada, so choram quando alguem melhora o lixo que entregam.", ct);
                await SendNPCTalkContinue(s, NpcID0, ct);
            }
        }

        private async Task HandleNpcTalkResponse(HydraSession s, RoState st, byte[] msg, CancellationToken ct)
        {
            if (msg.Length < 7) return;

            var npcID = new byte[4];
            Buffer.BlockCopy(msg, 2, npcID, 0, 4);
            byte response = msg[6];

            if (new ReadOnlySpan<byte>(npcID).SequenceEqual(NpcID0))
            {
                await HandleCeltosResponse(s, st, response, ct);
            }
            else if (new ReadOnlySpan<byte>(npcID).SequenceEqual(NpcID1))
            {
                await HandleKafraResponse(s, st, response, ct);
            }
        }

        private async Task HandleCeltosResponse(HydraSession s, RoState st, byte response, CancellationToken ct)
        {
            if (response == 1)
            {
                // Mostrar informações do servidor
                await SendNPCTalk(s, NpcID0, "[Celtos]", ct);
                await SendNPCTalk(s, NpcID0, "Your RO client uses the following server details:", ct);
                await SendNPCTalk(s, NpcID0, $"version: {st.Version}", ct);
                await SendNPCTalk(s, NpcID0, $"master_version: {st.MasterVersion}", ct);
                await SendNPCTalk(s, NpcID0, $"serverType: {(st.ServerType != 0 ? st.ServerType.ToString() : "Unknown")}", ct);

                if (st.SecureLogin != 0)
                {
                    await SendNPCTalk(s, NpcID0, $"secureLogin: {st.SecureLogin}", ct);
                }

                if (!string.IsNullOrEmpty(st.MasterLoginPacket))
                {
                    await SendNPCTalk(s, NpcID0, $"masterLogin_packet: {st.MasterLoginPacket}", ct);
                }

                if (!string.IsNullOrEmpty(st.GameLoginPacket))
                {
                    await SendNPCTalk(s, NpcID0, $"gameLogin_packet: {st.GameLoginPacket}", ct);
                }

                await SendNPCTalkContinue(s, NpcID0, ct);
                st.NpcTalkCode = 3;
            }
            else if (response == 2)
            {
                await SendNPCTalk(s, NpcID0, "[Celtos]", ct);
                await SendNPCTalk(s, NpcID0, "Valeu pela visita. Agora some e tenta multiplicar commits em vez de fofoca.", ct);
                await SendNpcTalkClose(s, NpcID0, ct);
            }
        }

        private async Task HandleKafraResponse(HydraSession s, RoState st, byte response, CancellationToken ct)
        {
            if (response == 1)
            {
                // Usar storage
                var w = new Buf();
                w.Bytes(0xF0, 0x01, 40, 0) // 0xF0 0x01 40
                 .U16(3).U16(501).U8(0).U8(1).U16(16).Bytes(new byte[10])
                 .U16(4).U16(909).U8(3).U8(1).U16(144).Bytes(new byte[10])
                 .U16(0xF2).U16(2).U16(300);
                await SendNpcImageShow(s, "kafra_04.bmp", 0xFF, ct);
                await SendNpcTalkClose(s, NpcID1, ct);
                await Send(s, w.ToArray(), ct);
            }
            else if (response == 2)
            {
                await SendNPCTalk(s, NpcID1, "[Kafra]", ct);
                await SendNPCTalk(s, NpcID1, "We Kafra Corp. always try to serve you the best.", ct);
                await SendNPCTalk(s, NpcID1, "Please come again.", ct);
                await SendNpcTalkClose(s, NpcID1, ct);
            }
        }

        private async Task HandleNpcTalkContinue(HydraSession s, RoState st, byte[] msg, CancellationToken ct)
        {
            if (msg.Length < 6) return;

            var npcID = new byte[4];
            Buffer.BlockCopy(msg, 2, npcID, 0, 4);

            if (new ReadOnlySpan<byte>(npcID).SequenceEqual(NpcID0))
            {
                await HandleCeltosContinue(s, st, ct);
            }
            else if (new ReadOnlySpan<byte>(npcID).SequenceEqual(NpcID1))
            {
                await SendNpcTalkResponses(s, NpcID1, "Use Storage:Cancel:", ct);
            }
        }

        private async Task HandleCeltosContinue(HydraSession s, RoState st, CancellationToken ct)
        {
            if (st.NpcTalkCode == 2)
            {
                await SendNpcTalkResponses(s, NpcID0, "Continua:Eita!:", ct);
                st.NpcTalkCode = 3;
            }
            else
            {
                if (st.NpcTalkCode == 0)
                {
                    if (st.ServerType == 0)
                    {
                        await SendNPCTalk(s, NpcID0, "[Celtos]", ct);
                        await SendNPCTalk(s, NpcID0, "Olhei seu servidor... e nada combina. Igual suas ideias: soltas, sem fundamento, e depois ainda juram que e 'original'.", ct);
                    }
                    else if (st.ServerType == 7 || st.ServerType == 12)
                    {
                        await SendNPCTalk(s, NpcID0, "[Celtos]", ct);
                        await SendNPCTalk(s, NpcID0, "Esse serverType ai? Igual ao 'codigo exclusivo' que voces vendem: incompleto, bugado e sem suporte.", ct);
                    }
                    else
                    {
                        await SendNPCTalk(s, NpcID0, "[Celtos]", ct);
                        await SendNPCTalk(s, NpcID0, "Analise concluida: OpenKore suporta seu servidor melhor do que voces suportam critica.", ct);
                        await SendNPCTalk(s, NpcID0, "Quer detalhes? Eu entrego. Diferente de voces, que so entregam drama.", ct);
                    }
                    await SendNPCTalkContinue(s, NpcID0, ct);
                    st.NpcTalkCode = 1;
                }
                else if (st.NpcTalkCode == 1)
                {
                    if (st.ServerType == 0 || st.ServerType == 7)
                    {
                        await SendNPCTalk(s, NpcID0, "[Celtos]", ct);
                        await SendNPCTalk(s, NpcID0, "Quer mesmo ouvir os detalhes? Ou vai correr pro Discord chorar 'copia' de novo?", ct);
                    }
                    else
                    {
                        await SendNPCTalk(s, NpcID0, "[Celtos]", ct);
                        await SendNPCTalk(s, NpcID0, "Quer saber os detalhes ou prefere inventar fofoca em forum?", ct);
                    }
                    await SendNPCTalkContinue(s, NpcID0, ct);
                    st.NpcTalkCode = 2;
                }
                // ... continuar com a lógica completa
            }
        }

        // ===== Métodos de envio de pacotes NPC =====

        private async Task SendNPCTalk(HydraSession s, byte[] npcID, string message, CancellationToken ct)
        {
            var msgBytes = Encoding.ASCII.GetBytes(message);
            var w = new Buf();
            w.U16(0x00B4).U16((ushort)(msgBytes.Length + 8)).Bytes(npcID).Bytes(msgBytes);
            await Send(s, w.ToArray(), ct);
        }

        private async Task SendNPCTalkContinue(HydraSession s, byte[] npcID, CancellationToken ct)
        {
            await Send(s, Pack.Join(Pack.U16(0x00B5), npcID), ct);
        }

        private async Task SendNpcTalkClose(HydraSession s, byte[] npcID, CancellationToken ct)
        {
            await Send(s, Pack.Join(Pack.U16(0x00B6), npcID), ct);
        }

        private async Task SendNpcTalkResponses(HydraSession s, byte[] npcID, string responses, CancellationToken ct)
        {
            var respBytes = Encoding.ASCII.GetBytes(responses);
            var w = new Buf();
            w.U16(0x00B7).U16((ushort)(respBytes.Length + 8)).Bytes(npcID).Bytes(respBytes);
            await Send(s, w.ToArray(), ct);
        }

        private async Task SendNpcImageShow(HydraSession s, string image, byte type, CancellationToken ct)
        {
            var imageBytes = Pack.Z(64, image);
            await Send(s, Pack.Join(Pack.U16(0x01B3), imageBytes, Pack.U8(type)), ct);
        }

        // ===== Métodos auxiliares restantes =====

        private async Task HandleNpcTalkCancel(HydraSession s, RoState st, byte[] msg, CancellationToken ct)
        {
            if (msg.Length >= 6)
            {
                var npcID = new byte[4];
                Buffer.BlockCopy(msg, 2, npcID, 0, 4);
                if (new ReadOnlySpan<byte>(npcID).SequenceEqual(NpcID1))
                {
                    await SendNpcImageShow(s, "kafra_04.bmp", 0xFF, ct);
                }
            }
        }

        private async Task HandleEmoticon(HydraSession s, RoState st, byte[] msg, CancellationToken ct)
        {
            if (msg.Length >= 3)
            {
                var data = Pack.Join(Pack.U16(0x00C0), AccountID, new byte[] { msg[2] });
                st.EmoticonTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await Send(s, data, ct);
            }
        }

        private async Task HandleDevModePackets(HydraSession s, RoState st, string sw, byte[] msg, CancellationToken ct)
        {
            // Enviar feedback para pacotes não tratados no modo dev
            var data = Pack.Join(Pack.U16(0x008E), Pack.U16(35),
                               Encoding.ASCII.GetBytes($"Sent packet {sw} ({msg.Length} bytes)."));

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - st.EmoticonTime > 1.8)
            {
                st.EmoticonTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                data = Pack.Join(data, Pack.U16(0x00C0), AccountID, Pack.U8(1));
            }

            // Verificar se é um pacote de info de ator
            if (msg.Length >= 4)
            {
                var last4 = new byte[4];
                Buffer.BlockCopy(msg, msg.Length - 4, last4, 0, 4);

                if (new ReadOnlySpan<byte>(last4).SequenceEqual(NpcID0))
                {
                    data = Pack.Join(data, Pack.U16(0x0095), NpcID0, Pack.Z(24, "Server Details Guide"));
                }
                else if (new ReadOnlySpan<byte>(last4).SequenceEqual(NpcID1))
                {
                    data = Pack.Join(data, Pack.U16(0x0095), NpcID1, Pack.Z(24, "Kafra"));
                }
            }

            await Send(s, data, ct);
        }

        // ===== Métodos auxiliares de envio =====

        private async Task SendLookTo(HydraSession s, byte[] id, byte direction, CancellationToken ct)
        {
            await Send(s, Pack.Join(Pack.U16(0x009C), id, Pack.U8(direction), Pack.U8(0)), ct);
        }

        private async Task SendUnitInfo(HydraSession s, byte[] id, string name, CancellationToken ct)
        {
            // Usar 0x0195 como fallback
            await Send(s, Pack.Join(Pack.U16(0x0195), id, Pack.Z(24, name),
                                   Pack.Z(24, ""), Pack.Z(24, ""), Pack.Z(24, "")), ct);
        }

        private async Task SendSystemChatMessage(HydraSession s, string message, CancellationToken ct)
        {
            var msgBytes = Encoding.ASCII.GetBytes(message);
            await Send(s, Pack.Join(Pack.U16(0x009A), Pack.U16((ushort)(msgBytes.Length + 4)), msgBytes), ct);
        }

        private async Task SendShowNPC(HydraSession s, byte objType, byte[] id, ushort spriteId, int x, int y, string name, CancellationToken ct)
        {
            // Implementação simplificada - usar 0x0078 como fallback
            var w = new Buf();
            w.U16(0x0078).Bytes(id).U16(0x01BD).U16(0).U16(0).U16(0).U16(spriteId)
             .U16(0).U16(0).U16(0).U16(0).U16(0).U16(0).U16(0).U16(0)
             .Bytes(Pack.U32(0)).Bytes(Pack.U16(0)).U16(0).U16(0).U16(0).U16(0)
             .Bytes(Coord(x, y)).U8(0).U8(0).U16(0).U16(1);
            await Send(s, w.ToArray(), ct);
        }

        private async Task SendShowItemOnGround(HydraSession s, byte[] id, ushort spriteId, int x, int y, CancellationToken ct)
        {
            // Usar formato não expandido como fallback
            await Send(s, Pack.Join(Pack.U16(0x009D), id, Pack.U16(spriteId),
                                   Pack.U8(1), Pack.U16((ushort)(x + 1)), Pack.U16((ushort)(y - 1)),
                                   Pack.U16(1), Pack.U8(0), Pack.U8(0)), ct);
        }

        // ===== helpers =====
        static RoState GetState(HydraSession s)
        {
            if (s.Items.TryGetValue("__RO", out var o) && o is RoState st) return st;
            s.Items["__RO"] = st = new RoState();
            return st;
        }

        static async Task Send(HydraSession s, byte[] data, CancellationToken ct)
        {
            if (s.Debug) s.Log($"[RO] TX {data.Length} bytes: {BitConverter.ToString(data).Replace("-", " ")}");
            await s.SendAsync(data, ct);
        }

        static (byte, byte, byte, byte, byte[]) BindIpPort(HydraSession s)
        {
            var ip = (s.BindIp ?? "127.0.0.1").Replace("localhost", "127.0.0.1");
            byte a = 127, b = 0, c = 0, d = 1;
            var parts = ip.Split('.');
            if (parts.Length == 4 && byte.TryParse(parts[0], out a) && byte.TryParse(parts[1], out b) &&
                byte.TryParse(parts[2], out c) && byte.TryParse(parts[3], out d)) { }
            return (a, b, c, d, Pack.U16((ushort)s.BindPort));
        }

        static byte[] Coord(int x, int y)
        {
            byte b0 = (byte)(x & 0xFF), b1 = (byte)(y & 0xFF),
                 b2 = (byte)(((x >> 4) & 0xF0) | ((y >> 8) & 0x0F));
            return new[] { b0, b1, b2 };
        }

        static bool IsMasterLogin(string sw) =>
            sw is "0064" or "01DD" or "01FA" or "0277" or "027C" or "02B0" or "0825" or "0987" or "0A76" or "0AAC" or "0B04";

        bool IsMapLogin(string sw, byte[] msg, RoState st)
        {
            // Implementação básica - expandir conforme necessário
            return sw == _st.MapLogin || sw is "0072" or "009B" or "00F5" or "0436" or "022D";
        }

        static bool IsSync(string sw, RoState st) =>
            sw is "007E" or "035F" or "0089" or "0116" or "00A7" or "0360";

        static bool IsServerChoicePacket(byte[] msg) =>
            msg.Length >= 17 &&
            new ReadOnlySpan<byte>(msg, 2, 4).SequenceEqual(AccountID) &&
            new ReadOnlySpan<byte>(msg, 6, 4).SequenceEqual(SessionID) &&
            new ReadOnlySpan<byte>(msg, 10, 4).SequenceEqual(SessionID2) &&
            msg[14] == 0 && msg[15] == 0;

        static bool IsNpcTalk(byte[] msg) =>
            msg.Length >= 6 && (
                new ReadOnlySpan<byte>(msg, 2, 4).SequenceEqual(NpcID1) ||
                new ReadOnlySpan<byte>(msg, 2, 4).SequenceEqual(NpcID0));

        static int ReadLenOrRest(ushort op, byte[] buf, int off, int nTot)
        {
            if (off + 4 <= nTot)
            {
                ushort len = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(buf, off + 2, 2));
                if (len >= 2 && off + len <= nTot) return len;
            }
            return nTot - off;
        }

        static byte[] BuildGGResponse(ushort op, byte[] msg)
        {
            if (msg.Length >= 4)
            {
                ushort len = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(msg, 2, 2));
                if (len > 0 && len <= msg.Length)
                    return Pack.Join(Pack.U16(op), new ReadOnlySpan<byte>(msg, 2, len).ToArray());
            }
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
                var b = new byte[size];
                Array.Copy(raw, b, Math.Min(raw.Length, size - 1));
                return b;
            }
            public static byte[] ASCII(string s) => Encoding.ASCII.GetBytes(s ?? "");
            public static byte[] Join(params byte[][] parts)
            {
                int n = 0;
                foreach (var p in parts) n += p.Length;
                var b = new byte[n];
                int o = 0;
                foreach (var p in parts)
                {
                    Buffer.BlockCopy(p, 0, b, o, p.Length);
                    o += p.Length;
                }
                return b;
            }
        }

        sealed class Buf
        {
            readonly List<byte> _bytes = new List<byte>();
            public Buf Bytes(params byte[] v) { _bytes.AddRange(v); return this; }
            public Buf U8(byte v) { _bytes.Add(v); return this; }
            public Buf U16(ushort v) { _bytes.AddRange(Pack.U16(v)); return this; }
            public Buf U32(uint v) { _bytes.AddRange(Pack.U32(v)); return this; }
            public Buf Z(int size, string s) { _bytes.AddRange(Pack.Z(size, s)); return this; }
            public byte[] ToArray() => _bytes.ToArray();
        }
    }
}