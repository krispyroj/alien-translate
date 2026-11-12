using System;
using System.Drawing;
using System.Windows.Forms;

namespace AlienTranslate
{
    internal sealed class PreviewWindow : Form
    {
        private const int PaddingSize = 10;
        private const int CloseButtonSize = 24;
        private const int CloseButtonSpace = CloseButtonSize + PaddingSize;
        private const TextFormatFlags TextFlags = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak
            | TextFormatFlags.TextBoxControl | TextFormatFlags.EndEllipsis;

        private readonly Timer dismissal = new Timer();
        private readonly Button closeButton;
        private string previewText = "";

        internal PreviewWindow()
        {
            Text = "Alien Translate";
            Font = new Font("Tahoma", 11);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = SystemColors.Info;
            ForeColor = SystemColors.InfoText;
            DoubleBuffered = true;
            closeButton = new Button
            {
                Text = "×",
                AccessibleName = "ปิดผลการแปลง",
                Size = new Size(CloseButtonSize, CloseButtonSize),
                FlatStyle = FlatStyle.Flat,
                BackColor = SystemColors.Info,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (sender, args) => Dismiss();
            Controls.Add(closeButton);
            dismissal.Tick += (sender, args) => Dismiss();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                const int noActivate = 0x08000000;
                parameters.ExStyle |= noActivate;
                return parameters;
            }
        }

        internal void Present(string text, Point anchor)
        {
            dismissal.Stop();
            const int previewLimit = 1000;
            previewText = text.Length <= previewLimit
                ? text
                : text.Substring(0, char.IsHighSurrogate(text[previewLimit - 1]) ? previewLimit - 1 : previewLimit) + "…";
            var area = Screen.FromPoint(anchor).WorkingArea;
            int maxWidth = Math.Max(1, Math.Min(480, area.Width - 2 * PaddingSize) - CloseButtonSpace);
            int maxHeight = Math.Min(Font.Height * 8, area.Height - 2 * PaddingSize);
            var measured = TextRenderer.MeasureText(previewText, Font, new Size(maxWidth, maxHeight), TextFlags);
            ClientSize = new Size(Math.Min(maxWidth, measured.Width) + 2 * PaddingSize + CloseButtonSpace,
                Math.Max(CloseButtonSize, Math.Min(maxHeight, measured.Height)) + 2 * PaddingSize);
            closeButton.Location = new Point(ClientSize.Width - PaddingSize - CloseButtonSize, PaddingSize);
            int x = Math.Max(area.Left, Math.Min(anchor.X + 12, area.Right - Width));
            int y = anchor.Y + 20;
            if (y + Height > area.Bottom)
                y = anchor.Y - Height - 10;
            Location = new Point(x, Math.Max(area.Top, y));
            Invalidate();
            Show();
            dismissal.Interval = Math.Min(9000, 4500 + Math.Min(text.Length, 150) * 30);
            dismissal.Start();
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            ControlPaint.DrawBorder(args.Graphics, ClientRectangle, SystemColors.ControlDark, ButtonBorderStyle.Solid);
            var bounds = Rectangle.Inflate(ClientRectangle, -PaddingSize, -PaddingSize);
            bounds.Width -= CloseButtonSpace;
            TextRenderer.DrawText(args.Graphics, previewText, Font, bounds, ForeColor, TextFlags);
        }

        private void Dismiss()
        {
            dismissal.Stop();
            Hide();
            previewText = "";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                dismissal.Dispose();
            base.Dispose(disposing);
        }
    }
}
