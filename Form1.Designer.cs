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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            grid = new System.Windows.Forms.DataGridView();
            txtLog = new System.Windows.Forms.RichTextBox();
            btnReloadSel = new System.Windows.Forms.Button();
            btnStartSel = new System.Windows.Forms.Button();
            btnStopSel = new System.Windows.Forms.Button();
            lblStatus = new System.Windows.Forms.Label();
            uiTimer = new System.Windows.Forms.Timer(components);
            panel1 = new System.Windows.Forms.Panel();
            lblTitle = new System.Windows.Forms.Label();
            pictureBox1 = new System.Windows.Forms.PictureBox();

            ((System.ComponentModel.ISupportInitialize)(grid)).BeginInit();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(pictureBox1)).BeginInit();
            SuspendLayout();

            // 
            // panel1
            // 
            panel1.BackColor = System.Drawing.Color.FromArgb(30, 30, 40);
            panel1.Controls.Add(lblTitle);
            panel1.Controls.Add(pictureBox1);
            panel1.Dock = System.Windows.Forms.DockStyle.Top;
            panel1.Location = new System.Drawing.Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(884, 60);
            panel1.TabIndex = 5;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblTitle.ForeColor = System.Drawing.Color.White;
            lblTitle.Location = new System.Drawing.Point(60, 18);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new System.Drawing.Size(280, 25);
            lblTitle.TabIndex = 1;
            lblTitle.Text = "HydraServer — RO ↔ QRY Multiport";
            // 
            // pictureBox1
            // 
            pictureBox1.Image = (System.Drawing.Image)resources.GetObject("pictureBox1.Image");
            pictureBox1.Location = new System.Drawing.Point(12, 12);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new System.Drawing.Size(36, 36);
            pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // grid
            // 
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = System.Drawing.Color.FromArgb(45, 45, 55);
            grid.BorderStyle = System.Windows.Forms.BorderStyle.None;
            grid.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(60, 60, 70);
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.Padding = new System.Windows.Forms.Padding(0, 5, 0, 5);
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(60, 60, 70);
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            grid.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(45, 45, 55);
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(80, 80, 100);
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            grid.DefaultCellStyle = dataGridViewCellStyle2;
            grid.EnableHeadersVisualStyles = false;
            grid.GridColor = System.Drawing.Color.FromArgb(60, 60, 70);
            grid.Location = new System.Drawing.Point(12, 75);
            grid.MultiSelect = false;
            grid.Name = "grid";
            grid.ReadOnly = true;
            grid.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.FromArgb(45, 45, 55);
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.FromArgb(45, 45, 55);
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            grid.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            grid.RowHeadersVisible = false;
            grid.RowTemplate.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(45, 45, 55);
            grid.RowTemplate.DefaultCellStyle.ForeColor = System.Drawing.Color.White;
            grid.RowTemplate.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(80, 80, 100);
            grid.RowTemplate.Height = 25;
            grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            grid.Size = new System.Drawing.Size(860, 200);
            grid.TabIndex = 0;
            // 
            // btnReloadSel
            // 
            btnReloadSel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnReloadSel.BackColor = System.Drawing.Color.FromArgb(70, 130, 180);
            btnReloadSel.FlatAppearance.BorderSize = 0;
            btnReloadSel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnReloadSel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnReloadSel.ForeColor = System.Drawing.Color.White;
            btnReloadSel.Location = new System.Drawing.Point(12, 285);
            btnReloadSel.Name = "btnReloadSel";
            btnReloadSel.Size = new System.Drawing.Size(120, 34);
            btnReloadSel.TabIndex = 1;
            btnReloadSel.Text = "🔄 Reload Par";
            btnReloadSel.UseVisualStyleBackColor = false;
            btnReloadSel.Click += new System.EventHandler(this.btnReloadSel_Click);
            // 
            // btnStartSel
            // 
            btnStartSel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnStartSel.BackColor = System.Drawing.Color.FromArgb(60, 179, 113);
            btnStartSel.FlatAppearance.BorderSize = 0;
            btnStartSel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnStartSel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnStartSel.ForeColor = System.Drawing.Color.White;
            btnStartSel.Location = new System.Drawing.Point(140, 285);
            btnStartSel.Name = "btnStartSel";
            btnStartSel.Size = new System.Drawing.Size(120, 34);
            btnStartSel.TabIndex = 2;
            btnStartSel.Text = "▶ Start Par";
            btnStartSel.UseVisualStyleBackColor = false;
            btnStartSel.Click += new System.EventHandler(this.btnStartSel_Click);
            // 
            // btnStopSel
            // 
            btnStopSel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnStopSel.BackColor = System.Drawing.Color.FromArgb(220, 80, 80);
            btnStopSel.FlatAppearance.BorderSize = 0;
            btnStopSel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnStopSel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnStopSel.ForeColor = System.Drawing.Color.White;
            btnStopSel.Location = new System.Drawing.Point(268, 285);
            btnStopSel.Name = "btnStopSel";
            btnStopSel.Size = new System.Drawing.Size(120, 34);
            btnStopSel.TabIndex = 3;
            btnStopSel.Text = "⏹ Stop Par";
            btnStopSel.UseVisualStyleBackColor = false;
            btnStopSel.Click += new System.EventHandler(this.btnStopSel_Click);
            // 
            // lblStatus
            // 
            lblStatus.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            lblStatus.AutoSize = false;
            lblStatus.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblStatus.ForeColor = System.Drawing.Color.White;
            lblStatus.Location = new System.Drawing.Point(400, 292);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new System.Drawing.Size(472, 24);
            lblStatus.TabIndex = 4;
            lblStatus.Text = "Carregando...";
            lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtLog
            // 
            txtLog.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtLog.BackColor = System.Drawing.Color.FromArgb(30, 30, 40);
            txtLog.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtLog.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtLog.ForeColor = System.Drawing.Color.FromArgb(200, 200, 200);
            txtLog.Location = new System.Drawing.Point(12, 325);
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.Size = new System.Drawing.Size(860, 224);
            txtLog.TabIndex = 5;
            txtLog.Text = "";
            // 
            // uiTimer
            // 
            uiTimer.Interval = 500;
            uiTimer.Tick += new System.EventHandler(this.uiTimer_Tick);
            // 
            // Form1
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(45, 45, 55);
            ClientSize = new System.Drawing.Size(884, 561);
            Controls.Add(txtLog);
            Controls.Add(lblStatus);
            Controls.Add(btnStopSel);
            Controls.Add(btnStartSel);
            Controls.Add(btnReloadSel);
            Controls.Add(grid);
            Controls.Add(panel1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MinimumSize = new System.Drawing.Size(800, 500);
            Name = "Form1";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "HydraServer";
            FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(grid)).EndInit();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(pictureBox1)).EndInit();
            ResumeLayout(false);
        }
    }
}