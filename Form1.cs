using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace HydraServer
{
    public partial class Form1 : Form
    {
        readonly System.Collections.Generic.List<HydraPair> _pairs = new();
        HydraConfig? _cfg;
        ServerTypeDatabase? _stDb;
        ServerType? _st;

        public Form1()
        {
            InitializeComponent();
            SetupGridAppearance();
            grid.DataSource = BuildTable();
            uiTimer.Enabled = true;
        }

        void SetupGridAppearance()
        {
            // Configurar cores das linhas alternadas
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(50, 50, 60);
        }

        DataTable BuildTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("Par", typeof(int));
            dt.Columns.Add("RO", typeof(string));
            dt.Columns.Add("QRY", typeof(string));
            dt.Columns.Add("Status", typeof(string));
            return dt;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;

                // hydra.cfg
                string cfgPath = Path.Combine(baseDir, "hydra.cfg");
                if (!File.Exists(cfgPath))
                {
                    var probe = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\hydra.cfg"));
                    if (File.Exists(probe)) cfgPath = probe;
                }
                _cfg = HydraConfig.Load(cfgPath);

                // servertypes.txt (obrigatório)
                string stPath = Path.Combine(baseDir, "servertypes.txt");
                if (!File.Exists(stPath))
                {
                    var probe = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\servertypes.txt"));
                    if (File.Exists(probe)) stPath = probe;
                }
                _stDb = ServerTypeDatabase.LoadFromFile(stPath);
                if (!_stDb.TryGet(_cfg.ServerType ?? "", out _st))
                    throw new Exception($"server_type '{_cfg.ServerType}' não encontrado em servertypes.txt");

                // killer de portas (Windows)
                PortKiller.FreeRequestedPorts(_cfg.RoPorts.Concat(_cfg.QryPorts), logInfo: Log, logErr: Log);

                // listeners
                _pairs.Clear();
                for (int i = 0; i < _cfg.RoPorts.Count; i++)
                {
                    var pair = new HydraPair(
                        index: i + 1,
                        roIp: _cfg.RagnarokIp, roPort: _cfg.RoPorts[i],
                        qryIp: _cfg.QueryIp, qryPort: _cfg.QryPorts[i],
                        st: _st!, debug: _cfg.Debug
                    );
                    pair.Logger = Log;

                    if (!pair.CanBindBoth())
                    {
                        Log($"[AVISO] Par {i + 1} indisponível (porta em uso/sem permissão) - RO:{_cfg.RagnarokIp}:{_cfg.RoPorts[i]}, QRY:{_cfg.QueryIp}:{_cfg.QryPorts[i]}");
                        continue;
                    }
                    else
                    {
                        Log($"[OK] Par {i + 1} pode bind em ambas portas");
                    }

                    pair.Start();
                    Log($"[SUCESSO] Par {i + 1}  RO:{pair.RoEndPoint}  <->  QRY:{pair.QryEndPoint}");

                    pair.StateChanged += _ => RefreshGrid();
                    _pairs.Add(pair);
                }

                if (_pairs.Count == 0)
                    throw new Exception("Nenhum par disponível para bind.");

                RefreshGrid();
                Log($"✅ HydraServer pronto. Pares ativos: {_pairs.Count}");
                UpdateStatusLabel();
            }
            catch (Exception ex)
            {
                Log($"[ERRO] {ex.Message}");
                MessageBox.Show(this, ex.ToString(), "HydraServer - Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void RefreshGrid()
        {
            if (InvokeRequired) { BeginInvoke((Action)RefreshGrid); return; }

            var dt = (DataTable)grid.DataSource!;
            dt.Rows.Clear();

            foreach (var p in _pairs)
            {
                string status = p.State switch
                {
                    PairState.Running => "🟢 Rodando",
                    PairState.Stopped => "🔴 Parado",
                    PairState.Error => "⚠️ Erro",
                    _ => "❓ Desconhecido"
                };

                dt.Rows.Add(
                    p.Index,
                    $"{p.RoEndPoint.Address}:{p.RoEndPoint.Port}",
                    $"{p.QryEndPoint.Address}:{p.QryEndPoint.Port}",
                    status
                );
            }

            UpdateStatusLabel();
        }

        void UpdateStatusLabel()
        {
            lblStatus.Text = $"ServerType: {_cfg?.ServerType ?? "-"} | Debug: {(_cfg?.Debug ?? false ? "ON" : "OFF")} | FakeIP: {_cfg?.FakeIp ?? "-"} | Pares: {_pairs.Count}";
        }

        HydraPair? SelectedPair()
        {
            if (grid.CurrentRow == null) return null;
            int idx = grid.CurrentRow.Index;
            if (idx < 0 || idx >= _pairs.Count) return null;
            return _pairs[idx];
        }

        private void btnReloadSel_Click(object sender, EventArgs e)
        {
            var p = SelectedPair();
            if (p is null)
            {
                MessageBox.Show("Selecione um par primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                
                Log($"🔄 Reload OK (Par {p.Index})");
                RefreshGrid();
            }
            catch (Exception ex)
            {
                Log($"❌ Reload falhou: {ex.Message}");
                MessageBox.Show($"Falha ao recarregar par {p.Index}: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnStartSel_Click(object sender, EventArgs e)
        {
            var p = SelectedPair();
            if (p is null)
            {
                MessageBox.Show("Selecione um par primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                p.Start();
                Log($"▶ Start OK (Par {p.Index})");
                RefreshGrid();
            }
            catch (Exception ex)
            {
                Log($"❌ Start falhou: {ex.Message}");
                MessageBox.Show($"Falha ao iniciar par {p.Index}: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnStopSel_Click(object sender, EventArgs e)
        {
            var p = SelectedPair();
            if (p is null)
            {
                MessageBox.Show("Selecione um par primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                p.Stop();
                Log($"⏹ Stop OK (Par {p.Index})");
                RefreshGrid();
            }
            catch (Exception ex)
            {
                Log($"❌ Stop falhou: {ex.Message}");
                MessageBox.Show($"Falha ao parar par {p.Index}: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void uiTimer_Tick(object sender, EventArgs e)
        {
            // Atualiza status periodicamente
            foreach (var pair in _pairs)
            {
                // Verifica se sockets ainda estão ativos
                // Futura telemetria aqui
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Log("🛑 Encerrando HydraServer...");
            foreach (var p in _pairs)
            {
                try
                {
                    p.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log($"[AVISO] Erro ao encerrar par {p.Index}: {ex.Message}");
                }
            }
            Log("✅ HydraServer encerrado.");
        }

        void Log(string msg)
        {
            if (!txtLog.IsHandleCreated) return;

            var timestamp = $"[{DateTime.Now:HH:mm:ss}]";
            var formattedMsg = $"{timestamp} {msg}";

            txtLog.BeginInvoke(new Action(() =>
            {
                txtLog.AppendText($"{formattedMsg}\r\n");
                txtLog.ScrollToCaret();
            }));
        }
    }
}