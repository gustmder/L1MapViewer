using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using L1MapViewer.Other;

namespace L1FlyMapViewer
{
    partial class MapForm
    {
        private IContainer components;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem openToolStripMenuItem;
        private StatusStrip statusStrip1;
        public ToolStripStatusLabel toolStripStatusLabel1;
        public ToolStripProgressBar toolStripProgressBar1;
        public ToolStripStatusLabel toolStripStatusLabel2;
        public ToolStripStatusLabel toolStripStatusLabel3;

        // 左側面板
        private Panel leftPanel;
        public ComboBox comboBox1;
        private PictureBox miniMapPictureBox;

        // 中間地圖顯示區域
        public ZoomablePanel panel1;
        public PictureBox pictureBox4;
        public PictureBox pictureBox3;
        public PictureBox pictureBox2;
        public PictureBox pictureBox1;
        public VScrollBar vScrollBar1;
        public HScrollBar hScrollBar1;

        protected override void Dispose(bool disposing)
        {
            if (disposing && this.components != null)
                this.components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new Container();

            // MenuStrip
            this.menuStrip1 = new MenuStrip();
            this.openToolStripMenuItem = new ToolStripMenuItem();

            // StatusStrip
            this.statusStrip1 = new StatusStrip();
            this.toolStripStatusLabel1 = new ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new ToolStripStatusLabel();
            this.toolStripStatusLabel3 = new ToolStripStatusLabel();
            this.toolStripProgressBar1 = new ToolStripProgressBar();

            // 左側面板
            this.leftPanel = new Panel();
            this.comboBox1 = new ComboBox();
            this.miniMapPictureBox = new PictureBox();

            // 中間地圖面板
            this.panel1 = new ZoomablePanel();
            this.pictureBox4 = new PictureBox();
            this.pictureBox3 = new PictureBox();
            this.pictureBox2 = new PictureBox();
            this.pictureBox1 = new PictureBox();
            this.vScrollBar1 = new VScrollBar();
            this.hScrollBar1 = new HScrollBar();

            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.leftPanel.SuspendLayout();
            ((ISupportInitialize)this.miniMapPictureBox).BeginInit();
            this.panel1.SuspendLayout();
            ((ISupportInitialize)this.pictureBox4).BeginInit();
            ((ISupportInitialize)this.pictureBox3).BeginInit();
            ((ISupportInitialize)this.pictureBox2).BeginInit();
            ((ISupportInitialize)this.pictureBox1).BeginInit();
            this.SuspendLayout();

            //
            // menuStrip1
            //
            this.menuStrip1.Items.AddRange(new ToolStripItem[] {
                this.openToolStripMenuItem
            });
            this.menuStrip1.Location = new Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new Size(1200, 24);
            this.menuStrip1.TabIndex = 0;

            //
            // openToolStripMenuItem
            //
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new Size(164, 20);
            this.openToolStripMenuItem.Text = "開啟天堂客戶端讀取地圖";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);

            //
            // statusStrip1
            //
            this.statusStrip1.Items.AddRange(new ToolStripItem[] {
                this.toolStripStatusLabel1,
                this.toolStripStatusLabel2,
                this.toolStripStatusLabel3,
                this.toolStripProgressBar1
            });
            this.statusStrip1.Location = new Point(0, 678);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new Size(1200, 22);
            this.statusStrip1.TabIndex = 1;

            //
            // toolStripStatusLabel1
            //
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new Size(280, 17);
            this.toolStripStatusLabel1.Text = "点击获取坐标 | Ctrl+拖拽選择範圍 | Ctrl+滾輪縮放";

            //
            // toolStripStatusLabel2
            //
            this.toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            this.toolStripStatusLabel2.Size = new Size(100, 17);

            //
            // toolStripStatusLabel3
            //
            this.toolStripStatusLabel3.Name = "toolStripStatusLabel3";
            this.toolStripStatusLabel3.Size = new Size(885, 17);
            this.toolStripStatusLabel3.Spring = true;

            //
            // toolStripProgressBar1
            //
            this.toolStripProgressBar1.Name = "toolStripProgressBar1";
            this.toolStripProgressBar1.Size = new Size(100, 16);
            this.toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
            this.toolStripProgressBar1.Visible = false;

            //
            // leftPanel
            //
            this.leftPanel.BorderStyle = BorderStyle.FixedSingle;
            this.leftPanel.Controls.Add(this.comboBox1);
            this.leftPanel.Controls.Add(this.miniMapPictureBox);
            this.leftPanel.Dock = DockStyle.Left;
            this.leftPanel.Location = new Point(0, 24);
            this.leftPanel.Name = "leftPanel";
            this.leftPanel.Size = new Size(280, 654);
            this.leftPanel.TabIndex = 2;

