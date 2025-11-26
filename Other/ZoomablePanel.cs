using System.Windows.Forms;

namespace L1MapViewer.Other
{
    /// <summary>
    /// 支援縮放功能的自訂 Panel，可阻止滾輪事件在縮放時觸發滾動
    /// </summary>
    public class ZoomablePanel : Panel
    {
        public ZoomablePanel()
        {
            // 啟用滑鼠滾輪事件
            this.SetStyle(ControlStyles.Selectable, true);

            // 啟用雙緩衝和優化渲染
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // 先觸發事件處理器（讓 MouseWheel 事件能被處理）
            base.OnMouseWheel(e);

            // 如果按住 Ctrl 鍵，標記為已處理，阻止滾動
            if (Control.ModifierKeys == Keys.Control)
            {
                if (e is HandledMouseEventArgs handledArgs)
                {
                    handledArgs.Handled = true;
                }
            }
        }

        protected override void OnEnter(System.EventArgs e)
        {
            // 當滑鼠進入時取得焦點，確保可以接收滾輪事件
            this.Focus();
            base.OnEnter(e);
        }
    }
}
