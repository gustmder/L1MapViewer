# L1MapViewer 大地圖效能優化計劃

## 問題描述
在載入 560 個 S32 區塊的大地圖後，拖曳操作變得非常緩慢。

## 效能分析結果

### 關鍵數據
| 項目 | 數值 |
|------|------|
| S32 區塊大小 | 3072×1536 px (每塊 ~9.4 MB) |
| 渲染緩衝區邊距 | 2048 px（四邊） |
| 典型渲染區域 | ~6000×5200 px (~62 MB) |
| 每次渲染區塊數 | 6-8 塊 |
| 重新渲染觸發條件 | viewport 移動超過 1024 px |

### 已識別的效能瓶頸

#### 1. **[嚴重] MouseMove 中的 Update() 強制同步繪製**
- **位置**: `MapForm.cs:8433`
- **問題**: 每次滑鼠移動都調用 `s32PictureBox.Update()`，強制同步繪製
- **影響**: 60-120 Hz 的同步 Paint 調用，阻塞 UI 執行緒

```csharp
s32PictureBox.Invalidate();
s32PictureBox.Update();  // ← 阻塞直到 Paint 完成
```

#### 2. **[中等] Paint 事件中的 DrawImage 縮放開銷**
- **位置**: `MapForm.cs:8629-8645`
- **問題**: 大型 viewport bitmap (6000×5200) 的縮放操作昂貴
- **影響**: 每次 Paint 都要處理 ~62 MB 的縮放

#### 3. **[中等] MouseUp 時同步更新 MiniMap**
- **位置**: `MapForm.cs:8496`
- **問題**: `UpdateMiniMap()` 同步創建新 Bitmap
- **影響**: 拖曳結束時有短暫卡頓

#### 4. **[低等] Lock 競爭**
- **位置**: `_viewportBitmapLock` 在 Paint 和 Render callback 之間
- **問題**: 背景渲染完成時的 Bitmap 交換可能阻塞 Paint

#### 5. **[潛在] 渲染緩衝區過大**
- **問題**: 2048px 邊距可能過大，導致渲染更多區塊

---

## 實施順序（漸進式優化）

### 第一輪：快速修復（立即執行）

| 步驟 | 修改 | 檔案:行號 | 預期效果 |
|------|------|-----------|----------|
| 1 | 移除 `Update()` 調用 | `MapForm.cs:8433` | 大幅減少同步 Paint |
| 2 | 延遲 MiniMap 更新 | `MapForm.cs:8496` | 消除拖曳結束卡頓 |
| 3 | 緩衝區邊距改為 1536px | `ViewState.cs:283` | 減少 25% 渲染區域 |

**測試**: 完成後載入 560 S32 地圖測試拖曳流暢度

### 第二輪：進階優化（視第一輪效果決定）

| 步驟 | 修改 | 預期效果 |
|------|------|----------|
| 4 | 預縮放 Bitmap | 消除 Paint 中的縮放開銷 |
| 5 | 平行區塊渲染 | 多核心加速 |
| 6 | 雙緩衝區 | 消除 Lock 競爭 |

---

## 優化細節

### 2.1 移除 Update() 強制同步繪製
**檔案**: `MapForm.cs:8432-8433`

```csharp
// 修改前
s32PictureBox.Invalidate();
s32PictureBox.Update();  // 移除這行

// 修改後
s32PictureBox.Invalidate();
// 讓 OS 自然批次處理 Paint 訊息
```

**預期效果**: 大幅減少 Paint 調用次數（從 60-120/秒 降到 10-30/秒）

### 2.2 延遲 MiniMap 更新
**檔案**: `MapForm.cs:8496`

```csharp
// 修改前
UpdateMiniMap();

// 修改後
// 使用 BeginInvoke 延遲到拖曳事件處理完成後
this.BeginInvoke((MethodInvoker)delegate { UpdateMiniMap(); });
```

**預期效果**: 消除拖曳結束時的卡頓

### 2.3 縮小渲染緩衝區邊距
**檔案**: `ViewState.cs:283`

```csharp
// 修改前
public int RenderBufferMargin { get; set; } = 2048;

// 修改後
public int RenderBufferMargin { get; set; } = 1536;
```

**權衡**:
- 優點: 減少渲染區塊數和 Bitmap 大小
- 缺點: viewport 移動 768px 就觸發重新渲染（原本是 1024px）

---

## 關鍵檔案

- `MapForm.cs:8407-8437` - 拖曳 MouseMove 處理
- `MapForm.cs:8624-8726` - Paint 事件
- `MapForm.cs:5277-5513` - RenderViewport 背景渲染
- `Models/ViewState.cs:283` - RenderBufferMargin 設定
- `Models/ViewState.cs:352-370` - NeedsRerender 邏輯

---

## 效能診斷 Log 說明

### Console Log 訊息

| Log 訊息 | 觸發時機 | 用途 |
|----------|----------|------|
| `[RERENDER-TRIGGERED]` | 拖曳結束 150ms 後，系統決定要重新渲染 | 看是否頻繁觸發渲染 |
| `[RENDER-START]` | 背景渲染開始 | 看渲染何時開始 |
| `[RENDER-COMPLETE]` | 渲染完成，Bitmap 交換 | 看渲染花了多久、lock 等待多久 |
| `[PAINT]` | DrawImage 超過 10ms | 看繪製是否太慢 |
| `[DRAG-END]` | 拖曳結束 | 看 FPS 和 moves/paints 比例 |

