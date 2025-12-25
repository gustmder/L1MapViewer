using System;
using System.Collections.Generic;
using System.IO;
using L1MapViewer.Models;

namespace L1MapViewer.CLI
{
    /// <summary>
    /// SEG 檔案解析器 - 解析舊版 .seg 格式地圖檔案
    /// 格式（根據 segFileToBmp 實現）：
    /// 1. Layer1: 64×128 × 2 bytes (IndexId 1 byte + TileId 1 byte)
    /// 2. Layer2: count (WORD) + items (4 bytes each)
    /// 3. Layer3: 64×64 × 2 bytes (地圖屬性)
    /// 4. Layer4: count (DWORD) + groups (每物件 5 bytes)
    /// </summary>
    public static class SegParser
    {
        /// <summary>
        /// 解析 SEG 檔案並轉換為 S32Data 結構
        /// </summary>
        public static S32Data Parse(byte[] data)
        {
            if (data == null || data.Length < 16384) // Layer1 最小大小 64*128*2
                return null;

            S32Data s32Data = new S32Data();
            s32Data.OriginalFileData = data;

            try
            {
                using (BinaryReader br = new BinaryReader(new MemoryStream(data)))
                {
                    // 記錄第一層偏移
                    s32Data.Layer1Offset = (int)br.BaseStream.Position;

                    // 第一層（地板）- 64x128，每格 2 bytes (IndexId + TileId)
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 128; x++)
                        {
                            byte indexId = br.ReadByte();
                            byte tileId = br.ReadByte();

                            s32Data.Layer1[y, x] = new TileCell
                            {
                                X = x,
                                Y = y,
                                TileId = tileId,
                                IndexId = indexId
                            };

                            // 收集使用的 tile
                            if (!s32Data.UsedTiles.ContainsKey(tileId))
                            {
                                s32Data.UsedTiles[tileId] = new TileInfo
                                {
                                    TileId = tileId,
                                    IndexId = indexId,
                                    UsageCount = 1,
                                    Thumbnail = null
                                };
                            }
                            else
                            {
                                s32Data.UsedTiles[tileId].UsageCount++;
                            }
                        }
                    }

                    // 記錄第二層偏移
                    s32Data.Layer2Offset = (int)br.BaseStream.Position;

                    // 第二層 - count (WORD) + items (4 bytes each: X, Y, TileId(2))
                    // SEG 格式的 Layer2 結構：每項 4 bytes (X(1), Y(1), TileId(2))
                    // 注意：SEG 格式沒有 IndexId，需要從 TileId 高位提取
                    if (br.BaseStream.Position + 2 <= br.BaseStream.Length)
                    {
                        int layer2Count = br.ReadUInt16();

                        for (int i = 0; i < layer2Count && br.BaseStream.Position + 4 <= br.BaseStream.Length; i++)
                        {
                            byte x = br.ReadByte();
                            byte y = br.ReadByte();
                            ushort tileData = br.ReadUInt16();

                            // SEG 格式: TileId 在低 8 位, IndexId 在高 8 位
                            byte tileId = (byte)(tileData & 0xFF);
                            byte indexId = (byte)(tileData >> 8);

                            s32Data.Layer2.Add(new Layer2Item
                            {
                                X = x,
                                Y = y,
                                IndexId = indexId,
                                TileId = tileId,
                                UK = 0
                            });
                        }
                    }

                    // 記錄第三層偏移
                    s32Data.Layer3Offset = (int)br.BaseStream.Position;

                    // 第三層（地圖屬性）- 64x64，每格 2 bytes
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 64; x++)
                        {
                            if (br.BaseStream.Position + 2 <= br.BaseStream.Length)
                            {
                                byte attr1 = br.ReadByte();
                                byte attr2 = br.ReadByte();
                                s32Data.Layer3[y, x] = new MapAttribute
                                {
                                    Attribute1 = attr1,
                                    Attribute2 = attr2
                                };
                            }
                            else
                            {
                                s32Data.Layer3[y, x] = new MapAttribute
                                {
                                    Attribute1 = 0,
                                    Attribute2 = 0
                                };
                            }
                        }
                    }

                    // 記錄第四層偏移
                    s32Data.Layer4Offset = (int)br.BaseStream.Position;

