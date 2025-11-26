using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using L1MapViewer;
using L1MapViewer.Helper;
using L1MapViewer.Other;

namespace L1FlyMapViewer
{
    public partial class MapForm : Form, IMapViewer
    {
        // IMapViewer 介面實作 - 明確公開控制項屬性
        ComboBox IMapViewer.comboBox1 => this.comboBox1;
        PictureBox IMapViewer.pictureBox1 => this.pictureBox1;
        PictureBox IMapViewer.pictureBox2 => this.pictureBox2;
        PictureBox IMapViewer.pictureBox3 => this.pictureBox3;
        PictureBox IMapViewer.pictureBox4 => this.pictureBox4;
        VScrollBar IMapViewer.vScrollBar1 => this.vScrollBar1;
        HScrollBar IMapViewer.hScrollBar1 => this.hScrollBar1;
        ToolStripProgressBar IMapViewer.toolStripProgressBar1 => this.toolStripProgressBar1;
        ToolStripStatusLabel IMapViewer.toolStripStatusLabel1 => this.toolStripStatusLabel1;
        ToolStripStatusLabel IMapViewer.toolStripStatusLabel2 => this.toolStripStatusLabel2;
        ToolStripStatusLabel IMapViewer.toolStripStatusLabel3 => this.toolStripStatusLabel3;
        Panel IMapViewer.panel1 => this.panel1;

        private Point mouseDownPoint;
        private bool isMouseDrag;
        private const int DRAG_THRESHOLD = 5;

        // 縮放相關
        public double zoomLevel { get; set; } = 1.0;
        private const double ZOOM_MIN = 0.1;
        private const double ZOOM_MAX = 5.0;
        private const double ZOOM_STEP = 0.2;
        private Image originalMapImage;

        public MapForm()
        {
            InitializeComponent();

            // 註冊滑鼠滾輪事件用於縮放
            this.panel1.MouseWheel += Panel1_MouseWheel;

            // 確保 panel1 可以接收焦點
            this.panel1.TabStop = true;

            // 當滑鼠進入 panel1 時自動取得焦點
            this.panel1.MouseEnter += (s, e) => this.panel1.Focus();

            // 設置 PictureBox 的 SizeMode 為 StretchImage
            this.pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            this.pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            this.pictureBox3.SizeMode = PictureBoxSizeMode.StretchImage;
            this.pictureBox4.SizeMode = PictureBoxSizeMode.StretchImage;
        }

        private void MapForm_Load(object sender, EventArgs e)
        {
            // 自動載入已有的地圖資料
            if (Share.MapDataList != null && Share.MapDataList.Count > 0)
            {
                this.comboBox1.Items.Clear();
                this.comboBox1.BeginUpdate();
                foreach (string key in Utils.SortAsc(Share.MapDataList.Keys))
                {
                    Struct.L1Map l1Map = Share.MapDataList[key];
                    this.comboBox1.Items.Add(string.Format("{0}-{1}", key, l1Map.szName));
                }
                this.comboBox1.EndUpdate();
                if (this.comboBox1.Items.Count > 0)
                {
                    this.comboBox1.SelectedIndex = 0;
                }
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "請選擇天堂資料夾";
                folderDialog.ShowNewFolderButton = false;

                string iniPath = Path.GetTempPath() + "mapviewer.ini";
                if (File.Exists(iniPath))
                {
                    string savedPath = Utils.GetINI("Path", "LineagePath", "", iniPath);
                    if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
                        folderDialog.SelectedPath = savedPath;
                }
                else
                {
                    folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                }

                if (folderDialog.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(folderDialog.SelectedPath))
                    return;

                this.toolStripStatusLabel3.Text = folderDialog.SelectedPath;
                Share.LineagePath = folderDialog.SelectedPath;
                Utils.WriteINI("Path", "LineagePath", folderDialog.SelectedPath, iniPath);
                this.LoadMap(folderDialog.SelectedPath);
            }
        }

        public void LoadMap(string selectedPath)
        {
            Utils.ShowProgressBar(true, this);
            var dictionary = L1MapHelper.Read(selectedPath);
            this.comboBox1.Items.Clear();
            this.comboBox1.BeginUpdate();
            foreach (string key in Utils.SortAsc(dictionary.Keys))
            {
                Struct.L1Map l1Map = dictionary[key];
                this.comboBox1.Items.Add(string.Format("{0}-{1}", key, l1Map.szName));
            }
            this.comboBox1.EndUpdate();
            if (this.comboBox1.Items.Count > 0)
            {
                this.comboBox1.SelectedIndex = 0;
            }
            this.toolStripStatusLabel2.Text = "MapCount=" + dictionary.Count;
            Utils.ShowProgressBar(false, this);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.comboBox1.SelectedItem == null)
                return;

            // 重置縮放級別
            zoomLevel = 1.0;
            if (originalMapImage != null)
            {
                originalMapImage.Dispose();
                originalMapImage = null;
            }

