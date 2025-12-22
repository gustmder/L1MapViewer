# 地圖通行資料匯出格式規格

參考來源: `C:\workspaces\lineage\tool\MapTool\map.cs`

## 資料來源

### Layer3 屬性讀取

從 S32 檔案讀取 Layer3 (64x64) 的兩個屬性值：

- `tileList_t1[x,y]` = Layer3 的 Attribute1
- `tileList_t3[x,y]` = Layer3 的 Attribute2

### 屬性值例外處理

在進行任何計算之前，必須先對屬性值進行例外處理。這些特殊值會被轉換為 5：

| 原始值 | 二進位 | 十六進位 | 轉換後 |
|--------|--------|----------|--------|
| 33 | 00100001 | 0x21 | 5 |
| 65 | 01000001 | 0x41 | 5 |
| 69 | 01000101 | 0x45 | 5 |
| 73 | 01001001 | 0x49 | 5 |
| 77 | 01001101 | 0x4D | 5 |

**重要**: 這會影響區域類型判斷！例如：
- 65 (0x41) → last hex = "1" → 原本是一般區域
- 轉換為 5 (0x05) → last hex = "5" → 變成安全區域

```csharp
private int replaceException(int i)
{
    if (i == 65 || i == 69 || i == 73 || i == 33 || i == 77)
        i = 5;
    return i;
}
```

## 8 方向通行性計算 (decryptData)

計算結果存入 `tileList[x,y]`，為 8 方向通行性 + 區域類型的組合值。

### 4 個基本方向

| 方向 | 位元 | 值 | 判斷條件 |
|------|------|-----|----------|
| D0 (下) | bit 0 | +1 | `(tileList_t1[x, y+1] & 1) == 0` |
| D4 (上) | bit 1 | +2 | `(tileList_t1[x, y] & 1) == 0` |
| D2 (左) | bit 2 | +4 | `(tileList_t3[x-1, y] & 1) == 0` |
| D6 (右) | bit 3 | +8 | `(tileList_t3[x, y] & 1) == 0` |

### 4 個對角方向

| 方向 | 位元 | 值 | 判斷函數 |
|------|------|-----|----------|
| D1 (左下) | bit 4 | +16 | `isPassable_D1(x-1, y+1)` |
| D3 (左上) | bit 5 | +32 | `isPassable_D3(x-1, y-1)` |
| D5 (右上) | bit 6 | +64 | `isPassable_D5(x+1, y-1)` |
| D7 (右下) | bit 7 | +128 | `isPassable_D7(x+1, y+1)` |

### 對角方向判斷邏輯

```csharp
// D1 (左下): 檢查 4 個格子
isPassable_D1(x, y) =>
    (tileList_t1[x, y] & 1) == 0 &&
    (tileList_t1[x+1, y] & 1) == 0 &&
    (tileList_t3[x+1, y] & 1) == 0 &&
    (tileList_t3[x+1, y-1] & 1) == 0

// D3 (左上): 檢查 4 個格子
isPassable_D3(x, y) =>
    (tileList_t1[x, y+1] & 1) == 0 &&
    (tileList_t1[x+1, y+1] & 1) == 0 &&
    (tileList_t3[x, y] & 1) == 0 &&
    (tileList_t3[x, y+1] & 1) == 0

// D5 (右上): 檢查 4 個格子
isPassable_D5(x, y) =>
    (tileList_t1[x, y+1] & 1) == 0 &&
    (tileList_t1[x-1, y+1] & 1) == 0 &&
    (tileList_t3[x-1, y] & 1) == 0 &&
    (tileList_t3[x-1, y+1] & 1) == 0

// D7 (右下): 檢查 4 個格子
isPassable_D7(x, y) =>
    (tileList_t1[x, y] & 1) == 0 &&
    (tileList_t1[x-1, y] & 1) == 0 &&
    (tileList_t3[x-1, y] & 1) == 0 &&
    (tileList_t3[x-1, y-1] & 1) == 0
```

### 區域類型 (getZone)

根據 `tileList_t1[x,y]` 的低 4 位元 (十六進位最後一位) 判斷：

| 十六進位值 | 區域類型 | 值 |
|------------|----------|-----|
| 0, 1, 2, 3 | 一般區域 | +256 (0x100) |
| 4, 5, 6, 7, C, D, E, F | 安全區域 | +512 (0x200) |
| 8, 9, A, B | 戰鬥區域 | +1024 (0x400) |

---

## 輸出格式

### 格式選項

MapTool 支援三種輸出格式：

| 格式 | 說明 | 輸出內容 |
|------|------|----------|
| **L1J** | L1J 伺服器格式 | `formate_L1J(tileList)` |
| **DIR** | 原始 8 方向格式 | `tileList` (不轉換) |
| **DEBUG** | 除錯用 | `tileList_t1` + `tileList_t3` |

---

## L1J 格式 (formate_L1J)

將 `tileList` 轉換為 L1J 伺服器使用的簡化格式。

### 轉換規則

