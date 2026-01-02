using System;
using System.Collections.Generic;
using System.Windows.Forms;
using L1MapViewer.Models;

namespace L1MapViewer
{
    /// <summary>
    /// L5 新增/編輯對話框
    /// </summary>
    public class L5EditDialog : Form
    {
        private NumericUpDown numObjectIndex;
        private NumericUpDown numType;
        private NumericUpDown numX;
        private NumericUpDown numY;
        private ComboBox cmbTargetS32;
        private Label lblCoordInfo;
        private Button btnOK;
        private Button btnCancel;

        private S32Data _originalS32;
        private int _originalX;
        private int _originalY;

        public int ObjectIndex => (int)numObjectIndex.Value;
        public byte L5Type => (byte)numType.Value;
        public byte NewX => numX != null ? (byte)numX.Value : (byte)0;
        public byte NewY => numY != null ? (byte)numY.Value : (byte)0;
        public S32Data SelectedS32 => (cmbTargetS32?.SelectedItem as S32ComboItem)?.S32;
        public bool S32Changed { get; private set; } = false;

        /// <summary>
        /// 新增 L5 用的建構函式
        /// </summary>
        public L5EditDialog(int objectIndex, byte type, bool isNew)
            : this(objectIndex, type, 0, 0, isNew, null, null)
        {
        }

        /// <summary>
        /// 編輯 L5 用的建構函式（含 S32 選擇和座標）
        /// </summary>
        public L5EditDialog(int objectIndex, byte type, byte x, byte y, bool isNew, S32Data currentS32, IEnumerable<S32Data> availableS32s)
        {
            _originalS32 = currentS32;
            _originalX = x;
            _originalY = y;

            Text = isNew ? "新增 L5" : "編輯 L5";
            bool showS32Selector = !isNew && availableS32s != null;
            int dialogHeight = showS32Selector ? 340 : 210;
            Size = new System.Drawing.Size(380, dialogHeight);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            int yOffset = 0;

            // 目標 S32（僅編輯模式顯示）
            if (showS32Selector)
            {
                Label lblTargetS32 = new Label();
                lblTargetS32.Text = "目標 S32:";
                lblTargetS32.Location = new System.Drawing.Point(20, 20);
                lblTargetS32.Size = new System.Drawing.Size(150, 20);

                cmbTargetS32 = new ComboBox();
                cmbTargetS32.Location = new System.Drawing.Point(180, 18);
                cmbTargetS32.Size = new System.Drawing.Size(160, 23);
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

                // 座標資訊標籤
                lblCoordInfo = new Label();
                lblCoordInfo.Location = new System.Drawing.Point(20, 48);
                lblCoordInfo.Size = new System.Drawing.Size(330, 20);
                lblCoordInfo.ForeColor = System.Drawing.Color.Blue;
                UpdateCoordInfo();

                cmbTargetS32.SelectedIndexChanged += (s, e) =>
                {
                    var selectedItem = cmbTargetS32.SelectedItem as S32ComboItem;
                    S32Changed = (selectedItem?.S32 != _originalS32);
                    RecalculateCoordinates();
                };

                Controls.Add(lblTargetS32);
                Controls.Add(cmbTargetS32);
                Controls.Add(lblCoordInfo);
                yOffset = 55;

                // L1 X 座標（可超出 S32 邊界）
                Label lblX = new Label();
                lblX.Text = "L1 X (0-255):";
                lblX.Location = new System.Drawing.Point(20, 20 + yOffset);
                lblX.Size = new System.Drawing.Size(150, 20);

                numX = new NumericUpDown();
                numX.Location = new System.Drawing.Point(180, 18 + yOffset);
                numX.Size = new System.Drawing.Size(160, 23);
                numX.Minimum = 0;
                numX.Maximum = 255;
                numX.Value = x;

                // L1 Y 座標（可超出 S32 邊界）
                Label lblY = new Label();
                lblY.Text = "L1 Y (0-255):";
                lblY.Location = new System.Drawing.Point(20, 55 + yOffset);
                lblY.Size = new System.Drawing.Size(150, 20);

                numY = new NumericUpDown();
                numY.Location = new System.Drawing.Point(180, 53 + yOffset);
                numY.Size = new System.Drawing.Size(160, 23);
                numY.Minimum = 0;
                numY.Maximum = 255;
                numY.Value = y;

                Controls.AddRange(new Control[] { lblX, numX, lblY, numY });
                yOffset += 70;
            }

            // L4群組 (ObjectIndex)
            Label lblObjectIndex = new Label();
            lblObjectIndex.Text = "L4群組 (ObjectIndex):";
            lblObjectIndex.Location = new System.Drawing.Point(20, 20 + yOffset);
            lblObjectIndex.Size = new System.Drawing.Size(150, 20);

            numObjectIndex = new NumericUpDown();
            numObjectIndex.Location = new System.Drawing.Point(180, 18 + yOffset);
            numObjectIndex.Size = new System.Drawing.Size(160, 23);
            numObjectIndex.Minimum = 0;
            numObjectIndex.Maximum = 65535;
            numObjectIndex.Value = objectIndex;

            // Type
            Label lblType = new Label();
            lblType.Text = "Type:";
            lblType.Location = new System.Drawing.Point(20, 55 + yOffset);
            lblType.Size = new System.Drawing.Size(150, 20);

            numType = new NumericUpDown();
            numType.Location = new System.Drawing.Point(180, 53 + yOffset);
            numType.Size = new System.Drawing.Size(160, 23);
            numType.Minimum = 0;
            numType.Maximum = 255;
            numType.Value = type;

            // 說明
            Label lblHint = new Label();
            lblHint.Text = "Type: 0=半透明, 1=消失";
            lblHint.Location = new System.Drawing.Point(20, 85 + yOffset);
            lblHint.Size = new System.Drawing.Size(330, 20);
            lblHint.ForeColor = System.Drawing.Color.Gray;

            // 按鈕
            btnOK = new Button();
            btnOK.Text = "確定";
            btnOK.DialogResult = DialogResult.OK;
            btnOK.Location = new System.Drawing.Point(120, 115 + yOffset);
            btnOK.Size = new System.Drawing.Size(75, 28);

            btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new System.Drawing.Point(210, 115 + yOffset);
            btnCancel.Size = new System.Drawing.Size(75, 28);

            AcceptButton = btnOK;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[] {
                lblObjectIndex, numObjectIndex,
                lblType, numType,
                lblHint,
                btnOK, btnCancel
            });
        }

