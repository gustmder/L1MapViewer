using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using L1FlyMapViewer;
using L1MapViewer.Helper;
using L1MapViewer.Models;
using static L1MapViewer.Other.Struct;

namespace L1MapViewer.CLI.Commands
{
    /// <summary>
    /// 地圖資料載入共用工具
    /// </summary>
    public static class MapLoader
    {
        /// <summary>
        /// 載入結果
        /// </summary>
        public class LoadResult
        {
            public bool Success { get; set; }
            public string MapId { get; set; }
            public string ClientPath { get; set; }
            public L1Map CurrentMap { get; set; }
            public Dictionary<string, S32Data> S32Files { get; set; }
            public int MinX { get; set; }
            public int MinY { get; set; }
            public int MaxX { get; set; }
            public int MaxY { get; set; }
            public int MapWidth => MaxX - MinX;
            public int MapHeight => MaxY - MinY;
            public long LoadMapDataMs { get; set; }
            public long LoadS32FilesMs { get; set; }
        }

        /// <summary>
        /// 從 S32 路徑推斷 client 路徑
        /// </summary>
        public static string FindClientPath(string s32Path)
        {
            string dir = Path.GetDirectoryName(s32Path);
            while (!string.IsNullOrEmpty(dir))
            {
                string tileIdxPath = Path.Combine(dir, "Tile.idx");
                if (File.Exists(tileIdxPath))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        /// <summary>
        /// 載入地圖資料（完整流程）
        /// </summary>
        public static LoadResult Load(string mapPath, bool verbose = true)
        {
            var result = new LoadResult { Success = false };

            if (!Directory.Exists(mapPath))
            {
                if (verbose) Console.WriteLine($"目錄不存在: {mapPath}");
                return result;
            }

            var s32FilePaths = Directory.GetFiles(mapPath, "*.s32");
            if (s32FilePaths.Length == 0)
            {
                if (verbose) Console.WriteLine($"找不到 S32 檔案: {mapPath}");
                return result;
            }

            string clientPath = FindClientPath(s32FilePaths[0]);
            if (string.IsNullOrEmpty(clientPath))
            {
                if (verbose) Console.WriteLine($"無法找到 client 資料夾（需要 Tile.idx）");
                return result;
            }
            Share.LineagePath = clientPath;
            result.ClientPath = clientPath;
            result.MapId = Path.GetFileName(mapPath);

            // 載入地圖資料
            if (verbose) Console.Write("Loading map data...");
            var sw = Stopwatch.StartNew();
            L1MapHelper.Read(clientPath);
            sw.Stop();
            result.LoadMapDataMs = sw.ElapsedMilliseconds;
            if (verbose) Console.WriteLine($" {sw.ElapsedMilliseconds} ms");

            if (!Share.MapDataList.ContainsKey(result.MapId))
            {
                if (verbose) Console.WriteLine($"找不到地圖 {result.MapId}");
                return result;
            }
            result.CurrentMap = Share.MapDataList[result.MapId];

            // 載入所有 S32 檔案
            if (verbose) Console.Write("Loading S32 files...");
            sw.Restart();
            result.S32Files = new Dictionary<string, S32Data>();
            foreach (var kvp in result.CurrentMap.FullFileNameList)
            {
                string filePath = kvp.Key;
                var segInfo = kvp.Value;

                if (!segInfo.isS32) continue;
                if (!File.Exists(filePath)) continue;

                var s32 = S32Parser.ParseFile(filePath);
                if (s32 == null) continue;

                s32.FilePath = filePath;
                s32.SegInfo = segInfo;
                result.S32Files[filePath] = s32;
            }
            sw.Stop();
            result.LoadS32FilesMs = sw.ElapsedMilliseconds;
            if (verbose) Console.WriteLine($" {sw.ElapsedMilliseconds} ms (loaded: {result.S32Files.Count})");

            // 計算地圖範圍
            result.MinX = int.MaxValue;
            result.MinY = int.MaxValue;
            result.MaxX = int.MinValue;
            result.MaxY = int.MinValue;

            foreach (var s32Data in result.S32Files.Values)
            {
                int[] loc = s32Data.SegInfo.GetLoc(1.0);
                if (loc == null) continue;
                int mx = loc[0];
                int my = loc[1];

                result.MinX = Math.Min(result.MinX, mx);
                result.MinY = Math.Min(result.MinY, my);
                result.MaxX = Math.Max(result.MaxX, mx + 3072);
                result.MaxY = Math.Max(result.MaxY, my + 1536);
            }

            if (verbose) Console.WriteLine($"Map Size: {result.MapWidth} x {result.MapHeight} px");

            result.Success = true;
            return result;
        }
    }
}
