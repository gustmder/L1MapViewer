using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using L1FlyMapViewer;
using L1MapViewer.Helper;
using L1MapViewer.Models;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer.CLI.Commands
{
    /// <summary>
    /// 效能測試指令
    /// </summary>
    public static class BenchmarkCommands
    {
        /// <summary>
        /// 格子查找效能測試
        /// </summary>
        public static int CellFind(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-cellfind <map_path> [--runs N] [--optimized]");
                Console.WriteLine("範例: benchmark-cellfind C:\\client\\map\\4");
                Console.WriteLine("      benchmark-cellfind C:\\client\\map\\4 --optimized");
                Console.WriteLine();
                Console.WriteLine("測試從世界座標查找對應格子的效能");
                return 1;
            }

            string mapPath = args[0];
            int runs = 3;
            bool useOptimized = false;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--runs" && i + 1 < args.Length)
                    int.TryParse(args[++i], out runs);
                else if (args[i] == "--optimized")
                    useOptimized = true;
            }

            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success) return 1;

            Console.WriteLine();
            Console.WriteLine($"Runs: {runs}");
            Console.WriteLine($"Method: {(useOptimized ? "Optimized" : "BruteForce")}");
            Console.WriteLine();

            // 產生隨機測試點
            var random = new Random(42);
            int testPointCount = 20;
            var testPoints = new List<(int x, int y)>();

            for (int i = 0; i < testPointCount; i++)
            {
                int x = loadResult.MinX + random.Next(loadResult.MapWidth);
                int y = loadResult.MinY + random.Next(loadResult.MapHeight);
                testPoints.Add((x, y));
            }

            Console.WriteLine($"Test Points: {testPointCount}");
            Console.WriteLine();

            var allTimes = new List<long>();
            var totalCellsChecked = new List<int>();
            var totalS32Checked = new List<int>();
            int foundCount = 0;

            var sw = new Stopwatch();

            for (int run = 1; run <= runs; run++)
            {
                Console.WriteLine($"--- Run {run}/{runs} ---");

                int runCellsChecked = 0;
                int runS32Checked = 0;
                int runFound = 0;

                sw.Restart();

                foreach (var (x, y) in testPoints)
                {
                    CellFinder.FindResult result;
                    if (useOptimized)
                        result = CellFinder.FindCellOptimized(x, y, loadResult.S32Files.Values);
                    else
                        result = CellFinder.FindCellBruteForce(x, y, loadResult.S32Files.Values);

                    runCellsChecked += result.CellsChecked;
                    runS32Checked += result.S32Checked;
                    if (result.Found) runFound++;
                }

                sw.Stop();
                allTimes.Add(sw.ElapsedMilliseconds);
                totalCellsChecked.Add(runCellsChecked);
                totalS32Checked.Add(runS32Checked);
                if (run == 1) foundCount = runFound;

                Console.WriteLine($"  Time: {sw.ElapsedMilliseconds} ms");
                Console.WriteLine($"  S32 Checked: {runS32Checked}");
                Console.WriteLine($"  Cells Checked: {runCellsChecked:N0}");
                Console.WriteLine($"  Found: {runFound}/{testPointCount}");
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            Console.WriteLine($"Average Time: {allTimes.Average():F1} ms");
            Console.WriteLine($"Min: {allTimes.Min()} ms, Max: {allTimes.Max()} ms");
            Console.WriteLine($"Avg S32 Checked: {totalS32Checked.Average():F0}");
            Console.WriteLine($"Avg Cells Checked: {totalCellsChecked.Average():F0}");
            Console.WriteLine($"Found Rate: {foundCount}/{testPointCount} ({100.0 * foundCount / testPointCount:F1}%)");

            return 0;
        }

        /// <summary>
        /// 模擬完整 MouseClick 流程（格子查找 + 渲染）
        /// </summary>
        public static int MouseClick(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-mouseclick <map_path> [--runs N]");
                Console.WriteLine("範例: benchmark-mouseclick C:\\client\\map\\4");
                Console.WriteLine();
                Console.WriteLine("模擬完整的滑鼠點擊流程：格子查找 + Viewport 渲染");
                return 1;
            }

            string mapPath = args[0];
            int runs = 5;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--runs" && i + 1 < args.Length)
                    int.TryParse(args[++i], out runs);
            }

            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success) return 1;

            Console.WriteLine();
            Console.WriteLine($"Runs: {runs}");

            int viewportWidth = 2048;
            int viewportHeight = 2048;

            var renderer = new ViewportRenderer();
            var checkedFiles = new HashSet<string>(loadResult.S32Files.Keys);

            var random = new Random(42);
            int testPointCount = 10;
            var testPoints = new List<(int x, int y)>();

            for (int i = 0; i < testPointCount; i++)
            {
                int x = loadResult.MinX + random.Next(loadResult.MapWidth);
                int y = loadResult.MinY + random.Next(loadResult.MapHeight);
                testPoints.Add((x, y));
            }

            Console.WriteLine($"Test Points: {testPointCount}");
            Console.WriteLine($"Viewport: {viewportWidth} x {viewportHeight}");
            Console.WriteLine();

            var allCellFindTimes = new List<long>();
            var allRenderTimes = new List<long>();
            var allTotalTimes = new List<long>();

            var sw = new Stopwatch();

            for (int run = 1; run <= runs; run++)
            {
                Console.WriteLine($"--- Run {run}/{runs} ---");

                long totalCellFind = 0;
                long totalRender = 0;
                int foundCount = 0;

                renderer.ClearCache();

                foreach (var (clickX, clickY) in testPoints)
                {
                    sw.Restart();
                    var findResult = CellFinder.FindCellOptimized(clickX, clickY, loadResult.S32Files.Values);
                    sw.Stop();
                    totalCellFind += sw.ElapsedMilliseconds;

                    if (findResult.Found)
                    {
                        foundCount++;

                        int scrollX = clickX - viewportWidth / 2;
                        int scrollY = clickY - viewportHeight / 2;
                        var worldRect = new Rectangle(scrollX, scrollY, viewportWidth, viewportHeight);

                        sw.Restart();
                        var stats = new ViewportRenderer.RenderStats();
                        using (var bmp = renderer.RenderViewport(worldRect, loadResult.S32Files, checkedFiles,
                            true, true, true, out stats))
                        {
                        }
                        sw.Stop();
                        totalRender += sw.ElapsedMilliseconds;
                    }
                }

                long runTotal = totalCellFind + totalRender;
                allCellFindTimes.Add(totalCellFind);
                allRenderTimes.Add(totalRender);
                allTotalTimes.Add(runTotal);

                Console.WriteLine($"  Cell Find:  {totalCellFind,5} ms ({(double)totalCellFind / testPointCount:F1} ms/click)");
                Console.WriteLine($"  Render:     {totalRender,5} ms ({(foundCount > 0 ? (double)totalRender / foundCount : 0):F1} ms/click)");
                Console.WriteLine($"  Total:      {runTotal,5} ms ({(double)runTotal / testPointCount:F1} ms/click)");
                Console.WriteLine($"  Found: {foundCount}/{testPointCount}");
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary (per click) ===");
            Console.WriteLine($"Cell Find Avg: {allCellFindTimes.Average() / testPointCount:F1} ms");
            Console.WriteLine($"Render Avg:    {allRenderTimes.Average() / testPointCount:F1} ms");
            Console.WriteLine($"Total Avg:     {allTotalTimes.Average() / testPointCount:F1} ms");
            Console.WriteLine();
            Console.WriteLine($"Min Total: {allTotalTimes.Min() / testPointCount:F1} ms/click");
            Console.WriteLine($"Max Total: {allTotalTimes.Max() / testPointCount:F1} ms/click");

            return 0;
        }

        /// <summary>
        /// 測試附近群組搜尋效能（UpdateNearbyGroupThumbnails 的核心邏輯）
        /// </summary>
        public static int NearbyGroups(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-nearbygroups <map_path> [--runs N] [--radius N]");
                Console.WriteLine("範例: benchmark-nearbygroups C:\\client\\map\\4");
                Console.WriteLine("      benchmark-nearbygroups C:\\client\\map\\4 --radius 20");
                Console.WriteLine();
                Console.WriteLine("測試 UpdateNearbyGroupThumbnails 的群組收集效能");
                return 1;
            }

            string mapPath = args[0];
            int runs = 5;
            int radius = 10;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--runs" && i + 1 < args.Length)
                    int.TryParse(args[++i], out runs);
                else if (args[i] == "--radius" && i + 1 < args.Length)
                    int.TryParse(args[++i], out radius);
            }

            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success) return 1;

            Console.WriteLine();
            Console.WriteLine($"Runs: {runs}");
            Console.WriteLine($"Radius: {radius}");

            // 統計 Layer4 物件總數
            int totalLayer4 = 0;
            int totalLayer5 = 0;
            foreach (var s32 in loadResult.S32Files.Values)
            {
                totalLayer4 += s32.Layer4.Count;
                totalLayer5 += s32.Layer5.Count;
            }
            Console.WriteLine($"Total Layer4 Objects: {totalLayer4:N0}");
            Console.WriteLine($"Total Layer5 Items: {totalLayer5:N0}");
            Console.WriteLine();

            // 產生隨機測試點（選擇有 Layer4 物件的格子）
            var random = new Random(42);
            int testPointCount = 10;
            var testCells = new List<(S32Data s32, int cellX, int cellY, int gameX, int gameY)>();

            // 收集所有有 Layer4 物件的格子
            var cellsWithLayer4 = new List<(S32Data s32, int cellX, int cellY, int gameX, int gameY)>();
            foreach (var s32 in loadResult.S32Files.Values)
            {
                foreach (var obj in s32.Layer4)
                {
                    int gameX = s32.SegInfo.nLinBeginX + obj.X / 2;
                    int gameY = s32.SegInfo.nLinBeginY + obj.Y;
                    cellsWithLayer4.Add((s32, obj.X, obj.Y, gameX, gameY));
                }
            }

            // 隨機選擇測試點
            for (int i = 0; i < Math.Min(testPointCount, cellsWithLayer4.Count); i++)
            {
                int idx = random.Next(cellsWithLayer4.Count);
                testCells.Add(cellsWithLayer4[idx]);
            }

            Console.WriteLine($"Test Points: {testCells.Count}");
            Console.WriteLine();

            var allCollectGroupsTimes = new List<long>();
            var allCollectLayer5Times = new List<long>();
            var allTotalTimes = new List<long>();

            var sw = new Stopwatch();

            for (int run = 1; run <= runs; run++)
            {
                Console.WriteLine($"--- Run {run}/{runs} ---");

                long totalCollectGroups = 0;
                long totalCollectLayer5 = 0;
                int totalGroups = 0;
                int totalObjects = 0;

                foreach (var (clickedS32, cellX, cellY, clickedGameX, clickedGameY) in testCells)
                {
                    // Step 1: 收集點擊格子的 Layer5 設定
                    sw.Restart();
                    var clickedCellLayer5 = new Dictionary<int, byte>();
                    foreach (var item in clickedS32.Layer5)
                    {
                        if (item.X == cellX && item.Y == cellY)
                        {
                            if (!clickedCellLayer5.ContainsKey(item.ObjectIndex))
                                clickedCellLayer5[item.ObjectIndex] = item.Type;
                        }
                    }
                    sw.Stop();
                    totalCollectLayer5 += sw.ElapsedMilliseconds;

                    // Step 2: 收集附近的群組
                    sw.Restart();
                    var nearbyGroups = new Dictionary<int, (int distance, List<ObjectTile> objects, bool hasLayer5, byte layer5Type)>();

                    foreach (var s32Data in loadResult.S32Files.Values)
                    {
                        int segStartX = s32Data.SegInfo.nLinBeginX;
                        int segStartY = s32Data.SegInfo.nLinBeginY;

                        foreach (var obj in s32Data.Layer4)
                        {
                            int objGameX = segStartX + obj.X / 2;
                            int objGameY = segStartY + obj.Y;
                            int distance = Math.Abs(objGameX - clickedGameX) + Math.Abs(objGameY - clickedGameY);

                            if (distance <= radius)
                            {
                                if (!nearbyGroups.ContainsKey(obj.GroupId))
                                {
                                    bool hasLayer5 = clickedCellLayer5.TryGetValue(obj.GroupId, out byte layer5Type);
                                    nearbyGroups[obj.GroupId] = (distance, new List<ObjectTile>(), hasLayer5, layer5Type);
                                }

                                var current = nearbyGroups[obj.GroupId];
                                if (distance < current.distance)
                                    nearbyGroups[obj.GroupId] = (distance, current.objects, current.hasLayer5, current.layer5Type);

                                nearbyGroups[obj.GroupId].objects.Add(obj);
                            }
                        }
                    }
                    sw.Stop();
                    totalCollectGroups += sw.ElapsedMilliseconds;

                    totalGroups += nearbyGroups.Count;
                    totalObjects += nearbyGroups.Values.Sum(g => g.objects.Count);
                }

                long runTotal = totalCollectGroups + totalCollectLayer5;
                allCollectGroupsTimes.Add(totalCollectGroups);
                allCollectLayer5Times.Add(totalCollectLayer5);
                allTotalTimes.Add(runTotal);

                Console.WriteLine($"  Collect Layer5:  {totalCollectLayer5,5} ms");
                Console.WriteLine($"  Collect Groups:  {totalCollectGroups,5} ms");
                Console.WriteLine($"  Total:           {runTotal,5} ms ({(double)runTotal / testCells.Count:F1} ms/click)");
                Console.WriteLine($"  Avg Groups/click: {(double)totalGroups / testCells.Count:F1}");
                Console.WriteLine($"  Avg Objects/click: {(double)totalObjects / testCells.Count:F1}");
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary (per click) ===");
            Console.WriteLine($"Collect Layer5 Avg: {allCollectLayer5Times.Average() / testCells.Count:F1} ms");
            Console.WriteLine($"Collect Groups Avg: {allCollectGroupsTimes.Average() / testCells.Count:F1} ms");
            Console.WriteLine($"Total Avg:          {allTotalTimes.Average() / testCells.Count:F1} ms");
            Console.WriteLine();
            Console.WriteLine("Note: This only tests data collection, not thumbnail generation.");
            Console.WriteLine("Thumbnail generation (2600ms in your log) is the main bottleneck.");

            return 0;
        }

        /// <summary>
        /// 測試空間索引 vs 暴力搜尋的效能比較
        /// </summary>
        public static int SpatialIndex(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: benchmark-spatialindex <map_path> [--runs N] [--radius N]");
                Console.WriteLine("範例: benchmark-spatialindex C:\\client\\map\\4");
                Console.WriteLine();
                Console.WriteLine("比較空間索引 vs 暴力搜尋的效能");
                return 1;
            }

            string mapPath = args[0];
            int runs = 5;
            int radius = 10;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--runs" && i + 1 < args.Length)
                    int.TryParse(args[++i], out runs);
                else if (args[i] == "--radius" && i + 1 < args.Length)
                    int.TryParse(args[++i], out radius);
            }

            var loadResult = MapLoader.Load(mapPath);
            if (!loadResult.Success) return 1;

            Console.WriteLine();
            Console.WriteLine($"Runs: {runs}");
            Console.WriteLine($"Radius: {radius}");

            // 統計 Layer4 物件總數
            int totalLayer4 = 0;
            foreach (var s32 in loadResult.S32Files.Values)
                totalLayer4 += s32.Layer4.Count;
            Console.WriteLine($"Total Layer4 Objects: {totalLayer4:N0}");
            Console.WriteLine();

            // 建立空間索引
            Console.Write("Building spatial index...");
            var spatialIndex = new Layer4SpatialIndex();
            spatialIndex.Build(loadResult.S32Files.Values);
            Console.WriteLine($" {spatialIndex.BuildTimeMs} ms");
            Console.WriteLine($"  Grid cells: {spatialIndex.GridCellCount:N0}");
            Console.WriteLine($"  Objects indexed: {spatialIndex.TotalObjects:N0}");
            Console.WriteLine();

            // 產生隨機測試點
            var random = new Random(42);
            int testPointCount = 20;
            var testCells = new List<(int gameX, int gameY)>();

            var cellsWithLayer4 = new List<(int gameX, int gameY)>();
            foreach (var s32 in loadResult.S32Files.Values)
            {
                foreach (var obj in s32.Layer4)
                {
                    int gameX = s32.SegInfo.nLinBeginX + obj.X / 2;
                    int gameY = s32.SegInfo.nLinBeginY + obj.Y;
                    cellsWithLayer4.Add((gameX, gameY));
                }
            }

            for (int i = 0; i < Math.Min(testPointCount, cellsWithLayer4.Count); i++)
            {
                int idx = random.Next(cellsWithLayer4.Count);
                testCells.Add(cellsWithLayer4[idx]);
            }

            Console.WriteLine($"Test Points: {testCells.Count}");
            Console.WriteLine();

            var bruteForceTimes = new List<long>();
            var spatialIndexTimes = new List<long>();
            var sw = new Stopwatch();

            for (int run = 1; run <= runs; run++)
            {
                Console.WriteLine($"--- Run {run}/{runs} ---");

                // 暴力搜尋
                long bruteForceTotal = 0;
                int bruteForceGroups = 0;
                int bruteForceObjects = 0;

                foreach (var (centerX, centerY) in testCells)
                {
                    sw.Restart();
                    var nearbyGroups = new Dictionary<int, (int distance, List<ObjectTile> objects)>();

                    foreach (var s32Data in loadResult.S32Files.Values)
                    {
                        int segStartX = s32Data.SegInfo.nLinBeginX;
                        int segStartY = s32Data.SegInfo.nLinBeginY;

                        foreach (var obj in s32Data.Layer4)
                        {
                            int objGameX = segStartX + obj.X / 2;
                            int objGameY = segStartY + obj.Y;
                            int distance = Math.Abs(objGameX - centerX) + Math.Abs(objGameY - centerY);

                            if (distance <= radius)
                            {
                                if (!nearbyGroups.TryGetValue(obj.GroupId, out var groupInfo))
                                {
                                    groupInfo = (distance, new List<ObjectTile>());
                                    nearbyGroups[obj.GroupId] = groupInfo;
                                }
                                if (distance < groupInfo.distance)
                                    nearbyGroups[obj.GroupId] = (distance, groupInfo.objects);
                                groupInfo.objects.Add(obj);
                            }
                        }
                    }
                    sw.Stop();
                    bruteForceTotal += sw.ElapsedMilliseconds;
                    bruteForceGroups += nearbyGroups.Count;
                    bruteForceObjects += nearbyGroups.Values.Sum(g => g.objects.Count);
                }

                // 空間索引搜尋
                long spatialTotal = 0;
                int spatialGroups = 0;
                int spatialObjects = 0;

                foreach (var (centerX, centerY) in testCells)
                {
                    sw.Restart();
                    var nearbyGroups = spatialIndex.CollectNearbyGroups(centerX, centerY, radius);
                    sw.Stop();
                    spatialTotal += sw.ElapsedMilliseconds;
                    spatialGroups += nearbyGroups.Count;
                    spatialObjects += nearbyGroups.Values.Sum(g => g.objects.Count);
                }

                bruteForceTimes.Add(bruteForceTotal);
                spatialIndexTimes.Add(spatialTotal);

                double speedup = bruteForceTotal > 0 ? (double)bruteForceTotal / Math.Max(1, spatialTotal) : 0;

                Console.WriteLine($"  Brute Force:    {bruteForceTotal,5} ms ({(double)bruteForceTotal / testCells.Count:F1} ms/click)");
                Console.WriteLine($"  Spatial Index:  {spatialTotal,5} ms ({(double)spatialTotal / testCells.Count:F1} ms/click)");
                Console.WriteLine($"  Speedup:        {speedup:F1}x");
                Console.WriteLine($"  Groups found:   {bruteForceGroups / testCells.Count:F1} (BF) vs {spatialGroups / testCells.Count:F1} (SI)");
                Console.WriteLine($"  Objects found:  {bruteForceObjects / testCells.Count:F1} (BF) vs {spatialObjects / testCells.Count:F1} (SI)");
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            double avgBruteForce = bruteForceTimes.Average();
            double avgSpatial = spatialIndexTimes.Average();
            double overallSpeedup = avgBruteForce / Math.Max(1, avgSpatial);

            Console.WriteLine($"Brute Force Avg:   {avgBruteForce:F0} ms ({avgBruteForce / testCells.Count:F1} ms/click)");
            Console.WriteLine($"Spatial Index Avg: {avgSpatial:F0} ms ({avgSpatial / testCells.Count:F1} ms/click)");
            Console.WriteLine($"Overall Speedup:   {overallSpeedup:F1}x");
            Console.WriteLine();
            Console.WriteLine($"Index Build Time:  {spatialIndex.BuildTimeMs} ms (one-time cost)");

            return 0;
        }
    }
}
