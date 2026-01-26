#!/usr/bin/env python3
"""
Tile 檔案完整性檢查腳本
檢查指定地圖資料夾中所有 S32 使用的 Tile 是否有偏移表異常
支援從獨立 .til 檔案或 Tile.pak 讀取
"""

import os
import sys
import struct
from collections import defaultdict

def read_s32_tile_ids(s32_path):
    """從 S32 檔案讀取所有使用的 TileId"""
    tile_ids = set()

    try:
        with open(s32_path, 'rb') as f:
            data = f.read()

        if len(data) < 0x30:
            return tile_ids

        # 讀取 SegInfo 檢查是否為有效 S32
        # 跳過 SegInfo (0x30 bytes) 直接讀取 Layer1

        # Layer1: 128x64 格子，每格 4 bytes (TileId: 2 bytes, IndexId: 1 byte, Unknown: 1 byte)
        layer1_offset = 0x30
        layer1_size = 128 * 64 * 4  # 32768 bytes

        if len(data) < layer1_offset + layer1_size:
            return tile_ids

        for y in range(64):
            for x in range(128):
                offset = layer1_offset + (y * 128 + x) * 4
                tile_id = struct.unpack('<H', data[offset:offset+2])[0]
                if tile_id > 0:
                    tile_ids.add(tile_id)

        # Layer2 和 Layer4 需要更複雜的解析，這裡簡化處理
        # 暫時只檢查 Layer1 的 TileId

    except Exception as e:
        print(f"  [錯誤] 讀取 {s32_path} 失敗: {e}", file=sys.stderr)

    return tile_ids


def load_idx_old_format(idx_path, pak_path):
    """載入 OLD 格式的 idx 檔案"""
    result = {}

    try:
        with open(idx_path, 'rb') as f:
            data = f.read()

        if len(data) < 4:
            return result

        # 前 4 bytes 是檔案數量
        file_count = struct.unpack('<I', data[0:4])[0]

        # 每個條目 28 bytes
        # 結構：position(4) + filename(20) + size(4)
        entry_size = 28
        offset = 4

        for i in range(file_count):
            if offset + entry_size > len(data):
                break

            position = struct.unpack('<I', data[offset:offset+4])[0]
            filename = data[offset+4:offset+24].split(b'\x00')[0].decode('ascii', errors='ignore')
            size = struct.unpack('<I', data[offset+24:offset+28])[0]

            if filename:
                result[filename.lower()] = {
                    'position': position,
                    'size': size,
                    'pak_path': pak_path
                }

            offset += entry_size

    except Exception as e:
        print(f"  [錯誤] 載入 idx 失敗: {e}", file=sys.stderr)

    return result


def read_tile_from_pak(idx_entry):
    """從 pak 檔案讀取 Tile 資料"""
    try:
        with open(idx_entry['pak_path'], 'rb') as f:
            f.seek(idx_entry['position'])
            data = f.read(idx_entry['size'])
        return data
    except Exception as e:
        return None


def check_tile_integrity(tile_data, tile_id=0):
    """檢查 Tile 資料的完整性"""
    result = {
        'valid': True,
        'frame_count': 0,
        'file_size': 0,
        'corrupted_from': -1,
        'corrupted_count': 0,
        'error': ''
    }

    if tile_data is None or len(tile_data) < 4:
        result['valid'] = False
        result['error'] = '資料為空或過短'
        return result

    result['file_size'] = len(tile_data)

    try:
        # 讀取 frame count
        frame_count = struct.unpack('<I', tile_data[0:4])[0]
        result['frame_count'] = frame_count

        if frame_count <= 0 or frame_count > 65536:
            result['valid'] = False
            result['error'] = f'無效的 frame 數量: {frame_count}'
            return result

        # 計算 header 大小
        header_size = 4 + frame_count * 4

        if header_size > len(tile_data):
            result['valid'] = False
            result['error'] = f'檔案過小，無法容納偏移表'
            return result

        # 讀取偏移表
        offsets = []
        for i in range(frame_count):
            offset = struct.unpack('<I', tile_data[4 + i*4 : 8 + i*4])[0]
            offsets.append(offset)

        # 檢查偏移表完整性
        corrupted_from = -1
        previous_valid_diff = -1
        corrupted_count = 0

        for i in range(1, frame_count):
            diff = offsets[i] - offsets[i-1]

            # 偵測異常：連續多個 frame 偏移差值只有 1 byte
            if diff == 1:
                if previous_valid_diff > 10 and corrupted_from == -1:
                    corrupted_from = i
                corrupted_count += 1
            elif diff > 1:
                previous_valid_diff = diff

        result['corrupted_from'] = corrupted_from
        result['corrupted_count'] = corrupted_count

        # 判斷是否有效
        if corrupted_count >= 5:
            result['valid'] = False
            result['error'] = f'偏移表異常：從 frame {corrupted_from} 開始，共 {corrupted_count} 個 frame 損壞'

    except Exception as e:
        result['valid'] = False
        result['error'] = str(e)

    return result