```csharp
int[,] formate_L1J(int[,] tileList)
{
    int[,] result = new int[xLength, yLength];

    for (int y = 0; y < yLength; y++)
    {
        for (int x = 0; x < xLength; x++)
        {
            int tile = tileList[x, y];

            // 南北方向 (D0 或 D4 可通行)
            if ((tile & 1) == 1 || (tile & 2) == 2)
                result[x, y] += 2;

            // 東西方向 (D2 或 D6 可通行)
            if ((tile & 4) == 4 || (tile & 8) == 8)
                result[x, y] += 1;

            // 南北雙向可通行
            if ((tile & 1) == 1 && (tile & 2) == 2)
                result[x, y] += 8;

            // 東西雙向可通行
            if ((tile & 4) == 4 && (tile & 8) == 8)
                result[x, y] += 4;

            // 區域類型
            // 一般區域 (256): 不加值
            // 安全區域 (512): +16
            if ((tile & 512) == 512)
                result[x, y] += 16;
            // 戰鬥區域 (1024): +32
            if ((tile & 1024) == 1024)
                result[x, y] += 32;
        }
    }
    return result;
}
```

### L1J 輸出值結構

| 位元 | 值 | 意義 |
|------|-----|------|
| bit 0 | 1 | 東西方向可通行 |
| bit 1 | 2 | 南北方向可通行 |
| bit 2 | 4 | 東西雙向可通行 |
| bit 3 | 8 | 南北雙向可通行 |
| bit 4 | 16 | 安全區域 |
| bit 5 | 32 | 戰鬥區域 |

### L1J 常見值範例

| 值 | 二進位 | 意義 |
|----|--------|------|
| 0 | 000000 | 不可通行 |
| 1 | 000001 | 只能東西移動 |
| 2 | 000010 | 只能南北移動 |
| 3 | 000011 | 可東西南北移動 (單向) |
| 15 | 001111 | 完全可通行 (一般區域) |
| 31 | 011111 | 完全可通行 (安全區域) |
| 47 | 101111 | 完全可通行 (戰鬥區域) |

---

## DIR 格式

直接輸出 `tileList` 的原始值，不經過 `formate_L1J` 轉換。

### DIR 輸出值結構

| 位元 | 值 | 意義 |
|------|-----|------|
| bit 0 | 1 | D0 (下) 可通行 |
| bit 1 | 2 | D4 (上) 可通行 |
| bit 2 | 4 | D2 (左) 可通行 |
| bit 3 | 8 | D6 (右) 可通行 |
| bit 4 | 16 | D1 (左下) 可通行 |
| bit 5 | 32 | D3 (左上) 可通行 |
| bit 6 | 64 | D5 (右上) 可通行 |
| bit 7 | 128 | D7 (右下) 可通行 |
| bit 8 | 256 | 一般區域 |
| bit 9 | 512 | 安全區域 |
| bit 10 | 1024 | 戰鬥區域 |

### DIR 常見值範例

| 值 | 意義 |
|----|------|
| 256 | 不可通行 (一般區域) |
| 511 | 8 方向全通行 (一般區域): 255 + 256 |
| 767 | 8 方向全通行 (安全區域): 255 + 512 |
| 1279 | 8 方向全通行 (戰鬥區域): 255 + 1024 |

---

## 輸出檔案格式

### 檔案格式
- 副檔名: `.txt`
- 編碼: UTF-8
- 格式: CSV (逗號分隔)

### 檔案結構

```
[可選: 座標範圍 - 當 isWriteRange = true]
xBegin
xEnd
yBegin
yEnd

[資料區 - yLength 行, 每行 xLength 個值]
value[0,0],value[1,0],value[2,0],...,value[xLength-1,0]
value[0,1],value[1,1],value[2,1],...,value[xLength-1,1]
...
value[0,yLength-1],value[1,yLength-1],...,value[xLength-1,yLength-1]
```

### 輸出代碼

```csharp
private void outputTxt(int[,] data, string path)
{
    TextWriter writer = new StreamWriter(path + ".txt");

    // 可選: 寫入座標範圍
    if (this.isWriteRange)
    {
        writer.Write(this.xBegin + "\r\n");
        writer.Write(this.xEnd + "\r\n");
        writer.Write(this.yBegin + "\r\n");
        writer.Write(this.yEnd + "\r\n");
    }

    // 寫入資料 (y 為行, x 為列)
    for (int y = 0; y < this.yLength; y++)
    {
        for (int x = 0; x < this.xLength; x++)
        {
            writer.Write(data[x, y]);
            if (x < this.xLength - 1)
                writer.Write(",");
        }
        writer.WriteLine();
    }

    writer.Close();
}
```

---

## L1MapViewer 實作狀態

| 功能 | 狀態 | 備註 |
|------|------|------|
| L1J 格式匯出 | ✅ 已實作 | `MapForm.ExportMapData()` |
| DIR 格式匯出 | ✅ 已實作 | `MapForm.ExportMapData(isL1JFormat: false)` |
| DEBUG 格式匯出 | ❌ 未實作 | 低優先級 |
| 座標範圍輸出 | ❌ 未實作 | `isWriteRange` 選項 |
| 屬性值例外處理 | ✅ 已實作 | `MapForm.ReplaceException()` |
