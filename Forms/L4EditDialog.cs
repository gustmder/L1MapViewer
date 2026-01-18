using System;
using System.Collections.Generic;
// using System.Drawing; // Replaced with Eto.Drawing
using Eto.Forms;
using Eto.Drawing;
using L1MapViewer.Compatibility;
using L1MapViewer.Models;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// L4 物件編輯對話框
    /// </summary>
    public class L4EditDialog : WinFormsDialog
    {
        private NumericUpDown numGroupId;
        private NumericUpDown numX;
        private NumericUpDown numY;
        private NumericUpDown numLayer;
        private NumericUpDown numIndexId;
        private NumericUpDown numTileId;
        private ComboBox cmbTargetS32;
        private Label lblCoordInfo;
        private Button btnOK;
        private Button btnCancel;

        private S32Data _originalS32;
        private ObjectTile _originalObject;

        public int GroupId => (int)numGroupId.Value;
        public int NewX => (int)numX.Value;
        public int NewY => (int)numY.Value;
        public int Layer => (int)numLayer.Value;
        public int IndexId => (int)numIndexId.Value;
        public int TileId => (int)numTileId.Value;
        public S32Data SelectedS32 => (cmbTargetS32?.SelectedItem as S32ComboItem)?.S32;
        public bool S32Changed { get; private set; } = false;

        /// <summary>
        /// L4 編輯對話框
        /// </summary>
        /// <param name="obj">要編輯的物件</param>
        /// <param name="currentS32">物件所屬的 S32</param>
        /// <param name="availableS32s">可用的 S32 清單</param>
        public L4EditDialog(ObjectTile obj, S32Data currentS32, IEnumerable<S32Data> availableS32s)
        {
            _originalS32 = currentS32;
            _originalObject = obj;

            Text = "編輯 L4 物件";
            Size = new Size(450, 480);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            // 布局常數
            const int leftMargin = 25;
            const int labelWidth = 130;
            const int inputX = 165;
            const int inputWidth = 100;
            const int rowHeight = 35;
            int yOffset = 20;

            // 目標 S32
            Label lblTargetS32 = new Label();
            lblTargetS32.Text = "所屬 S32:";
            lblTargetS32.SetLocation(new Point(leftMargin, yOffset + 3));
            lblTargetS32.Size = new Size(labelWidth, 20);

            cmbTargetS32 = new ComboBox();
            cmbTargetS32.SetLocation(new Point(inputX, yOffset));
            cmbTargetS32.Size = new Size(240, 25);
            cmbTargetS32.DropDownStyle = ComboBoxStyle.DropDownList;

            foreach (var s32 in availableS32s)
            {
                string displayName = System.IO.Path.GetFileName(s32.FilePath);
                cmbTargetS32.Items.Add(new S32ComboItem { S32 = s32, DisplayName = displayName });
                if (s32 == currentS32)
                {
                    cmbTargetS32.SelectedIndex = cmbTargetS32.Items.Count - 1;
                }
            }

            cmbTargetS32.SelectedIndexChanged += (s, e) =>
            {
                var selectedItem = cmbTargetS32.SelectedItem as S32ComboItem;
                S32Changed = (selectedItem?.S32 != _originalS32);
                RecalculateCoordinates();
            };

            Controls.Add(lblTargetS32);
            Controls.Add(cmbTargetS32);
            yOffset += rowHeight;

            // 座標資訊標籤
            lblCoordInfo = new Label();
            lblCoordInfo.SetLocation(new Point(leftMargin, yOffset));
            lblCoordInfo.Size = new Size(380, 22);
            lblCoordInfo.TextColor = Colors.Blue;
            UpdateCoordInfo();
            Controls.Add(lblCoordInfo);
            yOffset += 30;

            // 分隔線
            Label separator1 = new Label();
            separator1.BorderStyle = BorderStyle.Fixed3D;
            separator1.SetLocation(new Point(leftMargin, yOffset));
            separator1.Size = new Size(380, 2);
            Controls.Add(separator1);
            yOffset += 18;

            // X 座標
            Label lblX = new Label();
            lblX.Text = "X (L1座標 0-255):";
            lblX.SetLocation(new Point(leftMargin, yOffset + 3));
            lblX.Size = new Size(labelWidth, 20);

            numX = new NumericUpDown();
            numX.SetLocation(new Point(inputX, yOffset));
            numX.Size = new Size(inputWidth, 25);
            numX.Minimum = 0;
            numX.Maximum = 255;
            numX.Value = obj.X;
            numX.ValueChanged += (s, e) => UpdateCoordInfo();

            Controls.Add(lblX);
            Controls.Add(numX);
            yOffset += rowHeight;

            // Y 座標
            Label lblY = new Label();
            lblY.Text = "Y (L1座標 0-255):";
            lblY.SetLocation(new Point(leftMargin, yOffset + 3));
            lblY.Size = new Size(labelWidth, 20);

            numY = new NumericUpDown();
            numY.SetLocation(new Point(inputX, yOffset));
            numY.Size = new Size(inputWidth, 25);
            numY.Minimum = 0;
            numY.Maximum = 255;
            numY.Value = obj.Y;
            numY.ValueChanged += (s, e) => UpdateCoordInfo();

            Controls.Add(lblY);
            Controls.Add(numY);
            yOffset += rowHeight;

            // 分隔線
            Label separator2 = new Label();
            separator2.BorderStyle = BorderStyle.Fixed3D;
            separator2.SetLocation(new Point(leftMargin, yOffset));
            separator2.Size = new Size(380, 2);
            Controls.Add(separator2);
            yOffset += 18;

            // GroupId
            Label lblGroupId = new Label();
            lblGroupId.Text = "GroupId:";
            lblGroupId.SetLocation(new Point(leftMargin, yOffset + 3));
            lblGroupId.Size = new Size(labelWidth, 20);

            numGroupId = new NumericUpDown();
            numGroupId.SetLocation(new Point(inputX, yOffset));
            numGroupId.Size = new Size(inputWidth, 25);
            numGroupId.Minimum = 0;
            numGroupId.Maximum = 65535;
            numGroupId.Value = obj.GroupId;

            Controls.Add(lblGroupId);
            Controls.Add(numGroupId);
            yOffset += rowHeight;

            // Layer
            Label lblLayer = new Label();
            lblLayer.Text = "Layer (高度):";
            lblLayer.SetLocation(new Point(leftMargin, yOffset + 3));
            lblLayer.Size = new Size(labelWidth, 20);

            numLayer = new NumericUpDown();
            numLayer.SetLocation(new Point(inputX, yOffset));
            numLayer.Size = new Size(inputWidth, 25);
            numLayer.Minimum = 0;
            numLayer.Maximum = 255;
            numLayer.Value = obj.Layer;

            Controls.Add(lblLayer);
            Controls.Add(numLayer);
            yOffset += rowHeight;

            // TileId
            Label lblTileId = new Label();
            lblTileId.Text = "TileId:";
            lblTileId.SetLocation(new Point(leftMargin, yOffset + 3));
            lblTileId.Size = new Size(labelWidth, 20);

            numTileId = new NumericUpDown();
            numTileId.SetLocation(new Point(inputX, yOffset));
            numTileId.Size = new Size(inputWidth, 25);
            numTileId.Minimum = 0;
            numTileId.Maximum = 65535;
            numTileId.Value = obj.TileId;

            Controls.Add(lblTileId);
            Controls.Add(numTileId);
            yOffset += rowHeight;

            // IndexId
            Label lblIndexId = new Label();
            lblIndexId.Text = "IndexId:";
            lblIndexId.SetLocation(new Point(leftMargin, yOffset + 3));
            lblIndexId.Size = new Size(labelWidth, 20);

            numIndexId = new NumericUpDown();
            numIndexId.SetLocation(new Point(inputX, yOffset));
            numIndexId.Size = new Size(inputWidth, 25);
            numIndexId.Minimum = 0;
            numIndexId.Maximum = 255;
            numIndexId.Value = obj.IndexId;

            Controls.Add(lblIndexId);
            Controls.Add(numIndexId);
            yOffset += rowHeight + 15;

            // 按鈕
            btnOK = new Button();
            btnOK.Text = "確定";
            btnOK.DialogResult = DialogResult.Ok;
            btnOK.SetLocation(new Point(inputX, yOffset));
            btnOK.Size = new Size(90, 32);

            btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.SetLocation(new Point(inputX + 110, yOffset));
            btnCancel.Size = new Size(90, 32);

            AcceptButton = btnOK;
            CancelButton = btnCancel;

            Controls.Add(btnOK);
            Controls.Add(btnCancel);
        }

        private void UpdateCoordInfo()
        {
            if (lblCoordInfo == null) return;

            var selectedItem = cmbTargetS32?.SelectedItem as S32ComboItem;
            var s32 = selectedItem?.S32 ?? _originalS32;
            if (s32 == null) return;

            int localX = (int)(numX?.Value ?? _originalObject.X);
            int localY = (int)(numY?.Value ?? _originalObject.Y);
            int globalX = s32.SegInfo.nLinBeginX * 2 + localX;
            int globalY = s32.SegInfo.nLinBeginY + localY;
            int gameX = globalX / 2;
            int gameY = globalY;

            lblCoordInfo.Text = $"全域L1座標: ({globalX}, {globalY}) | 遊戲座標: ({gameX}, {gameY})";
        }

        private void RecalculateCoordinates()
        {
            if (numX == null || numY == null || _originalS32 == null) return;

            var selectedItem = cmbTargetS32.SelectedItem as S32ComboItem;
            if (selectedItem == null) return;

            var targetS32 = selectedItem.S32;

            // 計算原始的全域座標
            int globalX = _originalS32.SegInfo.nLinBeginX * 2 + _originalObject.X;
            int globalY = _originalS32.SegInfo.nLinBeginY + _originalObject.Y;

            // 計算目標 S32 中的本地座標
            int newLocalX = globalX - targetS32.SegInfo.nLinBeginX * 2;
            int newLocalY = globalY - targetS32.SegInfo.nLinBeginY;

            // 座標必須 >= 0 且 <= 255（byte 範圍）
            bool isValid = newLocalX >= 0 && newLocalX <= 255 && newLocalY >= 0 && newLocalY <= 255;

            if (isValid)
            {
                numX.Value = newLocalX;
                numY.Value = newLocalY;
                numX.Enabled = true;
                numY.Enabled = true;
                lblCoordInfo.TextColor = Colors.Blue;
                btnOK.Enabled = true;
            }
            else
            {
                // 座標超出範圍，禁止確定
                numX.Enabled = false;
                numY.Enabled = false;
                lblCoordInfo.Text = $"座標超出範圍! ({newLocalX}, {newLocalY}) - 無法移動到此 S32";
                lblCoordInfo.TextColor = Colors.Red;
                btnOK.Enabled = false;
            }

            UpdateCoordInfo();
        }

        /// <summary>
        /// ComboBox 項目包裝類
        /// </summary>
        private class S32ComboItem
        {
            public S32Data S32 { get; set; }
            public string DisplayName { get; set; }
            public override string ToString() => DisplayName;
        }
    }
}