        private void UpdateCoordInfo()
        {
            if (lblCoordInfo == null || _originalS32 == null) return;
            int globalX = _originalS32.SegInfo.nLinBeginX * 2 + _originalX;
            int globalY = _originalS32.SegInfo.nLinBeginY + _originalY;
            lblCoordInfo.Text = $"全域座標: ({globalX}, {globalY})";
        }

        private void RecalculateCoordinates()
        {
            if (numX == null || numY == null || _originalS32 == null) return;

            var selectedItem = cmbTargetS32.SelectedItem as S32ComboItem;
            if (selectedItem == null) return;

            var targetS32 = selectedItem.S32;

            // 計算原始的全域座標
            int globalX = _originalS32.SegInfo.nLinBeginX * 2 + _originalX;
            int globalY = _originalS32.SegInfo.nLinBeginY + _originalY;

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
                lblCoordInfo.Text = $"全域座標: ({globalX}, {globalY}) → 新本地座標: ({newLocalX}, {newLocalY})";
                lblCoordInfo.ForeColor = System.Drawing.Color.Blue;
                btnOK.Enabled = true;
            }
            else
            {
                // 座標為負數，禁止確定
                numX.Enabled = false;
                numY.Enabled = false;
                lblCoordInfo.Text = $"座標為負數! ({newLocalX}, {newLocalY}) - 無法移動到此 S32";
                lblCoordInfo.ForeColor = System.Drawing.Color.Red;
                btnOK.Enabled = false;
            }
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