            string szSelectName = this.comboBox1.SelectedItem.ToString();
            if (szSelectName.Contains("-"))
                szSelectName = szSelectName.Split('-')[0].Trim();
            L1MapHelper.doPaintEvent(szSelectName, this);

            // 等待地圖繪製完成後更新小地圖
            Application.DoEvents();
            UpdateMiniMap();
        }

        // 更新小地圖
        private void UpdateMiniMap()
        {
            try
            {
                if (this.pictureBox1.Image == null)
                    return;

                int miniWidth = 260;
                int miniHeight = 260;

                Bitmap miniMap = new Bitmap(miniWidth, miniHeight);
                using (Graphics g = Graphics.FromImage(miniMap))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                    float scaleX = (float)miniWidth / this.pictureBox1.Image.Width;
                    float scaleY = (float)miniHeight / this.pictureBox1.Image.Height;
                    float scale = Math.Min(scaleX, scaleY);

                    int scaledWidth = (int)(this.pictureBox1.Image.Width * scale);
                    int scaledHeight = (int)(this.pictureBox1.Image.Height * scale);
                    int offsetX = (miniWidth - scaledWidth) / 2;
                    int offsetY = (miniHeight - scaledHeight) / 2;

                    g.FillRectangle(Brushes.Black, 0, 0, miniWidth, miniHeight);
                    g.DrawImage(this.pictureBox1.Image, offsetX, offsetY, scaledWidth, scaledHeight);

                    if (this.panel1.Width > 0 && this.panel1.Height > 0 && this.pictureBox1.Width > 0 && this.pictureBox1.Height > 0)
                    {
                        float viewPortScaleX = (float)scaledWidth / this.pictureBox1.Width;
                        float viewPortScaleY = (float)scaledHeight / this.pictureBox1.Height;

                        int viewX = (int)(this.hScrollBar1.Value * viewPortScaleX) + offsetX;
                        int viewY = (int)(this.vScrollBar1.Value * viewPortScaleY) + offsetY;
                        int viewWidth = (int)(this.panel1.Width * viewPortScaleX);
                        int viewHeight = (int)(this.panel1.Height * viewPortScaleY);

                        using (Pen viewPortPen = new Pen(Color.Red, 2))
                        {
                            g.DrawRectangle(viewPortPen, viewX, viewY, viewWidth, viewHeight);
                        }
                    }
                }

                if (this.miniMapPictureBox.Image != null)
                    this.miniMapPictureBox.Image.Dispose();
                this.miniMapPictureBox.Image = miniMap;
            }
            catch
            {
                // 忽略錯誤
            }
        }

        public void vScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            this.pictureBox1.Top = -this.vScrollBar1.Value;
            if (!this.isMouseDrag)
                UpdateMiniMap();
        }

        public void hScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            this.pictureBox1.Left = -this.hScrollBar1.Value;
            if (!this.isMouseDrag)
                UpdateMiniMap();
        }

        private void pictureBox2_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.mouseDownPoint = Cursor.Position;
                this.isMouseDrag = true;
                this.Cursor = Cursors.Hand;
            }
            else if (e.Button == MouseButtons.Right)
            {
                L1MapHelper.doLocTagEvent(e, this);
            }
        }

        private void pictureBox2_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            this.Cursor = Cursors.Default;

            if (this.isMouseDrag)
            {
                int dragDistance = Math.Abs(Cursor.Position.X - this.mouseDownPoint.X) +
                                  Math.Abs(Cursor.Position.Y - this.mouseDownPoint.Y);

                if (dragDistance < DRAG_THRESHOLD)
                {
                    int adjustedX = (int)(e.X / this.zoomLevel);
                    int adjustedY = (int)(e.Y / this.zoomLevel);
                    var linLoc = L1MapHelper.GetLinLocation(adjustedX, adjustedY);
                    if (linLoc != null)
                    {
                        ShowSinglePoint(linLoc.x, linLoc.y);
                    }
                }

                UpdateMiniMap();
                this.isMouseDrag = false;
            }
        }

        private void pictureBox2_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.isMouseDrag)
            {
                try
                {
                    int deltaX = Cursor.Position.X - this.mouseDownPoint.X;
                    int deltaY = Cursor.Position.Y - this.mouseDownPoint.Y;

                    int newScrollX = this.hScrollBar1.Value - deltaX;
                    int newScrollY = this.vScrollBar1.Value - deltaY;

                    if (this.hScrollBar1.Maximum > 0)
                    {
                        newScrollX = Math.Max(this.hScrollBar1.Minimum, Math.Min(newScrollX, this.hScrollBar1.Maximum));
                        this.hScrollBar1.Value = newScrollX;
                    }

                    if (this.vScrollBar1.Maximum > 0)
                    {
                        newScrollY = Math.Max(this.vScrollBar1.Minimum, Math.Min(newScrollY, this.vScrollBar1.Maximum));
                        this.vScrollBar1.Value = newScrollY;
                    }

                    this.vScrollBar1_Scroll(null, null);
                    this.hScrollBar1_Scroll(null, null);

                    this.mouseDownPoint = Cursor.Position;
                }
                catch
                {
                    // 忽略錯誤
                }
            }
            else
            {
                L1MapHelper.doMouseMoveEvent(e, this);
            }
        }

        private void pictureBox2_Paint(object sender, PaintEventArgs e)
        {
            // 可以在這裡繪製額外的標記
        }

        // 滑鼠滾輪縮放地圖
        private void Panel1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys != Keys.Control)
                return;

            if (this.pictureBox1.Image == null)
                return;

            double oldZoom = zoomLevel;
            if (e.Delta > 0)
            {
                zoomLevel = Math.Min(ZOOM_MAX, zoomLevel + ZOOM_STEP);
            }
            else
            {
                zoomLevel = Math.Max(ZOOM_MIN, zoomLevel - ZOOM_STEP);
            }

            if (Math.Abs(oldZoom - zoomLevel) < 0.001)
                return;

            if (originalMapImage == null)
            {
                originalMapImage = (Image)this.pictureBox1.Image.Clone();
            }

            this.panel1.SuspendLayout();

            try
            {
                Point mousePos = this.panel1.PointToClient(Cursor.Position);

                double xRatio = (double)(mousePos.X + this.hScrollBar1.Value) / this.pictureBox1.Width;
                double yRatio = (double)(mousePos.Y + this.vScrollBar1.Value) / this.pictureBox1.Height;

                int newWidth = (int)(originalMapImage.Width * zoomLevel);
                int newHeight = (int)(originalMapImage.Height * zoomLevel);

                this.pictureBox1.Size = new Size(newWidth, newHeight);
                this.pictureBox2.Size = new Size(newWidth, newHeight);
                this.pictureBox3.Size = new Size(newWidth, newHeight);
                this.pictureBox4.Size = new Size(newWidth, newHeight);

                this.hScrollBar1.Maximum = Math.Max(0, newWidth);
                this.vScrollBar1.Maximum = Math.Max(0, newHeight);

                int newScrollX = (int)(newWidth * xRatio - mousePos.X);
                int newScrollY = (int)(newHeight * yRatio - mousePos.Y);

                this.hScrollBar1.Value = Math.Max(0, Math.Min(this.hScrollBar1.Maximum - this.panel1.Width, newScrollX));
                this.vScrollBar1.Value = Math.Max(0, Math.Min(this.vScrollBar1.Maximum - this.panel1.Height, newScrollY));

                this.vScrollBar1_Scroll(null, null);
                this.hScrollBar1_Scroll(null, null);
            }
            finally
            {
                this.panel1.ResumeLayout();
            }

            this.panel1.Invalidate();
        }

        // 顯示單點座標
        private void ShowSinglePoint(int x, int y)
        {
            string coords = string.Format("{0},{1}", x, y);
            this.toolStripStatusLabel2.Text = coords;

            try
            {
                Clipboard.SetText(coords);
                this.toolStripStatusLabel1.Text = "已複製: " + coords;
            }
            catch
            {
                this.toolStripStatusLabel1.Text = coords;
            }
        }

        // 小地圖點擊跳轉
        private void miniMapPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                if (this.pictureBox1.Image == null)
                    return;

                int miniWidth = 260;
                int miniHeight = 260;

                float scaleX = (float)miniWidth / this.pictureBox1.Image.Width;
                float scaleY = (float)miniHeight / this.pictureBox1.Image.Height;
                float scale = Math.Min(scaleX, scaleY);

                int scaledWidth = (int)(this.pictureBox1.Image.Width * scale);
                int scaledHeight = (int)(this.pictureBox1.Image.Height * scale);
                int offsetX = (miniWidth - scaledWidth) / 2;
                int offsetY = (miniHeight - scaledHeight) / 2;

                int clickX = e.X - offsetX;
                int clickY = e.Y - offsetY;

                if (clickX < 0 || clickY < 0 || clickX > scaledWidth || clickY > scaledHeight)
                    return;

                float clickRatioX = (float)clickX / scaledWidth;
                float clickRatioY = (float)clickY / scaledHeight;

                int newScrollX = (int)(clickRatioX * this.pictureBox1.Image.Width) - this.panel1.Width / 2;
                int newScrollY = (int)(clickRatioY * this.pictureBox1.Image.Height) - this.panel1.Height / 2;

                newScrollX = Math.Max(this.hScrollBar1.Minimum, Math.Min(newScrollX, this.hScrollBar1.Maximum));
                newScrollY = Math.Max(this.vScrollBar1.Minimum, Math.Min(newScrollY, this.vScrollBar1.Maximum));

                this.hScrollBar1.Value = newScrollX;
                this.vScrollBar1.Value = newScrollY;
                this.hScrollBar1_Scroll(null, null);
                this.vScrollBar1_Scroll(null, null);
            }
            catch
            {
                // 忽略錯誤
            }
        }
    }
}