def main():
    if len(sys.argv) < 2:
        print("用法: python check_tile_integrity.py <地圖資料夾> [Client資料夾]")
        print()
        print("範例:")
        print("  python check_tile_integrity.py C:\\client\\map\\0")
        print("  python check_tile_integrity.py C:\\client\\map C:\\client")
        sys.exit(1)

    map_path = sys.argv[1]

    # 決定 Client 資料夾位置（用於找 Tile.idx/pak）
    if len(sys.argv) >= 3:
        client_path = sys.argv[2]
    else:
        # 自動尋找：從 map_path 向上找
        client_path = map_path
        while client_path:
            # 檢查是否有 Tile 資料夾或 Tile.idx
            tile_folder = os.path.join(client_path, 'Tile')
            tile_idx = os.path.join(client_path, 'Tile.idx')
            if os.path.exists(tile_folder) and os.listdir(tile_folder):
                break
            if os.path.exists(tile_idx):
                break
            parent = os.path.dirname(client_path)
            if parent == client_path:
                break
            client_path = parent

    # 確定 Tile 來源
    tile_folder = os.path.join(client_path, 'Tile')
    tile_idx_path = os.path.join(client_path, 'Tile.idx')
    tile_pak_path = os.path.join(client_path, 'Tile.pak')

    use_pak = False
    idx_data = None

    if os.path.exists(tile_idx_path) and os.path.exists(tile_pak_path):
        print(f"使用 Tile.pak 作為 Tile 來源")
        idx_data = load_idx_old_format(tile_idx_path, tile_pak_path)
        use_pak = True
        print(f"已載入 {len(idx_data)} 個 Tile 索引")
    elif os.path.exists(tile_folder) and os.listdir(tile_folder):
        print(f"使用獨立 .til 檔案作為 Tile 來源")
    else:
        print(f"錯誤: 找不到 Tile 資源")
        print(f"  已檢查: {tile_folder}")
        print(f"  已檢查: {tile_idx_path}")
        sys.exit(1)

    print(f"=== Tile 完整性檢查 ===")
    print(f"地圖路徑: {map_path}")
    print(f"Client 路徑: {client_path}")
    print()

    # 收集所有 S32 檔案
    s32_files = []
    for root, dirs, files in os.walk(map_path):
        for f in files:
            if f.lower().endswith('.s32'):
                s32_files.append(os.path.join(root, f))

    print(f"找到 {len(s32_files)} 個 S32 檔案")

    # 收集所有使用的 TileId
    all_tile_ids = set()
    processed = 0

    for s32_file in s32_files:
        tile_ids = read_s32_tile_ids(s32_file)
        all_tile_ids.update(tile_ids)
        processed += 1
        if processed % 100 == 0:
            print(f"  已處理 {processed}/{len(s32_files)} 個 S32 檔案，發現 {len(all_tile_ids)} 個不同的 TileId")

    print(f"\n共使用 {len(all_tile_ids)} 個不同的 TileId")
    print()

    # 檢查每個 Tile 的完整性
    print("檢查 Tile 檔案完整性...")
    corrupted_tiles = []
    missing_tiles = []
    checked = 0

    for tile_id in sorted(all_tile_ids):
        tile_filename = f"{tile_id}.til"
        tile_data = None

        if use_pak:
            # 從 pak 讀取
            key = tile_filename.lower()
            if key in idx_data:
                tile_data = read_tile_from_pak(idx_data[key])
            else:
                missing_tiles.append(tile_id)
                continue
        else:
            # 從獨立檔案讀取
            tile_file = os.path.join(tile_folder, tile_filename)
            if not os.path.exists(tile_file):
                missing_tiles.append(tile_id)
                continue
            with open(tile_file, 'rb') as f:
                tile_data = f.read()

        if tile_data is None:
            missing_tiles.append(tile_id)
            continue

        result = check_tile_integrity(tile_data, tile_id)

        if not result['valid']:
            corrupted_tiles.append((tile_id, result))

        checked += 1
        if checked % 500 == 0:
            print(f"  已檢查 {checked}/{len(all_tile_ids)} 個 Tile")

    print()
    print("=== 結果摘要 ===")
    print(f"已檢查 Tile: {checked}")
    print(f"缺少 Tile: {len(missing_tiles)}")
    print(f"損壞 Tile: {len(corrupted_tiles)}")

    if missing_tiles:
        print(f"\n缺少的 Tile ({len(missing_tiles)} 個):")
        for tid in missing_tiles[:20]:
            print(f"  {tid}.til")
        if len(missing_tiles) > 20:
            print(f"  ... 還有 {len(missing_tiles) - 20} 個")

    if corrupted_tiles:
        print(f"\n損壞的 Tile ({len(corrupted_tiles)} 個):")
        for tid, result in corrupted_tiles:
            print(f"  {tid}.til - {result['error']}")
            print(f"    Frames: {result['frame_count']}, 損壞數: {result['corrupted_count']}, 從 Frame {result['corrupted_from']} 開始")

    if not corrupted_tiles and not missing_tiles:
        print("\n所有 Tile 檔案完整性檢查通過！")

    return 0 if not corrupted_tiles else 1


if __name__ == '__main__':
    sys.exit(main())
