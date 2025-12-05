using System;
using System.Collections.Generic;
using System.IO;

namespace L1MapViewer.Converter {
    class L1Til {

        /// <summary>
        /// Tile 版本類型
        /// </summary>
        public enum TileVersion
        {
            /// <summary>24x24 舊版</summary>
            Classic,
            /// <summary>48x48 R版 (Remaster)</summary>
            Remaster,
            /// <summary>無法判斷</summary>
            Unknown
        }

        /// <summary>
        /// 判斷 til 資料是否為 R 版 (48x48)
        /// 根據第一個 block 的大小判斷：
        /// - 24x24: 約 625 bytes (2*12*13*2 + 1)
        /// - 48x48: 約 2401 bytes (2*24*25*2 + 1)
        /// </summary>
        public static bool IsRemaster(byte[] tilData)
        {
            return GetVersion(tilData) == TileVersion.Remaster;
        }

        /// <summary>
        /// 取得 til 資料的版本
        /// </summary>
        public static TileVersion GetVersion(byte[] tilData)
        {
            if (tilData == null || tilData.Length < 8)
                return TileVersion.Unknown;

            try
            {
                using (var br = new BinaryReader(new MemoryStream(tilData)))
                {
                    int blockCount = br.ReadInt32();
                    if (blockCount <= 0)
                        return TileVersion.Unknown;

                    // 讀取前兩個 offset 來計算第一個 block 大小
                    int offset0 = br.ReadInt32();
                    int offset1 = br.ReadInt32();
                    int firstBlockSize = offset1 - offset0;

                    // 根據 block 大小判斷
                    // 24x24: 2*12*13*2 + 1 = 625 bytes (容許範圍 400-1000)
                    // 48x48: 2*24*25*2 + 1 = 2401 bytes (容許範圍 1800-3500)
                    if (firstBlockSize >= 400 && firstBlockSize <= 1000)
                        return TileVersion.Classic;
                    else if (firstBlockSize >= 1800 && firstBlockSize <= 3500)
                        return TileVersion.Remaster;
                    else
                        return TileVersion.Unknown;
                }
            }
            catch
            {
                return TileVersion.Unknown;
            }
        }

        /// <summary>
        /// 取得 til 版本對應的 tile 尺寸
        /// </summary>
        public static int GetTileSize(TileVersion version)
        {
            switch (version)
            {
                case TileVersion.Classic: return 24;
                case TileVersion.Remaster: return 48;
                default: return 24;
            }
        }

        /// <summary>
        /// 從 til 資料取得 tile 尺寸
        /// </summary>
        public static int GetTileSize(byte[] tilData)
        {
            return GetTileSize(GetVersion(tilData));
        }

        /// <summary>
        /// 將 R 版 (48x48) 的 block 縮小成 Classic 版 (24x24)
        /// 使用 2.5D 菱形結構進行正確的 2x2 區塊平均縮放
        /// </summary>
        public static byte[] DownscaleBlock(byte[] blockData)
        {
            if (blockData == null || blockData.Length < 2)
                return blockData;

            byte type = blockData[0];

            // 計算來源尺寸 (48x48 -> halfSize=24)
            int srcDataLen = blockData.Length - 1;
            int srcPixelCount = srcDataLen / 2;

            // 反推 n: pixelCount = 2 * n * (n+1), 解出 n
            double n = (-1 + Math.Sqrt(1 + 2 * srcPixelCount)) / 2;
            int srcHalfSize = (int)Math.Round(n);

            // 如果不是 48x48 (halfSize=24)，直接返回原始資料
            if (srcHalfSize != 24)
                return blockData;

            int dstHalfSize = 12;  // 目標 24x24

            // 解析來源像素到 2D 菱形陣列
            // 菱形每行的像素數: 行 y 的像素數 = (y < halfSize/2) ? (y+1)*2 : (halfSize-1-y)*2
            var srcRows = new List<ushort[]>();
            int srcOffset = 1;  // 跳過 type byte

            for (int y = 0; y < srcHalfSize; y++)
            {
                int rowPixelCount = (y <= srcHalfSize / 2 - 1) ? (y + 1) * 2 : (srcHalfSize - 1 - y) * 2;
                var row = new ushort[rowPixelCount];
                for (int x = 0; x < rowPixelCount; x++)
                {
                    if (srcOffset + 1 < blockData.Length)
                    {
                        row[x] = (ushort)(blockData[srcOffset] | (blockData[srcOffset + 1] << 8));
                        srcOffset += 2;
                    }
                }
                srcRows.Add(row);
            }

            // 建立目標像素陣列 (2x2 縮放)
            var result = new List<byte> { type };

            for (int dstY = 0; dstY < dstHalfSize; dstY++)
            {
                int dstRowPixelCount = (dstY <= dstHalfSize / 2 - 1) ? (dstY + 1) * 2 : (dstHalfSize - 1 - dstY) * 2;

                for (int dstX = 0; dstX < dstRowPixelCount; dstX++)
                {
                    // 對應來源的 2x2 區塊
                    int srcY1 = dstY * 2;
                    int srcY2 = dstY * 2 + 1;
                    int srcX1 = dstX * 2;
                    int srcX2 = dstX * 2 + 1;

                    int r = 0, g = 0, b = 0, count = 0;

                    // 取樣來源 2x2 區塊的像素
                    void SamplePixel(int sy, int sx)
                    {
                        if (sy < srcRows.Count && sx >= 0 && sx < srcRows[sy].Length)
                        {
                            ushort c = srcRows[sy][sx];
                            r += (c >> 10) & 0x1F;
                            g += (c >> 5) & 0x1F;
                            b += c & 0x1F;
                            count++;
                        }
                    }

                    SamplePixel(srcY1, srcX1);
                    SamplePixel(srcY1, srcX2);
                    SamplePixel(srcY2, srcX1);
                    SamplePixel(srcY2, srcX2);

                    if (count > 0)
                    {
                        r /= count;
                        g /= count;
                        b /= count;
                    }

                    ushort avgColor = (ushort)((r << 10) | (g << 5) | b);
                    result.Add((byte)(avgColor & 0xFF));
                    result.Add((byte)((avgColor >> 8) & 0xFF));
                }
            }

            // 加上 Parse 多讀的 1 byte (與原始格式相容)
            result.Add(0);

            return result.ToArray();
        }

