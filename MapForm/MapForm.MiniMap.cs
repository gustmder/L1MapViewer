using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;
using L1MapViewer;
using L1MapViewer.Helper;
using L1MapViewer.Models;

namespace L1FlyMapViewer
{
    /// <summary>
    /// MapForm - 小地圖操作
    /// </summary>
    public partial class MapForm
    {
        /// <summary>
        /// 更新小地圖（如果沒有快取則背景渲染，否則只更新紅框）
        /// </summary>
        private void UpdateMiniMap()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                int mapWidth = _viewState.MapWidth;
                int mapHeight = _viewState.MapHeight;

                if (mapWidth <= 0 || mapHeight <= 0)
                    return;

                // 如果沒有快取且沒有在渲染中，啟動背景渲染
                if (_renderCache.MiniMapFullBitmap == null && !_miniMapRendering)
                {
                    LogPerf($"[MINIMAP-UPDATE] starting full render (no cache)");
                    // 先顯示一個「渲染中」的佔位圖
                    ShowMiniMapPlaceholder();
                    RenderMiniMapFullAsync();
                    return;
                }

                // 更新紅框顯示
                UpdateMiniMapRedBox();
                sw.Stop();
                if (sw.ElapsedMilliseconds > 5)
                {
                    LogPerf($"[MINIMAP-UPDATE] redbox only, took {sw.ElapsedMilliseconds}ms");
                }
            }
            catch
            {
                // 忽略錯誤
            }
        }

        /// <summary>
        /// 顯示小地圖佔位圖（渲染中提示）
        /// </summary>
        private void ShowMiniMapPlaceholder()
        {
            Bitmap placeholder = new Bitmap(MINIMAP_SIZE, MINIMAP_SIZE);
            using (Graphics g = Graphics.FromImage(placeholder))
            {
                g.Clear(Color.FromArgb(30, 30, 30));
                using (var font = new Font("Microsoft JhengHei", 12))
                using (var brush = new SolidBrush(Color.Gray))
                {
                    string text = "小地圖繪製中...";
                    var size = g.MeasureString(text, font);
                    g.DrawString(text, font, brush,
                        (MINIMAP_SIZE - size.Width) / 2,
                        (MINIMAP_SIZE - size.Height) / 2);
                }
            }
            miniMapPictureBox.Image?.Dispose();
            miniMapPictureBox.Image = placeholder;
        }

        /// <summary>
        /// 背景渲染完整的小地圖（使用共用的 MiniMapRenderer）
        /// </summary>
        private void RenderMiniMapFullAsync()
        {
            if (string.IsNullOrEmpty(_document.MapId) || !Share.MapDataList.ContainsKey(_document.MapId))
                return;

            int mapWidth = _viewState.MapWidth;
            int mapHeight = _viewState.MapHeight;
            if (mapWidth <= 0 || mapHeight <= 0)
                return;

            // 標記正在渲染
            _miniMapRendering = true;

            // 計算縮放比例（在 UI 執行緒計算並快取）
            float scale = Math.Min((float)MINIMAP_SIZE / mapWidth, (float)MINIMAP_SIZE / mapHeight);
            int scaledWidth = (int)(mapWidth * scale);
            int scaledHeight = (int)(mapHeight * scale);

            _miniMapScale = scale;
            _miniMapOffsetX = (MINIMAP_SIZE - scaledWidth) / 2;
            _miniMapOffsetY = (MINIMAP_SIZE - scaledHeight) / 2;

            // 複製需要的資料（避免跨執行緒存取）
            var s32FilesSnapshot = _document.S32Files.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 建立勾選的 S32 檔案清單
            HashSet<string> checkedFilePaths = new HashSet<string>();
            for (int i = 0; i < lstS32Files.Items.Count; i++)
            {
                if (lstS32Files.GetItemChecked(i) && lstS32Files.Items[i] is S32FileItem item)
                {
                    checkedFilePaths.Add(item.FilePath);
                }
            }

            int s32Count = checkedFilePaths.Count;

            // 背景執行緒渲染（使用共用的 MiniMapRenderer）
            Task.Run(() =>
            {
                try
                {
                    MiniMapRenderer.RenderStats stats;
                    Bitmap miniBitmap = _miniMapRenderer.RenderMiniMap(
                        mapWidth, mapHeight, MINIMAP_SIZE,
                        s32FilesSnapshot, checkedFilePaths,
                        out stats);

                    string mode = stats.IsSimplified ? "simplified" : "full";
                    LogPerf($"[MINIMAP] total={stats.TotalMs}ms | blocks={s32Count}, size={stats.ScaledWidth}x{stats.ScaledHeight}, mode={mode}");

                    // 回到 UI 執行緒更新
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        lock (_miniMapLock)
                        {
                            if (_renderCache.MiniMapFullBitmap != null)
                                _renderCache.MiniMapFullBitmap.Dispose();
                            _renderCache.MiniMapFullBitmap = miniBitmap;
                        }
                        _miniMapRendering = false;

                        // 渲染完成，更新顯示
                        UpdateMiniMapRedBox();
                    });
                }
                catch
                {
                    _miniMapRendering = false;
                }
            });
        }

        /// <summary>
        /// 只更新小地圖紅框位置（不重新渲染底圖）
        /// </summary>
        private void UpdateMiniMapRedBox()
        {
            lock (_miniMapLock)
            {
                if (_renderCache.MiniMapFullBitmap == null)
                    return;

                // 建立顯示圖（底圖 + 紅框）
                Bitmap displayBitmap = new Bitmap(MINIMAP_SIZE, MINIMAP_SIZE);
                using (Graphics g = Graphics.FromImage(displayBitmap))
                {
                    g.Clear(Color.Black);

                    // 繪製小地圖底圖（置中）
                    g.DrawImage(_renderCache.MiniMapFullBitmap, _miniMapOffsetX, _miniMapOffsetY);

                    // 繪製視窗位置紅框
                    if (s32MapPanel.Width > 0 && s32MapPanel.Height > 0)
                    {
                        int scrollX = _viewState.ScrollX;
                        int scrollY = _viewState.ScrollY;
                        int viewportWidthWorld = (int)(s32MapPanel.Width / s32ZoomLevel);
                        int viewportHeightWorld = (int)(s32MapPanel.Height / s32ZoomLevel);

                        int viewX = (int)(scrollX * _miniMapScale) + _miniMapOffsetX;
                        int viewY = (int)(scrollY * _miniMapScale) + _miniMapOffsetY;
                        int viewWidth = (int)(viewportWidthWorld * _miniMapScale);
                        int viewHeight = (int)(viewportHeightWorld * _miniMapScale);

                        using (Pen viewPortPen = new Pen(Color.Red, 2))
                        {
                            g.DrawRectangle(viewPortPen, viewX, viewY, viewWidth, viewHeight);
                        }
                    }
                }

                if (miniMapPictureBox.Image != null)
                    miniMapPictureBox.Image.Dispose();
                miniMapPictureBox.Image = displayBitmap;
            }
        }

        /// <summary>
        /// 清除小地圖快取（地圖變更時呼叫）
        /// </summary>
        private void ClearMiniMapCache()
        {
            lock (_miniMapLock)
            {
                if (_renderCache.MiniMapFullBitmap != null)
                {
                    _renderCache.MiniMapFullBitmap.Dispose();
                    _renderCache.MiniMapFullBitmap = null;
                }
            }
        }

        // 小地圖滑鼠按下 - 開始拖拽或點擊跳轉
        private void miniMapPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            // 設定小地圖焦點標記，讓 Form 的 KeyDown 處理方向鍵
            _interaction.IsMiniMapFocused = true;

            if (e.Button == MouseButtons.Left)
            {
                _interaction.StartMiniMapDrag();
                MoveMainMapFromMiniMap(e.X, e.Y, true);
            }
        }

        // 小地圖滑鼠移動 - 拖拽時只更新小地圖紅框
        private void miniMapPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (_interaction.IsMiniMapDragging && e.Button == MouseButtons.Left)
            {
                MoveMainMapFromMiniMap(e.X, e.Y, false);  // 拖拽中不重繪主地圖
            }
        }

        // 小地圖滑鼠放開 - 結束拖拽，更新主地圖
        private void miniMapPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (_interaction.IsMiniMapDragging)
            {
                _interaction.EndDrag();
                // 拖拽結束時更新小地圖（主地圖已經捲動到正確位置）
                UpdateMiniMap();
            }
        }

        // 根據小地圖座標移動主地圖（點擊位置為紅框中心）
        // updateMiniMapFlag: true=更新小地圖, false=只更新紅框
        private void MoveMainMapFromMiniMap(int mouseX, int mouseY, bool updateMiniMapFlag)
        {
            try
            {
                // 使用 ViewState 的地圖大小
                int pictureWidth = _viewState.MapWidth > 0 ? _viewState.MapWidth : this._mapViewerControl.Width;
                int pictureHeight = _viewState.MapHeight > 0 ? _viewState.MapHeight : this._mapViewerControl.Height;

                if (pictureWidth <= 0 || pictureHeight <= 0)
                    return;

                int miniWidth = MINIMAP_SIZE;
                int miniHeight = MINIMAP_SIZE;

                // 計算小地圖中圖片的縮放和偏移（與 UpdateMiniMap 一致）
                float scaleX = (float)miniWidth / pictureWidth;
                float scaleY = (float)miniHeight / pictureHeight;
                float scale = Math.Min(scaleX, scaleY);

                int scaledWidth = (int)(pictureWidth * scale);
                int scaledHeight = (int)(pictureHeight * scale);
                int offsetX = (miniWidth - scaledWidth) / 2;
                int offsetY = (miniHeight - scaledHeight) / 2;

                // 計算點擊在縮放圖片中的相對位置
                int clickX = mouseX - offsetX;
                int clickY = mouseY - offsetY;

                // 限制在有效範圍內
                clickX = Math.Max(0, Math.Min(clickX, scaledWidth));
                clickY = Math.Max(0, Math.Min(clickY, scaledHeight));

                // 計算點擊位置對應的主地圖世界座標
                int mapPosX = (int)((float)clickX / scaledWidth * pictureWidth);
                int mapPosY = (int)((float)clickY / scaledHeight * pictureHeight);

                // 計算捲動位置（世界座標），讓點擊位置成為視窗中央
                int viewportWidthWorld = (int)(s32MapPanel.Width / s32ZoomLevel);
                int viewportHeightWorld = (int)(s32MapPanel.Height / s32ZoomLevel);
                int newScrollX = mapPosX - viewportWidthWorld / 2;
                int newScrollY = mapPosY - viewportHeightWorld / 2;

                // 限制在有效範圍內（世界座標）
                int maxScrollX = Math.Max(0, pictureWidth - viewportWidthWorld);
                int maxScrollY = Math.Max(0, pictureHeight - viewportHeightWorld);
                newScrollX = Math.Max(0, Math.Min(newScrollX, maxScrollX));
                newScrollY = Math.Max(0, Math.Min(newScrollY, maxScrollY));

                // 設定 ViewState 的捲動位置
                _viewState.SetScrollSilent(newScrollX, newScrollY);

                // 根據參數決定是否更新小地圖和重新渲染
                if (updateMiniMapFlag)
                {
                    CheckAndRerenderIfNeeded();
                    UpdateMiniMap();
                }
                else
                {
                    // 拖拽時只更新小地圖紅框位置和重繪（快速繪製）
                    _mapViewerControl.Refresh();
                    UpdateMiniMapRedBox();
                }
            }
            catch
            {
                // 忽略錯誤
            }
        }

        // 小地圖點擊跳轉（保留給滑鼠右鍵查詢 S32 檔案用）
        private void miniMapPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            // 右鍵點擊顯示 S32 檔案資訊
            if (e.Button == MouseButtons.Right)
            {
                try
                {
                    // 使用 ViewState 的地圖大小
                    int pictureWidth = _viewState.MapWidth > 0 ? _viewState.MapWidth : this._mapViewerControl.Width;
                    int pictureHeight = _viewState.MapHeight > 0 ? _viewState.MapHeight : this._mapViewerControl.Height;

                    if (pictureWidth <= 0 || pictureHeight <= 0)
                        return;

                    int miniWidth = MINIMAP_SIZE;
                    int miniHeight = MINIMAP_SIZE;

                    float scaleX = (float)miniWidth / pictureWidth;
                    float scaleY = (float)miniHeight / pictureHeight;
                    float scale = Math.Min(scaleX, scaleY);

                    int scaledWidth = (int)(pictureWidth * scale);
                    int scaledHeight = (int)(pictureHeight * scale);
                    int offsetX = (miniWidth - scaledWidth) / 2;
                    int offsetY = (miniHeight - scaledHeight) / 2;

                    int clickX = e.X - offsetX;
                    int clickY = e.Y - offsetY;

                    if (clickX < 0 || clickY < 0 || clickX > scaledWidth || clickY > scaledHeight)
                        return;

                    float clickRatioX = (float)clickX / scaledWidth;
                    float clickRatioY = (float)clickY / scaledHeight;

                    int mapX = (int)(clickRatioX * pictureWidth);
                    int mapY = (int)(clickRatioY * pictureHeight);

                    var linLoc = L1MapHelper.GetLinLocation(mapX, mapY);
                    if (linLoc != null)
                    {
                        int blockX = ((linLoc.x - 0x7FFF) / 64) + 0x7FFF;
                        int blockY = ((linLoc.y - 0x7FFF) / 64) + 0x7FFF;
                        string targetFileName = $"{blockX:X4}{blockY:X4}.s32";
                        this.toolStripStatusLabel1.Text = $"S32 檔案: {targetFileName} (座標: {linLoc.x},{linLoc.y})";
                    }
                }
                catch { }
            }
        }

        // 小地圖 PreviewKeyDown - 保留給未來使用（目前由 Form KeyDown 處理）
        private void miniMapPictureBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
        }

        // 小地圖鍵盤事件 - 保留給未來使用（目前由 Form KeyDown 處理）
        private void miniMapPictureBox_KeyDown(object sender, KeyEventArgs e)
        {
        }

        // 方向鍵移動小地圖視圖
        private void MoveMiniMapByArrowKey(Keys keyCode)
        {
            if (_viewState.MapWidth <= 0 || _viewState.MapHeight <= 0)
                return;

            // 計算移動量（移動一個 viewport 的大小，考慮縮放）
            var viewport = _viewState.GetViewportWorldRect();
            int moveX = 0, moveY = 0;

            switch (keyCode)
            {
                case Keys.Up:
                    moveY = -viewport.Height;
                    break;
                case Keys.Down:
                    moveY = viewport.Height;
                    break;
                case Keys.Left:
                    moveX = -viewport.Width;
                    break;
                case Keys.Right:
                    moveX = viewport.Width;
                    break;
                default:
                    return;
            }

            // 計算新的捲動位置
            int newScrollX = _viewState.ScrollX + moveX;
            int newScrollY = _viewState.ScrollY + moveY;

            // 限制在地圖範圍內
            newScrollX = Math.Max(0, Math.Min(newScrollX, _viewState.MaxScrollX));
            newScrollY = Math.Max(0, Math.Min(newScrollY, _viewState.MaxScrollY));

            // 更新捲動位置
            _viewState.SetScrollSilent(newScrollX, newScrollY);

            // 重新渲染並更新小地圖
            RenderS32Map();
            UpdateMiniMap();
        }
    }
}