                    // 第四層（物件）
                    if (br.BaseStream.Position + 4 <= br.BaseStream.Length)
                    {
                        int layer4GroupCount = br.ReadInt32();

                        for (int i = 0; i < layer4GroupCount && br.BaseStream.Position < br.BaseStream.Length; i++)
                        {
                            if (br.BaseStream.Position + 4 > br.BaseStream.Length) break;

                            int groupId = br.ReadInt16();
                            int blockCount = br.ReadUInt16();

                            for (int j = 0; j < blockCount && br.BaseStream.Position < br.BaseStream.Length; j++)
                            {
                                if (br.BaseStream.Position + 5 > br.BaseStream.Length) break;

                                // SEG 格式: x(1), y(1), layer(1), indexId(1), tileId(1)
                                int x = br.ReadByte();
                                int y = br.ReadByte();
                                int layer = br.ReadByte();
                                int indexId = br.ReadByte();
                                int tileId = br.ReadByte();

                                var objTile = new ObjectTile
                                {
                                    GroupId = groupId,
                                    X = x,
                                    Y = y,
                                    Layer = layer,
                                    IndexId = indexId,
                                    TileId = tileId
                                };

                                s32Data.Layer4.Add(objTile);

                                // 收集使用的 tile（第四層）
                                if (!s32Data.UsedTiles.ContainsKey(tileId))
                                {
                                    s32Data.UsedTiles[tileId] = new TileInfo
                                    {
                                        TileId = tileId,
                                        IndexId = indexId,
                                        UsageCount = 1,
                                        Thumbnail = null
                                    };
                                }
                                else
                                {
                                    s32Data.UsedTiles[tileId].UsageCount++;
                                }
                            }
                        }
                    }

                    // 記錄第四層結束位置
                    s32Data.Layer4EndOffset = (int)br.BaseStream.Position;

                    // 讀取剩餘資料作為 Layer5-8
                    int remainingLength = (int)(br.BaseStream.Length - br.BaseStream.Position);
                    if (remainingLength > 0)
                    {
                        s32Data.Layer5to8Data = br.ReadBytes(remainingLength);
                    }
                    else
                    {
                        s32Data.Layer5to8Data = new byte[0];
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SegParser.Parse 錯誤: {ex.Message}");
                return null;
            }

            // Layer2 覆蓋 Layer1 - 根據逆向代碼實現
            // Layer2 項目會覆蓋 Layer1 對應位置的 TileId 和 IndexId
            // TODO: should overwrite in rendering layer.
            // ApplyLayer2ToLayer1(s32Data);

            return s32Data;
        }

        /// <summary>
        /// 將 Layer2 覆蓋到 Layer1
        /// 根據逆向代碼 sub_4E7D70，Layer2 項目會覆蓋 Layer1 對應位置
        /// </summary>
        private static void ApplyLayer2ToLayer1(S32Data s32Data)
        {
            foreach (var layer2Item in s32Data.Layer2)
            {
                int x = layer2Item.X;
                int y = layer2Item.Y;

                // 確保座標在有效範圍內 (Layer1 是 64x128)
                if (x >= 0 && x < 128 && y >= 0 && y < 64)
                {
                    // 覆蓋 Layer1 對應位置的 TileId 和 IndexId
                    s32Data.Layer1[y, x] = new TileCell
                    {
                        X = x,
                        Y = y,
                        TileId = layer2Item.TileId,
                        IndexId = layer2Item.IndexId
                    };

                    // 更新 UsedTiles
                    if (!s32Data.UsedTiles.ContainsKey(layer2Item.TileId))
                    {
                        s32Data.UsedTiles[layer2Item.TileId] = new TileInfo
                        {
                            TileId = layer2Item.TileId,
                            IndexId = layer2Item.IndexId,
                            UsageCount = 1,
                            Thumbnail = null
                        };
                    }
                    else
                    {
                        s32Data.UsedTiles[layer2Item.TileId].UsageCount++;
                    }
                }
            }
        }

        /// <summary>
        /// 從檔案解析 SEG
        /// </summary>
        public static S32Data ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            byte[] data = File.ReadAllBytes(filePath);
            var result = Parse(data);
            if (result != null)
            {
                result.FilePath = filePath;
            }
            return result;
        }
    }
}
