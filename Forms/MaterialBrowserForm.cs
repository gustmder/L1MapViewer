using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.CLI;
using L1MapViewer.Compatibility;
using L1MapViewer.Controls;
using L1MapViewer.Helper;
using L1MapViewer.Localization;
using L1MapViewer.Models;
using NLog;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// 素材庫瀏覽器
    /// </summary>
    public class MaterialBrowserForm : WinFormsDialog
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 選取的素材
        /// </summary>
        public Fs3pData SelectedMaterial { get; private set; }

        /// <summary>
        /// 選取的素材檔案路徑
        /// </summary>
        public string SelectedFilePath { get; private set; }

        private MaterialLibrary _library;
        private IconTextListControl materialGrid;
        private PictureBox pbPreview;
        private Label lblInfo;
        private TextBox txtSearch;
        private Button btnSearch;
        private Button btnOpen;
        private Button btnDelete;
        private Button btnSetPath;
        private Button btnOK;
        private Button btnCancel;
        private ImageList imageList;
        private Label lblPath;
        private Label lblCount;

        public MaterialBrowserForm()
        {
            _library = new MaterialLibrary();
            InitializeComponents();
            LoadMaterials();
            UpdateLocalization();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (this.GetInvokeRequired())
                this.Invoke(new Action(() => UpdateLocalization()));
            else
                UpdateLocalization();
        }

        private void InitializeComponents()
        {
            Title = "Material Browser";
            Size = new Size(800, 600);
            MinimumSize = new Size(600, 400);
            Resizable = true;

            // === 搜尋列 ===
            txtSearch = new TextBox
            {
                PlaceholderText = "搜尋素材...",
                Width = 200
            };
            txtSearch.KeyDown += (s, e) => { if (e.GetKeyCode() == Keys.Enter) SearchMaterials(); };

            btnSearch = new Button { Text = "搜尋" };
            btnSearch.Click += (s, e) => SearchMaterials();

            lblPath = new Label
            {
                Text = "",
                VerticalAlignment = VerticalAlignment.Center
            };

            btnSetPath = new Button { Text = "變更路徑" };
            btnSetPath.Click += BtnSetPath_Click;

            var searchBar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Padding = new Padding(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Items =
                {
                    txtSearch,
                    btnSearch,
                    new StackLayoutItem(lblPath, true),
                    btnSetPath
                }
            };

            // === 左側：素材網格 ===
            materialGrid = new IconTextListControl
            {
                TileWidth = 90,
                TileHeight = 105,
                ImageSize = 64,
                TilePadding = 4
            };
            materialGrid.SelectionChanged += MaterialGrid_SelectionChanged;
            materialGrid.ItemDoubleClick += MaterialGrid_DoubleClick;
            materialGrid.MouseUp += MaterialGrid_MouseUp;

            imageList = new ImageList
            {
                ImageSize = new Size(64, 64),
                ColorDepth = ColorDepth.Depth32Bit
            };
            materialGrid.LargeImageList = imageList;

            // === 右側：預覽和資訊 ===
            pbPreview = new PictureBox
            {
                Size = new Size(220, 220),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            lblInfo = new Label
            {
                Text = LocalizationManager.L("Material_SelectToView")
            };

            var rightPanel = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(0, 8),
                Rows =
                {
                    new TableRow(pbPreview),
                    new TableRow(new TableCell(lblInfo, true)) { ScaleHeight = true }
                }
            };

            // === 分割面板 ===
            var splitter = new Eto.Forms.Splitter
            {
                Orientation = Orientation.Horizontal,
                Position = 530,
                FixedPanel = SplitterFixedPanel.Panel2,
                Panel1 = materialGrid,
                Panel2 = rightPanel
            };

            // === 底部按鈕列 ===
            btnOpen = new Button { Text = "開啟檔案..." };
            btnOpen.Click += BtnOpen_Click;

            btnDelete = new Button { Text = "刪除", Enabled = false };
            btnDelete.Click += BtnDelete_Click;

            lblCount = new Label
            {
                Text = "",
                VerticalAlignment = VerticalAlignment.Center
            };

            btnOK = new Button { Text = "使用", Enabled = false };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button { Text = "取消" };
            btnCancel.Click += (s, e) => Close();

            var buttonBar = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Padding = new Padding(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Items =
                {
                    btnOpen,
                    btnDelete,
                    lblCount,
                    null,
                    btnOK,
                    btnCancel
                }
            };

            // === 主版面 (Eto 原生 TableLayout) ===
            Content = new TableLayout
            {
                Spacing = new Size(0, 0),
                Rows =
                {
                    new TableRow(searchBar),
                    new TableRow(new TableCell(splitter, true)) { ScaleHeight = true },
                    new TableRow(buttonBar)
                }
            };

            AbortButton = btnCancel;
        }

        private void LoadMaterials()
        {
            materialGrid.Items.Clear();
            imageList.Images.Clear();

            var materials = _library.GetAllMaterials();
            int imageIndex = 0;

            materialGrid.BeginUpdate();
            foreach (var info in materials)
            {
                var item = new IconTextListItem
                {
                    Text = info.Name ?? "未命名",
                    Tag = info.FilePath
                };

                // 添加縮圖
                if (info.ThumbnailPng != null && info.ThumbnailPng.Length > 0)
                {
                    try
                    {
                        using (var ms = new MemoryStream(info.ThumbnailPng))
                        {
                            var thumb = new Bitmap(ms);
                            imageList.Images.Add(thumb);
                            item.ImageIndex = imageIndex++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"LoadMaterials: Failed to load thumbnail for {info.Name}");
                        item.ImageIndex = -1;
                    }
                }
                else
                {
                    item.ImageIndex = -1;
                }

                materialGrid.Items.Add(item);
            }
            materialGrid.EndUpdate();

            UpdatePathLabel(materials.Count);
        }

        private void SearchMaterials()
        {
            string keyword = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                LoadMaterials();
                return;
            }

            materialGrid.Items.Clear();
            imageList.Images.Clear();

            var results = _library.Search(keyword);
            int imageIndex = 0;

            materialGrid.BeginUpdate();
            foreach (var info in results)
            {
                var item = new IconTextListItem
                {
                    Text = info.Name ?? "未命名",
                    Tag = info.FilePath
                };

                if (info.ThumbnailPng != null && info.ThumbnailPng.Length > 0)
                {
                    try
                    {
                        using (var ms = new MemoryStream(info.ThumbnailPng))
                        {
                            var thumb = new Bitmap(ms);
                            imageList.Images.Add(thumb);
                            item.ImageIndex = imageIndex++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"SearchMaterials: Failed to load thumbnail for {info.Name}");
                        item.ImageIndex = -1;
                    }
                }
                else
                {
                    item.ImageIndex = -1;
                }

                materialGrid.Items.Add(item);
            }
            materialGrid.EndUpdate();

            UpdatePathLabel(results.Count);
        }

        private void MaterialGrid_SelectionChanged(object sender, EventArgs e)
        {
            var selectedItem = materialGrid.SelectedItem;
            if (selectedItem == null)
            {
                pbPreview.Image = null;
                lblInfo.Text = LocalizationManager.L("Material_SelectToView");
                btnOK.Enabled = false;
                btnDelete.Enabled = false;
                SelectedFilePath = null;
                return;
            }

            string filePath = selectedItem.Tag as string;
            SelectedFilePath = filePath;

            try
            {
                var info = Fs3pParser.GetInfo(filePath);
                if (info != null)
                {
                    // 顯示縮圖
                    if (info.ThumbnailPng != null && info.ThumbnailPng.Length > 0)
                    {
                        using (var ms = new MemoryStream(info.ThumbnailPng))
                        {
                            pbPreview.Image?.Dispose();
                            pbPreview.Image = new Bitmap(ms);
                        }
                    }
                    else
                    {
                        pbPreview.Image?.Dispose();
                        pbPreview.Image = null;
                    }

                    // 顯示資訊
                    lblInfo.Text = $"{LocalizationManager.L("Material_InfoName")}: {info.Name}\n" +
                                   $"{LocalizationManager.L("Material_InfoSize")}: {info.Width} x {info.Height}\n" +
                                   $"{LocalizationManager.L("Material_InfoLayers")}: {GetLayerDescription(info.LayerFlags)}\n" +
                                   $"{LocalizationManager.L("Material_InfoFileSize")}: {info.FileSize / 1024.0:F1} KB\n" +
                                   $"{LocalizationManager.L("Material_InfoFile")}: {Path.GetFileName(filePath)}";

                    btnOK.Enabled = true;
                    btnDelete.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"MaterialGrid_SelectionChanged: Failed to load info for {filePath}");
                lblInfo.Text = $"{LocalizationManager.L("Material_CannotRead")}:\n{ex.Message}";
                btnOK.Enabled = false;
                btnDelete.Enabled = true;
            }
        }

        private string GetLayerDescription(ushort flags)
        {
            var layers = new List<string>();
            if ((flags & 0x01) != 0) layers.Add("L1");
            if ((flags & 0x02) != 0) layers.Add("L2");
            if ((flags & 0x04) != 0) layers.Add("L3");
            if ((flags & 0x08) != 0) layers.Add("L4");
            return layers.Count > 0 ? string.Join(", ", layers) : LocalizationManager.L("Material_NoLayers");
        }

        private void MaterialGrid_DoubleClick(object sender, EventArgs e)
        {
            if (materialGrid.SelectedItem != null)
            {
                UseMaterial();
            }
        }

        private void MaterialGrid_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Buttons != Eto.Forms.MouseButtons.Alternate)
                return;

            if (materialGrid.SelectedItem == null)
                return;

            var menu = new ContextMenuStrip();

            var menuUse = new ToolStripMenuItem(LocalizationManager.L("Material_UseThis"));
            menuUse.Click += (s, ev) => UseMaterial();
            menu.Items.Add(menuUse);

            menu.Items.Add(new ToolStripSeparator());

            var menuDelete = new ToolStripMenuItem(LocalizationManager.L("Button_Delete"));
            menuDelete.Click += (s, ev) => BtnDelete_Click(s, ev);
            menu.Items.Add(menuDelete);

            var menuOpenFolder = new ToolStripMenuItem(LocalizationManager.L("Material_OpenFolder"));
            menuOpenFolder.Click += (s, ev) => OpenContainingFolder();
            menu.Items.Add(menuOpenFolder);

            menu.Items.Add(new ToolStripSeparator());

            var menuRefresh = new ToolStripMenuItem(LocalizationManager.L("Button_Refresh"));
            menuRefresh.Click += (s, ev) => { _library.ClearCache(); LoadMaterials(); };
            menu.Items.Add(menuRefresh);

            menu.Show(materialGrid, e.Location);
        }

        /// <summary>
        /// 使用選取的素材 - 載入並關閉對話框
        /// </summary>
        private void UseMaterial()
        {
            if (string.IsNullOrEmpty(SelectedFilePath))
            {
                WinFormsMessageBox.Show(
                    LocalizationManager.L("Material_PleaseSelect"),
                    LocalizationManager.L("Dialog_Hint"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                SelectedMaterial = _library.LoadMaterial(SelectedFilePath);
                if (SelectedMaterial == null)
                {
                    WinFormsMessageBox.Show(
                        LocalizationManager.L("Material_CannotLoad"),
                        LocalizationManager.L("Dialog_Error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 成功載入，關閉對話框
                DialogResult = DialogResult.Ok;
                Close();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"UseMaterial: Failed to load material from {SelectedFilePath}");
                WinFormsMessageBox.Show(
                    $"{LocalizationManager.L("Material_LoadFailed")}: {ex.Message}",
                    LocalizationManager.L("Dialog_Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            UseMaterial();
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "素材檔案|*.fs32p|所有檔案|*.*";
                ofd.Title = LocalizationManager.L("Material_OpenFileTitle");

                if (ofd.ShowDialog(this) == DialogResult.Ok)
                {
                    try
                    {
                        SelectedFilePath = ofd.FileName;
                        SelectedMaterial = Fs3pParser.ParseFile(ofd.FileName);

                        if (SelectedMaterial != null)
                        {
                            DialogResult = DialogResult.Ok;
                            Close();
                        }
                        else
                        {
                            WinFormsMessageBox.Show(
                                LocalizationManager.L("Material_CannotParse"),
                                LocalizationManager.L("Dialog_Error"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"BtnOpen_Click: Failed to open material {ofd.FileName}");
                        WinFormsMessageBox.Show(
                            $"{LocalizationManager.L("Material_OpenFailed")}: {ex.Message}",
                            LocalizationManager.L("Dialog_Error"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedFilePath))
                return;

            string materialName = materialGrid.SelectedItem?.Text ?? Path.GetFileName(SelectedFilePath);

            var result = WinFormsMessageBox.Show(
                string.Format(LocalizationManager.L("Material_ConfirmDelete"), materialName),
                LocalizationManager.L("Dialog_ConfirmDelete"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                if (_library.DeleteMaterial(SelectedFilePath))
                {
                    SelectedFilePath = null;
                    LoadMaterials();
                }
                else
                {
                    WinFormsMessageBox.Show(
                        LocalizationManager.L("Material_DeleteFailed"),
                        LocalizationManager.L("Dialog_Error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnSetPath_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = LocalizationManager.L("Material_SelectFolder");
                fbd.SelectedPath = _library.LibraryPath;

                if (fbd.ShowDialog(this) == DialogResult.Ok)
                {
                    _library.LibraryPath = fbd.SelectedPath;
                    _library.ClearCache();
                    LoadMaterials();
                }
            }
        }

        private void OpenContainingFolder()
        {
            if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
                return;

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{SelectedFilePath}\"");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"OpenContainingFolder: Failed for {SelectedFilePath}");
            }
        }

        private void UpdatePathLabel(int count)
        {
            lblPath.Text = _library.LibraryPath;
            lblCount.Text = $"{count} {LocalizationManager.L("Material_Items")}";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pbPreview.Image?.Dispose();
                imageList?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void UpdateLocalization()
        {
            Text = LocalizationManager.L("Form_MaterialBrowser_Title");

            btnSearch.Text = LocalizationManager.L("Button_Search");
            btnSetPath.Text = LocalizationManager.L("Material_ChangePath");
            btnOpen.Text = LocalizationManager.L("Material_OpenFile");
            btnDelete.Text = LocalizationManager.L("Button_Delete");
            btnOK.Text = LocalizationManager.L("Material_Use");
            btnCancel.Text = LocalizationManager.L("Button_Cancel");
            txtSearch.PlaceholderText = LocalizationManager.L("Material_SearchPlaceholder");

            lblInfo.Text = LocalizationManager.L("Material_SelectToView");

            var materials = _library.GetAllMaterials();
            UpdatePathLabel(materials.Count);
        }
    }
}