### 診斷指標

**拖曳效能指標** (`[DRAG-END]`):
- `duration`: 拖曳持續時間 (ms)
- `moves`: MouseMove 事件次數
- `paints`: 實際 Paint 執行次數
- `FPS`: 實際繪製幀率 (paints/duration*1000)

**正常情況**:
- `paints` 應該是 `moves` 的 1/3 ~ 1/2（OS 批次處理）
- `FPS` 應該 > 20

**異常情況**:
- `moves=1, paints=0` 持續數秒 → UI 執行緒完全凍結
- `FPS < 10` → Paint 太慢或被阻塞

### 渲染流程診斷

**正常流程**:
```
[DRAG-END] duration=2000ms, moves=100, paints=80, FPS=40
[RERENDER-TRIGGERED] ScrollX=1234, ScrollY=5678
[RENDER-START] worldRect=4000x3500
[RENDER-COMPLETE] size=4000x3500, lockTime=0ms, invokeTime=5ms
```

**問題徵兆**:
1. `lockTime` 很大 → Lock 競爭，Paint 在等 `_viewportBitmapLock`
2. `[RENDER-START]` 到 `[RENDER-COMPLETE]` 間隔很長 → 渲染本身太慢
3. 拖曳期間出現 `[RENDER-START]` → 渲染干擾拖曳

### 測試步驟

1. 載入 560 S32 大地圖
2. 用中鍵拖曳地圖（拖曳 2-3 秒）
3. 放開後觀察 Console 輸出
4. 重複多次，看是否有 `moves=1, paints=0` 的凍結情況

---

## CLI Benchmark 結果

### Viewport 渲染 (561 S32 blocks)

| Mode | Average | Min | Max | Speedup |
|------|---------|-----|-----|---------|
| Sequential | 523 ms | - | 766 ms | - |
| **Parallel** | **170 ms** | - | 262 ms | **~3x** |

### MiniMap 渲染 (561 S32 blocks, 256x130 output)

| Mode | Average | Min | Max | Speedup |
|------|---------|-----|-----|---------|
| DrawImage 縮放 | ~7,200 ms | - | - | - |
| **直接像素渲染** | **140 ms** | 110 ms | 182 ms | **~50x** |

### S32 Parse (561 files, Parallel)

| Run | Time (ms) |
|-----|-----------|
| 1 | 781 |
| 2 | 734 |
| 3 | 724 |
| 4 | 836 |
| 5 | 643 |
| 6 | 714 |
| 7 | 696 |
| 8 | 749 |
| 9 | 739 |
| 10 | 782 |

**Summary:**
- **Average: 740 ms**
- **Min: 643 ms**
- **Max: 836 ms**
- **Per File: 1.32 ms**

---

## 已完成的優化

### 1. Viewport 平行渲染
- **檔案**: `Helper/ViewportRenderer.cs`, `MapForm.cs`
- **方法**: 使用 `Parallel.ForEach` 平行渲染 S32 區塊
- **效果**: ~3x 加速

### 2. MiniMap 直接像素渲染
- **檔案**: `Helper/MiniMapRenderer.cs`
- **方法**: 跳過 full-size bitmap，直接取樣 Layer1 格子顏色寫入 mini map
- **效果**: ~50x 加速（7200ms → 140ms）

### 3. S32 平行解析
- **檔案**: `MapForm.cs`, `Models/MapDocument.cs`
- **方法**: 使用 `Parallel.ForEach` 平行解析 S32 檔案
- **效果**: ~22% 加速（CLI），Form 受 JIT/GC 影響較大

### 4. 減少記憶體複製
- **檔案**: `MapForm.cs`, `CLI/S32Parser.cs`
- **方法**: 移除不必要的 `Array.Copy`，直接使用已讀取的 byte[]
- **效果**: 減少 ~28MB 記憶體配置（561 files × 50KB）

### 5. UpdateLayer5InvalidButton 異步化
- **檔案**: `MapForm.cs:17070`
- **問題**: `GetInvalidTileIds()` 會遍歷所有 S32 的 Layer1/2/4，對每個唯一 TileId 呼叫 `L1PakReader.UnPack()` 驗證存在性
- **瓶頸**: 561 S32 × (64×128 Layer1 + Layer2 + Layer4) = 數百萬次檢查，每個唯一 TileId 需要讀取 pak 檔案
- **解決**: 將整個函式包在 `Task.Run()` 中，完成後用 `BeginInvoke()` 更新 UI
- **效果**: Tile List 階段從 ~3000ms 降到 ~0ms（驗證移到背景）

---

## CLI Benchmark 指令

```bash
# Viewport 渲染測試
L1MapViewerCore.exe -cli benchmark-viewport <map_path> [--regions N]

# MiniMap 渲染測試
L1MapViewerCore.exe -cli benchmark-minimap <map_path> [--runs N]

# S32 解析測試
L1MapViewerCore.exe -cli benchmark-s32parse <map_path> [--runs N] [--parallel]
```
