using System;
using System.Collections.Generic;
using System.Diagnostics;
using L1MapViewer.Models;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer.Helper
{
    /// <summary>
    /// Layer4 物件的空間索引（Grid-based Spatial Hash）
    /// 將地圖分割成網格，每個網格儲存該區域內的物件參照
    /// </summary>
    public class Layer4SpatialIndex
    {
        // 網格大小（遊戲座標單位）- 應該大於等於搜尋半徑
        private const int GridSize = 16;

        // 網格資料：key = (gridX, gridY), value = 該網格內的物件列表
        private readonly Dictionary<(int, int), List<Layer4Entry>> _grid;

        // 統計資料
        public int TotalObjects { get; private set; }
        public int GridCellCount => _grid.Count;
        public long BuildTimeMs { get; private set; }

        /// <summary>
        /// Layer4 物件項目
        /// </summary>
        public class Layer4Entry
        {
            public S32Data S32Data;
            public ObjectTile Object;
            public int GameX;  // 遊戲座標 X
            public int GameY;  // 遊戲座標 Y
        }

        public Layer4SpatialIndex()
        {
            _grid = new Dictionary<(int, int), List<Layer4Entry>>();
        }

        /// <summary>
        /// 從 S32 檔案建立空間索引
        /// </summary>
        public void Build(IEnumerable<S32Data> s32Files)
        {
            var sw = Stopwatch.StartNew();
            _grid.Clear();
            TotalObjects = 0;

            foreach (var s32Data in s32Files)
            {
                int segStartX = s32Data.SegInfo.nLinBeginX;
                int segStartY = s32Data.SegInfo.nLinBeginY;

                foreach (var obj in s32Data.Layer4)
                {
                    // 計算遊戲座標
                    int gameX = segStartX + obj.X / 2;
                    int gameY = segStartY + obj.Y;

                    // 計算網格座標
                    int gridX = gameX / GridSize;
                    int gridY = gameY / GridSize;

                    var key = (gridX, gridY);
                    if (!_grid.TryGetValue(key, out var list))
                    {
                        list = new List<Layer4Entry>();
                        _grid[key] = list;
                    }

                    list.Add(new Layer4Entry
                    {
                        S32Data = s32Data,
                        Object = obj,
                        GameX = gameX,
                        GameY = gameY
                    });

                    TotalObjects++;
                }
            }

            sw.Stop();
            BuildTimeMs = sw.ElapsedMilliseconds;
        }

        /// <summary>
        /// 查詢指定座標附近的物件（曼哈頓距離）
        /// </summary>
        public List<Layer4Entry> QueryNearby(int centerGameX, int centerGameY, int radius)
        {
            var result = new List<Layer4Entry>();

            // 計算需要檢查的網格範圍
            int minGridX = (centerGameX - radius) / GridSize;
            int maxGridX = (centerGameX + radius) / GridSize;
            int minGridY = (centerGameY - radius) / GridSize;
            int maxGridY = (centerGameY + radius) / GridSize;

            // 遍歷相關網格
            for (int gx = minGridX; gx <= maxGridX; gx++)
            {
                for (int gy = minGridY; gy <= maxGridY; gy++)
                {
                    var key = (gx, gy);
                    if (_grid.TryGetValue(key, out var list))
                    {
                        foreach (var entry in list)
                        {
                            // 計算曼哈頓距離
                            int distance = Math.Abs(entry.GameX - centerGameX) + Math.Abs(entry.GameY - centerGameY);
                            if (distance <= radius)
                            {
                                result.Add(entry);
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 收集附近的群組（與 UpdateNearbyGroupThumbnails 相同邏輯，但使用空間索引）
        /// </summary>
        public Dictionary<int, (int distance, List<(S32Data s32, ObjectTile obj)> objects)>
            CollectNearbyGroups(int centerGameX, int centerGameY, int radius)
        {
            var nearbyGroups = new Dictionary<int, (int distance, List<(S32Data s32, ObjectTile obj)> objects)>();

            var nearbyObjects = QueryNearby(centerGameX, centerGameY, radius);

            foreach (var entry in nearbyObjects)
            {
                int distance = Math.Abs(entry.GameX - centerGameX) + Math.Abs(entry.GameY - centerGameY);

                if (!nearbyGroups.TryGetValue(entry.Object.GroupId, out var groupInfo))
                {
                    groupInfo = (distance, new List<(S32Data, ObjectTile)>());
                    nearbyGroups[entry.Object.GroupId] = groupInfo;
                }

                // 更新最小距離
                if (distance < groupInfo.distance)
                {
                    nearbyGroups[entry.Object.GroupId] = (distance, groupInfo.objects);
                }

                groupInfo.objects.Add((entry.S32Data, entry.Object));
            }

            return nearbyGroups;
        }

        /// <summary>
        /// 清除索引
        /// </summary>
        public void Clear()
        {
            _grid.Clear();
            TotalObjects = 0;
        }
    }
}
