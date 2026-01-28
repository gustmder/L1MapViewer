using System;

namespace L1MapViewer.Models
{
    /// <summary>
    /// 渲染選項 - 不可變資料結構
    /// 用於傳遞給渲染器，指定要顯示哪些層級和覆蓋層
    /// </summary>
    public readonly struct RenderOptions
    {
        /// <summary>顯示第一層（地板）</summary>
        public bool ShowLayer1 { get; }

        /// <summary>顯示第二層</summary>
        public bool ShowLayer2 { get; }

        /// <summary>顯示第四層（物件）</summary>
        public bool ShowLayer4 { get; }

        /// <summary>顯示第三層屬性（菱形邊線）</summary>
        public bool ShowLayer3Attributes { get; }

        /// <summary>顯示通行性覆蓋層</summary>
        public bool ShowPassability { get; }

        /// <summary>顯示安全區域（藍色）</summary>
        public bool ShowSafeZones { get; }

        /// <summary>顯示戰鬥區域（紅色）</summary>
        public bool ShowCombatZones { get; }

        /// <summary>顯示商店區域（綠色）</summary>
        public bool ShowMarketZones { get; }

        /// <summary>顯示遊戲格線</summary>
        public bool ShowGrid { get; }

        /// <summary>顯示 S32 區塊邊界</summary>
        public bool ShowS32Boundary { get; }

        /// <summary>顯示 Layer5 透明圖塊標記</summary>
        public bool ShowLayer5 { get; }

        /// <summary>是否在 Layer5 編輯模式</summary>
        public bool IsLayer5EditMode { get; }

        /// <summary>顯示座標標籤</summary>
        public bool ShowCoordinateLabels { get; }

        /// <summary>
        /// 建立渲染選項
        /// </summary>
        public RenderOptions(
            bool showLayer1 = true,
            bool showLayer2 = true,
            bool showLayer4 = true,
            bool showLayer3Attributes = false,
            bool showPassability = false,
            bool showSafeZones = false,
            bool showCombatZones = false,
            bool showMarketZones = false,
            bool showGrid = false,
            bool showS32Boundary = false,
            bool showLayer5 = false,
            bool isLayer5EditMode = false,
            bool showCoordinateLabels = false)
        {
            ShowLayer1 = showLayer1;
            ShowLayer2 = showLayer2;
            ShowLayer4 = showLayer4;
            ShowLayer3Attributes = showLayer3Attributes;
            ShowPassability = showPassability;
            ShowSafeZones = showSafeZones;
            ShowCombatZones = showCombatZones;
            ShowMarketZones = showMarketZones;
            ShowGrid = showGrid;
            ShowS32Boundary = showS32Boundary;
            ShowLayer5 = showLayer5;
            IsLayer5EditMode = isLayer5EditMode;
            ShowCoordinateLabels = showCoordinateLabels;
        }

        /// <summary>
        /// 從 ViewState 建立 RenderOptions
        /// </summary>
        public static RenderOptions FromViewState(ViewState viewState, bool isLayer5EditMode = false)
        {
            return new RenderOptions(
                showLayer1: viewState.ShowLayer1,
                showLayer2: true, // Layer2 通常與 Layer1 一起顯示
                showLayer4: viewState.ShowLayer4,
                showLayer3Attributes: viewState.ShowLayer3,
                showPassability: viewState.ShowPassability,
                showSafeZones: viewState.ShowSafeZones,
                showCombatZones: viewState.ShowCombatZones,
                showMarketZones: viewState.ShowMarketZones,
                showGrid: viewState.ShowGrid,
                showS32Boundary: viewState.ShowS32Boundary,
                showLayer5: false,
                isLayer5EditMode: isLayer5EditMode,
                showCoordinateLabels: false
            );
        }

        /// <summary>
        /// 預設選項（只顯示基本層）
        /// </summary>
        public static RenderOptions Default => new RenderOptions(
            showLayer1: true,
            showLayer2: true,
            showLayer4: true
        );

        /// <summary>
        /// 完整選項（顯示所有層和覆蓋層）
        /// </summary>
        public static RenderOptions Full => new RenderOptions(
            showLayer1: true,
            showLayer2: true,
            showLayer4: true,
            showLayer3Attributes: true,
            showPassability: true,
            showSafeZones: true,
            showCombatZones: true,
            showGrid: true,
            showS32Boundary: true
        );

        /// <summary>
        /// 檢查是否所有基本層都開啟（用於快取判斷）
        /// </summary>
        public bool AllBasicLayersEnabled => ShowLayer1 && ShowLayer2 && ShowLayer4;

        /// <summary>
        /// 檢查是否有任何覆蓋層開啟
        /// </summary>
        public bool HasOverlays => ShowLayer3Attributes || ShowPassability || ShowSafeZones ||
                                   ShowCombatZones || ShowMarketZones || ShowGrid || ShowS32Boundary || ShowLayer5 || ShowCoordinateLabels;
    }
}
