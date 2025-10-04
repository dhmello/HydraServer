namespace HydraServer
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.DataGridView grid;
        private System.Windows.Forms.RichTextBox txtLog;
        private System.Windows.Forms.Button btnReloadSel;
        private System.Windows.Forms.Button btnStartSel;
        private System.Windows.Forms.Button btnStopSel;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Timer uiTimer;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.PictureBox pictureBox1;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            grid = new DataGridView();
            txtLog = new RichTextBox();
            btnReloadSel = new Button();
            btnStartSel = new Button();
            btnStopSel = new Button();
            lblStatus = new Label();
            uiTimer = new System.Windows.Forms.Timer(components);
            panel1 = new Panel();
            lblTitle = new Label();
            pictureBox1 = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)grid).BeginInit();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // grid
            // 
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.FromArgb(45, 45, 55);
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(60, 60, 70);
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = Color.White;
            dataGridViewCellStyle1.Padding = new Padding(0, 5, 0, 5);
            dataGridViewCellStyle1.SelectionBackColor = Color.FromArgb(60, 60, 70);
            dataGridViewCellStyle1.SelectionForeColor = Color.White;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(45, 45, 55);
            dataGridViewCellStyle2.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle2.ForeColor = Color.White;
            dataGridViewCellStyle2.SelectionBackColor = Color.FromArgb(80, 80, 100);
            dataGridViewCellStyle2.SelectionForeColor = Color.White;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            grid.DefaultCellStyle = dataGridViewCellStyle2;
            grid.EnableHeadersVisualStyles = false;
            grid.GridColor = Color.FromArgb(60, 60, 70);
            grid.Location = new Point(12, 75);
            grid.MultiSelect = false;
            grid.Name = "grid";
            grid.ReadOnly = true;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = Color.FromArgb(45, 45, 55);
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle3.ForeColor = Color.White;
            dataGridViewCellStyle3.SelectionBackColor = Color.FromArgb(45, 45, 55);
            dataGridViewCellStyle3.SelectionForeColor = Color.White;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.True;
            grid.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            grid.RowHeadersVisible = false;
            grid.RowTemplate.DefaultCellStyle.BackColor = Color.FromArgb(45, 45, 55);
            grid.RowTemplate.DefaultCellStyle.ForeColor = Color.White;
            grid.RowTemplate.DefaultCellStyle.SelectionBackColor = Color.FromArgb(80, 80, 100);
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.Size = new Size(860, 200);
            grid.TabIndex = 0;
            // 
            // txtLog
            // 
            txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtLog.BackColor = Color.FromArgb(30, 30, 40);
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.ForeColor = Color.FromArgb(200, 200, 200);
            txtLog.Location = new Point(12, 325);
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.Size = new Size(860, 224);
            txtLog.TabIndex = 5;
            txtLog.Text = "";
            // 
            // btnReloadSel
            // 
            btnReloadSel.BackColor = Color.FromArgb(70, 130, 180);
            btnReloadSel.FlatAppearance.BorderSize = 0;
            btnReloadSel.FlatStyle = FlatStyle.Flat;
            btnReloadSel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnReloadSel.ForeColor = Color.White;
            btnReloadSel.Location = new Point(12, 285);
            btnReloadSel.Name = "btnReloadSel";
            btnReloadSel.Size = new Size(120, 34);
            btnReloadSel.TabIndex = 1;
            btnReloadSel.Text = "🔄 Reload Par";
            btnReloadSel.UseVisualStyleBackColor = false;
            btnReloadSel.Click += btnReloadSel_Click;
            // 
            // btnStartSel
            // 
            btnStartSel.BackColor = Color.FromArgb(60, 179, 113);
            btnStartSel.FlatAppearance.BorderSize = 0;
            btnStartSel.FlatStyle = FlatStyle.Flat;
            btnStartSel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnStartSel.ForeColor = Color.White;
            btnStartSel.Location = new Point(140, 285);
            btnStartSel.Name = "btnStartSel";
            btnStartSel.Size = new Size(120, 34);
            btnStartSel.TabIndex = 2;
            btnStartSel.Text = "▶ Start Par";
            btnStartSel.UseVisualStyleBackColor = false;
            btnStartSel.Click += btnStartSel_Click;
            // 
            // btnStopSel
            // 
            btnStopSel.BackColor = Color.FromArgb(220, 80, 80);
            btnStopSel.FlatAppearance.BorderSize = 0;
            btnStopSel.FlatStyle = FlatStyle.Flat;
            btnStopSel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnStopSel.ForeColor = Color.White;
            btnStopSel.Location = new Point(268, 285);
            btnStopSel.Name = "btnStopSel";
            btnStopSel.Size = new Size(120, 34);
            btnStopSel.TabIndex = 3;
            btnStopSel.Text = "⏹ Stop Par";
            btnStopSel.UseVisualStyleBackColor = false;
            btnStopSel.Click += btnStopSel_Click;
            // 
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblStatus.Font = new Font("Segoe UI", 9F);
            lblStatus.ForeColor = Color.White;
            lblStatus.Location = new Point(400, 292);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(472, 24);
            lblStatus.TabIndex = 4;
            lblStatus.Text = "Carregando...";
            lblStatus.TextAlign = ContentAlignment.MiddleRight;
            // 
            // uiTimer
            // 
            uiTimer.Interval = 500;
            uiTimer.Tick += uiTimer_Tick;
            // 
            // panel1
            // 
            panel1.BackColor = Color.FromArgb(30, 30, 40);
            panel1.Controls.Add(lblTitle);
            panel1.Controls.Add(pictureBox1);
            panel1.Dock = DockStyle.Top;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(884, 60);
            panel1.TabIndex = 5;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(60, 18);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(336, 25);
            lblTitle.TabIndex = 1;
            lblTitle.Text = "HydraServer — RO ↔ QRY Multiport";
            // 
            // pictureBox1
            // 
            pictureBox1.Image = Properties.Resources.icon;
            pictureBox1.Location = new Point(12, 12);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(36, 36);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(45, 45, 55);
            ClientSize = new Size(884, 561);
            Controls.Add(txtLog);
            Controls.Add(lblStatus);
            Controls.Add(btnStopSel);
            Controls.Add(btnStartSel);
            Controls.Add(btnReloadSel);
            Controls.Add(grid);
            Controls.Add(panel1);
            MinimumSize = new Size(800, 500);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "HydraServer";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)grid).EndInit();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
        }
    }
}