        /// <summary>
        /// 將整個 R 版 til 檔案縮小成 Classic 版
        /// </summary>
        public static byte[] DownscaleTil(byte[] tilData)
        {
            if (!IsRemaster(tilData))
                return tilData; // 已經是 Classic 版，不需縮小

            var blocks = Parse(tilData);
            var downscaledBlocks = new List<byte[]>();

            foreach (var block in blocks)
            {
                downscaledBlocks.Add(DownscaleBlock(block));
            }

            // 重新組裝 til 檔案
            return BuildTil(downscaledBlocks);
        }

        /// <summary>
        /// 從 block 列表組裝 til 檔案
        /// Parse 讀取的 block 會多 1 byte，所以寫入時要扣掉
        /// </summary>
        public static byte[] BuildTil(List<byte[]> blocks)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // 寫入 block 數量
                bw.Write(blocks.Count);

                // 計算並寫入偏移量 (blockCount + 1 個偏移量)
                int currentOffset = 0;
                for (int i = 0; i < blocks.Count; i++)
                {
                    bw.Write(currentOffset);
                    currentOffset += blocks[i].Length - 1; // 實際資料長度 (Parse 多讀了 1 byte)
                }
                // 最後一個偏移量是結尾位置
                bw.Write(currentOffset);

                // 寫入 block 資料
                foreach (var block in blocks)
                {
                    // Parse 多讀了 1 byte，寫入時扣掉
                    int writeLen = block.Length - 1;
                    if (writeLen > 0)
                        bw.Write(block, 0, writeLen);
                }

                return ms.ToArray();
            }
        }

        //畫大地圖用的(將.til檔案 拆成更小的單位)
        public static List<byte[]> Parse(byte[] srcData) {
            List<byte[]> result = new List<byte[]>();
            try {
                using (BinaryReader br = new BinaryReader(new MemoryStream(srcData))) {

                    // 取得Block數量. 
                    int nAllBlockCount = br.ReadInt32();

                    int[] nsBlockOffset = new int[nAllBlockCount + 1];
                    for (int i = 0; i <= nAllBlockCount; i++) {
                        nsBlockOffset[i] = br.ReadInt32();// 載入Block的偏移位置.
                    }
                   
                    int nCurPosition = (int)br.BaseStream.Position;

                    // 載入Block的資料.
                    for (int i = 0; i < nAllBlockCount; i++) {
                        int nPosition = nCurPosition + nsBlockOffset[i];
                        br.BaseStream.Seek(nPosition, SeekOrigin.Begin);

                        int nSize = nsBlockOffset[i + 1] - nsBlockOffset[i];
                        if (nSize <= 0) {
                            nSize = srcData.Length - nsBlockOffset[i];
                        }

                        // int type = br.ReadByte();
                        byte[] data = br.ReadBytes(nSize + 1);
                        result.Add(data);
                    }
                }
            } catch {
                // Utils.outputText("L1Til_Parse發生問題的檔案:" + logFileName, "Log.txt");
            }
            return result;
        }
    }
}