            //
            // comboBox1
            //
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new Point(10, 10);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new Size(260, 21);
            this.comboBox1.TabIndex = 0;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);

            //
            // miniMapPictureBox
            //
            this.miniMapPictureBox.BackColor = Color.Black;
            this.miniMapPictureBox.BorderStyle = BorderStyle.FixedSingle;
            this.miniMapPictureBox.Location = new Point(10, 40);
            this.miniMapPictureBox.Name = "miniMapPictureBox";
            this.miniMapPictureBox.Size = new Size(260, 260);
            this.miniMapPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            this.miniMapPictureBox.TabIndex = 1;
            this.miniMapPictureBox.TabStop = false;
            this.miniMapPictureBox.Cursor = Cursors.Hand;
            this.miniMapPictureBox.MouseClick += new MouseEventHandler(this.miniMapPictureBox_MouseClick);

            //
            // panel1 (中間地圖顯示區域)
            //
            this.panel1.BackColor = Color.Black;
            this.panel1.BorderStyle = BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.pictureBox4);
            this.panel1.Controls.Add(this.pictureBox3);
            this.panel1.Controls.Add(this.pictureBox2);
            this.panel1.Controls.Add(this.pictureBox1);
            this.panel1.Location = new Point(290, 34);
            this.panel1.Name = "panel1";
            this.panel1.Size = new Size(700, 600);
            this.panel1.TabIndex = 3;

            //
            // pictureBox4
            //
            this.pictureBox4.BackColor = Color.Transparent;
            this.pictureBox4.Dock = DockStyle.Fill;
            this.pictureBox4.Location = new Point(0, 0);
            this.pictureBox4.Name = "pictureBox4";
            this.pictureBox4.Size = new Size(panel1.Width, panel1.Height);
            this.pictureBox4.TabIndex = 3;
            this.pictureBox4.TabStop = false;

            //
            // pictureBox3
            //
            this.pictureBox3.BackColor = Color.Transparent;
            this.pictureBox3.Dock = DockStyle.Fill;
            this.pictureBox3.Location = new Point(0, 0);
            this.pictureBox3.Name = "pictureBox3";
            this.pictureBox3.Size = new Size(panel1.Width, panel1.Height);
            this.pictureBox3.TabIndex = 2;
            this.pictureBox3.TabStop = false;

            //
            // pictureBox2
            //
            this.pictureBox2.BackColor = Color.Transparent;
            this.pictureBox2.Dock = DockStyle.Fill;
            this.pictureBox2.Location = new Point(0, 0);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new Size(panel1.Width, panel1.Height);
            this.pictureBox2.TabIndex = 1;
            this.pictureBox2.TabStop = false;
            this.pictureBox2.Paint += new PaintEventHandler(this.pictureBox2_Paint);
            this.pictureBox2.MouseDown += new MouseEventHandler(this.pictureBox2_MouseDown);
            this.pictureBox2.MouseMove += new MouseEventHandler(this.pictureBox2_MouseMove);
            this.pictureBox2.MouseUp += new MouseEventHandler(this.pictureBox2_MouseUp);

            //
            // pictureBox1
            //
            this.pictureBox1.Location = new Point(3, 3);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new Size(100, 50);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;

            //
            // vScrollBar1
            //
            this.vScrollBar1.Location = new Point(993, 34);
            this.vScrollBar1.Name = "vScrollBar1";
            this.vScrollBar1.Size = new Size(17, 600);
            this.vScrollBar1.TabIndex = 4;
            this.vScrollBar1.Scroll += new ScrollEventHandler(this.vScrollBar1_Scroll);

            //
            // hScrollBar1
            //
            this.hScrollBar1.Location = new Point(290, 637);
            this.hScrollBar1.Name = "hScrollBar1";
            this.hScrollBar1.Size = new Size(700, 17);
            this.hScrollBar1.TabIndex = 5;
            this.hScrollBar1.Scroll += new ScrollEventHandler(this.hScrollBar1_Scroll);

            //
            // MapForm
            //
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1200, 700);
            this.Controls.Add(this.hScrollBar1);
            this.Controls.Add(this.vScrollBar1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.leftPanel);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MapForm";
            this.Text = "地圖編輯器";
            this.Load += new System.EventHandler(this.MapForm_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.leftPanel.ResumeLayout(false);
            this.leftPanel.PerformLayout();
            ((ISupportInitialize)this.miniMapPictureBox).EndInit();
            this.panel1.ResumeLayout(false);
            ((ISupportInitialize)this.pictureBox4).EndInit();
            ((ISupportInitialize)this.pictureBox3).EndInit();
            ((ISupportInitialize)this.pictureBox2).EndInit();
            ((ISupportInitialize)this.pictureBox1).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
