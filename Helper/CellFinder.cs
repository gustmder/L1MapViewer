using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using L1MapViewer.Models;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// 格子查找器 - 根據世界座標找到對應的 S32 和格子
    /// </summary>
    public static class CellFinder
    {
        /// <summary>
        /// 查找結果
        /// </summary>
        public class FindResult
        {
            public S32Data S32Data { get; set; }
            public int CellX { get; set; }
            public int CellY { get; set; }
            public bool Found { get; set; }
            public long ElapsedMs { get; set; }
            public int S32Checked { get; set; }
            public int CellsChecked { get; set; }
        }

        /// <summary>
        /// 根據世界座標找到對應的格子（暴力搜尋法）
        /// </summary>
        public static FindResult FindCellBruteForce(int worldX, int worldY, IEnumerable<S32Data> s32Files)
        {
            var sw = Stopwatch.StartNew();
            var result = new FindResult { Found = false };

            foreach (var s32Data in s32Files)
            {
                result.S32Checked++;

                // 使用 GetLoc 計算區塊位置
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 遍歷該 S32 的所有格子
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        result.CellsChecked++;

                        // 使用 GetLoc + drawTilBlock 公式計算像素位置
                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        // 檢查點擊位置是否在這個菱形內（使用數學公式而非 GDI+）
                        if (IsPointInDiamond(worldX, worldY, X, Y, 24, 24))
                        {
                            result.Found = true;
                            result.S32Data = s32Data;
                            result.CellX = x;
                            result.CellY = y;
                            sw.Stop();
                            result.ElapsedMs = sw.ElapsedMilliseconds;
                            return result;
                        }
                    }
                }
            }

            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
            return result;
        }

        /// <summary>
        /// 根據世界座標找到對應的格子（優化版：先過濾 S32 範圍）
        /// </summary>
        public static FindResult FindCellOptimized(int worldX, int worldY, IEnumerable<S32Data> s32Files)
        {
            var sw = Stopwatch.StartNew();
            var result = new FindResult { Found = false };

            const int BlockWidth = 3072;  // 64 * 24 * 2
            const int BlockHeight = 1536; // 64 * 12 * 2

            foreach (var s32Data in s32Files)
            {
                result.S32Checked++;

                // 使用 GetLoc 計算區塊位置
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                int mx = loc[0];
                int my = loc[1];

                // 先檢查點擊位置是否在這個 S32 的大致範圍內
                if (worldX < mx || worldX >= mx + BlockWidth ||
                    worldY < my || worldY >= my + BlockHeight)
                {
                    continue; // 跳過不在範圍內的 S32
                }

                // 遍歷該 S32 的所有格子
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        result.CellsChecked++;

                        // 使用 GetLoc + drawTilBlock 公式計算像素位置
                        int localBaseX = 0;
                        int localBaseY = 63 * 12;
                        localBaseX -= 24 * (x / 2);
                        localBaseY -= 12 * (x / 2);

                        int X = mx + localBaseX + x * 24 + y * 24;
                        int Y = my + localBaseY + y * 12;

                        // 檢查點擊位置是否在這個菱形內
                        if (IsPointInDiamond(worldX, worldY, X, Y, 24, 24))
                        {
                            result.Found = true;
                            result.S32Data = s32Data;
                            result.CellX = x;
                            result.CellY = y;
                            sw.Stop();
                            result.ElapsedMs = sw.ElapsedMilliseconds;
                            return result;
                        }
                    }
                }
            }

            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
            return result;
        }

        /// <summary>
        /// 檢查點是否在菱形內（純數學計算，不使用 GDI+）
        /// 菱形的四個頂點：(X, Y+12), (X+12, Y), (X+24, Y+12), (X+12, Y+24)
        /// </summary>
        private static bool IsPointInDiamond(int px, int py, int diamondX, int diamondY, int width, int height)
        {
            // 菱形中心點
            int centerX = diamondX + width / 2;  // X + 12
            int centerY = diamondY + height / 2; // Y + 12

            // 將點轉換為相對於中心的座標
            int dx = Math.Abs(px - centerX);
            int dy = Math.Abs(py - centerY);

            // 菱形的半寬和半高
            int halfWidth = width / 2;   // 12
            int halfHeight = height / 2; // 12

            // 菱形內的點滿足: |dx|/halfWidth + |dy|/halfHeight <= 1
            // 即: dx * halfHeight + dy * halfWidth <= halfWidth * halfHeight
            return dx * halfHeight + dy * halfWidth <= halfWidth * halfHeight;
        }

        /// <summary>
        /// 檢查點是否在菱形內（使用 GDI+ Region，相容舊版）
        /// </summary>
        public static bool IsPointInDiamondGdi(Point p, Point p1, Point p2, Point p3, Point p4)
        {
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddPolygon(new Point[] { p1, p2, p3, p4 });
                using (var region = new System.Drawing.Region(path))
                {
                    return region.IsVisible(p);
                }
            }
        }
    }
}
