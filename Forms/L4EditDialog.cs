using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using L1MapViewer.Models;

namespace L1MapViewer.Forms
{
    /// <summary>
    /// L4 物件編輯對話框
    /// </summary>
    public class L4EditDialog : Form
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
            Size = new Size(420, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            int yOffset = 20;

            // 目標 S32
            Label lblTargetS32 = new Label();
            lblTargetS32.Text = "所屬 S32:";
            lblTargetS32.Location = new Point(20, yOffset);
            lblTargetS32.Size = new Size(120, 20);

            cmbTargetS32 = new ComboBox();
            cmbTargetS32.Location = new Point(150, yOffset - 2);
            cmbTargetS32.Size = new Size(230, 23);
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
            yOffset += 30;

            // 座標資訊標籤
            lblCoordInfo = new Label();
            lblCoordInfo.Location = new Point(20, yOffset);
            lblCoordInfo.Size = new Size(360, 20);
            lblCoordInfo.ForeColor = Color.Blue;
            UpdateCoordInfo();
            Controls.Add(lblCoordInfo);
            yOffset += 30;

            // 分隔線
            Label separator1 = new Label();
            separator1.BorderStyle = BorderStyle.Fixed3D;
            separator1.Location = new Point(20, yOffset);
            separator1.Size = new Size(360, 2);
            Controls.Add(separator1);
            yOffset += 15;

            // X 座標
            Label lblX = new Label();
            lblX.Text = "X (L1座標 0-255):";
            lblX.Location = new Point(20, yOffset);
            lblX.Size = new Size(120, 20);

            numX = new NumericUpDown();
            numX.Location = new Point(150, yOffset - 2);
            numX.Size = new Size(100, 23);
            numX.Minimum = 0;
            numX.Maximum = 255;
            numX.Value = obj.X;
            numX.ValueChanged += (s, e) => UpdateCoordInfo();

            Controls.Add(lblX);
            Controls.Add(numX);
            yOffset += 30;

            // Y 座標
            Label lblY = new Label();
            lblY.Text = "Y (L1座標 0-255):";
            lblY.Location = new Point(20, yOffset);
            lblY.Size = new Size(120, 20);

            numY = new NumericUpDown();
            numY.Location = new Point(150, yOffset - 2);
            numY.Size = new Size(100, 23);
            numY.Minimum = 0;
            numY.Maximum = 255;
            numY.Value = obj.Y;
            numY.ValueChanged += (s, e) => UpdateCoordInfo();

            Controls.Add(lblY);
            Controls.Add(numY);
            yOffset += 30;

            // 分隔線
            Label separator2 = new Label();
            separator2.BorderStyle = BorderStyle.Fixed3D;
            separator2.Location = new Point(20, yOffset);
            separator2.Size = new Size(360, 2);
            Controls.Add(separator2);
            yOffset += 15;

            // GroupId
            Label lblGroupId = new Label();
            lblGroupId.Text = "GroupId:";
            lblGroupId.Location = new Point(20, yOffset);
            lblGroupId.Size = new Size(120, 20);

            numGroupId = new NumericUpDown();
            numGroupId.Location = new Point(150, yOffset - 2);
            numGroupId.Size = new Size(100, 23);
            numGroupId.Minimum = 0;
            numGroupId.Maximum = 65535;
            numGroupId.Value = obj.GroupId;

            Controls.Add(lblGroupId);
            Controls.Add(numGroupId);
            yOffset += 30;

            // Layer
            Label lblLayer = new Label();
            lblLayer.Text = "Layer (渲染層):";
            lblLayer.Location = new Point(20, yOffset);
            lblLayer.Size = new Size(120, 20);

            numLayer = new NumericUpDown();
            numLayer.Location = new Point(150, yOffset - 2);
            numLayer.Size = new Size(100, 23);
            numLayer.Minimum = 0;
            numLayer.Maximum = 255;
            numLayer.Value = obj.Layer;

            Controls.Add(lblLayer);
            Controls.Add(numLayer);
            yOffset += 30;

            // TileId
            Label lblTileId = new Label();
            lblTileId.Text = "TileId:";
            lblTileId.Location = new Point(20, yOffset);
            lblTileId.Size = new Size(120, 20);

            numTileId = new NumericUpDown();
            numTileId.Location = new Point(150, yOffset - 2);
            numTileId.Size = new Size(100, 23);
            numTileId.Minimum = 0;
            numTileId.Maximum = 65535;
            numTileId.Value = obj.TileId;

            Controls.Add(lblTileId);
            Controls.Add(numTileId);
            yOffset += 30;

            // IndexId
            Label lblIndexId = new Label();
            lblIndexId.Text = "IndexId:";
            lblIndexId.Location = new Point(20, yOffset);
            lblIndexId.Size = new Size(120, 20);

            numIndexId = new NumericUpDown();
            numIndexId.Location = new Point(150, yOffset - 2);
            numIndexId.Size = new Size(100, 23);
            numIndexId.Minimum = 0;
            numIndexId.Maximum = 255;
            numIndexId.Value = obj.IndexId;

            Controls.Add(lblIndexId);
            Controls.Add(numIndexId);
            yOffset += 40;

            // 按鈕
            btnOK = new Button();
            btnOK.Text = "確定";
            btnOK.DialogResult = DialogResult.OK;
            btnOK.Location = new Point(150, yOffset);
            btnOK.Size = new Size(80, 30);

            btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new Point(250, yOffset);
            btnCancel.Size = new Size(80, 30);

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
                lblCoordInfo.ForeColor = Color.Blue;
                btnOK.Enabled = true;
            }
            else
            {
                // 座標超出範圍，禁止確定
                numX.Enabled = false;
                numY.Enabled = false;
                lblCoordInfo.Text = $"座標超出範圍! ({newLocalX}, {newLocalY}) - 無法移動到此 S32";
                lblCoordInfo.ForeColor = Color.Red;
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
