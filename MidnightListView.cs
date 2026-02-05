using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ModernUI
{
    public class MidnightListView : ListView
    {
        #region Native Methods (Scrollbar Dark & Fixes)
        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_ERASEBKGND = 0x14;
        private const int WM_PAINT = 0xF;
        #endregion

        #region Colors Configuration
        private Color _backColor = Color.FromArgb(30, 30, 30);
        private Color _foreColor = Color.FromArgb(230, 230, 230);
        private Color _headerColor = Color.FromArgb(35, 35, 35);
        private Color _headerForeColor = Color.FromArgb(180, 180, 180);
        private Color _selectionColor = Color.FromArgb(0, 120, 215);
        private Color _hoverColor = Color.FromArgb(50, 50, 52);
        private Color _borderColor = Color.FromArgb(60, 60, 60);
        private Color _accentColor = Color.FromArgb(0, 120, 215);
        #endregion

        private ListViewItem _hoveredItem;

        public MidnightListView()
        {
            this.View = View.Details;
            this.DoubleBuffered = true;
            this.OwnerDraw = true;
            this.FullRowSelect = true;
            this.BorderStyle = BorderStyle.None;
            this.HeaderStyle = ColumnHeaderStyle.Nonclickable;

            this.BackColor = _backColor;
            this.ForeColor = _foreColor;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            MethodInfo method = typeof(Control).GetMethod("SetStyle", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(this, new object[] { ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true });
            method.Invoke(this, new object[] { ControlStyles.EnableNotifyMessage, true });
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (Environment.OSVersion.Version.Major >= 10)
            {
                SetWindowTheme(this.Handle, "DarkMode_Explorer", null);
                int attribute = 20;
                int useDarkMode = 1;
                try { DwmSetWindowAttribute(this.Handle, attribute, ref useDarkMode, sizeof(int)); } catch { }
            }
        }

        protected override void OnNotifyMessage(Message m)
        {
            if (m.Msg != WM_ERASEBKGND)
            {
                base.OnNotifyMessage(m);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            ListViewItem item = this.GetItemAt(e.X, e.Y);
            if (item != _hoveredItem)
            {
                _hoveredItem = item;
                this.Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredItem = null;
            this.Invalidate();
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(_headerColor))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            // Draw subtle gradient effect
            using (System.Drawing.Drawing2D.LinearGradientBrush gradBrush = 
                new System.Drawing.Drawing2D.LinearGradientBrush(
                    e.Bounds, 
                    Color.FromArgb(10, 255, 255, 255),
                    Color.FromArgb(0, 0, 0, 0),
                    System.Drawing.Drawing2D.LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(gradBrush, e.Bounds);
            }

            using (Pen pen = new Pen(_borderColor))
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 5, e.Bounds.Right - 1, e.Bounds.Bottom - 5);
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            Rectangle textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

            TextRenderer.DrawText(e.Graphics, e.Header.Text, new Font("Segoe UI", 9.5f, FontStyle.Bold), textRect, _headerForeColor, flags);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
        }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = false;
            bool isSelected = e.Item.Selected;
            bool isHovered = (e.Item == _hoveredItem);

            if (e.ColumnIndex == 0)
            {
                Rectangle rowBounds = e.Item.Bounds;
                Brush bgBrush = new SolidBrush(_backColor);

                if (isSelected)
                {
                    bgBrush = new SolidBrush(_selectionColor);
                }
                else if (isHovered)
                {
                    bgBrush = new SolidBrush(_hoverColor);
                }

                using (bgBrush)
                {
                    e.Graphics.FillRectangle(bgBrush, rowBounds);
                }

                if (isSelected)
                {
                    Rectangle accentRect = new Rectangle(rowBounds.X, rowBounds.Y, 4, rowBounds.Height);
                    using (SolidBrush accentBrush = new SolidBrush(Color.White))
                    {
                        e.Graphics.FillRectangle(accentBrush, accentRect);
                    }
                }
            }
            else
            {

            }

            Color textColor = _foreColor;
            if (isSelected) textColor = Color.White;

            string text = (e.ColumnIndex == 0) ? e.Item.Text : e.SubItem.Text;

            Rectangle textRect = e.Bounds;
            textRect.X += 10;
            textRect.Width -= 10;

            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

            TextRenderer.DrawText(e.Graphics, text, this.Font, textRect, textColor, flags);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Invalidate();
        }
    }
}