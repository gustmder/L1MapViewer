using System.Windows.Forms;

namespace L1FlyMapViewer
{
    /// <summary>
    /// 地圖檢視器介面，讓不同的 Form 都能使用地圖相關功能
    /// </summary>
    public interface IMapViewer
    {
        ComboBox comboBox1 { get; }
        PictureBox pictureBox1 { get; }
        PictureBox pictureBox2 { get; }
        PictureBox pictureBox3 { get; }
        PictureBox pictureBox4 { get; }
        VScrollBar vScrollBar1 { get; }
        HScrollBar hScrollBar1 { get; }
        ToolStripProgressBar toolStripProgressBar1 { get; }
        ToolStripStatusLabel toolStripStatusLabel1 { get; }
        ToolStripStatusLabel toolStripStatusLabel2 { get; }
        ToolStripStatusLabel toolStripStatusLabel3 { get; }
        Panel panel1 { get; }
        double zoomLevel { get; set; }

        void vScrollBar1_Scroll(object sender, ScrollEventArgs e);
        void hScrollBar1_Scroll(object sender, ScrollEventArgs e);
        void Refresh();
        void Invalidate();
    }
}
