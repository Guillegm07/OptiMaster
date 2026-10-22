using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OptiGuille
{
    internal static class UiTheme
    {
        public static readonly Color BgSidebar = Color.FromArgb(9, 13, 22);
        public static readonly Color BgCanvas = Color.FromArgb(13, 18, 30);
        public static readonly Color CardBg = Color.FromArgb(20, 27, 45);
        public static readonly Color CardAlt = Color.FromArgb(16, 22, 37);
        public static readonly Color CardHover = Color.FromArgb(28, 38, 62);
        public static readonly Color Border = Color.FromArgb(36, 49, 78);
        public static readonly Color BorderBright = Color.FromArgb(56, 139, 253);
        public static readonly Color Primary = Color.FromArgb(59, 130, 246);
        public static readonly Color Cyan = Color.FromArgb(6, 182, 212);
        public static readonly Color Mint = Color.FromArgb(16, 185, 129);
        public static readonly Color Purple = Color.FromArgb(139, 92, 246);
        public static readonly Color Amber = Color.FromArgb(245, 158, 11);
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);
        public static readonly Color TextLight = Color.FromArgb(248, 250, 252);
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
        public static readonly Color TrackDark = Color.FromArgb(10, 14, 24);
    }

    internal static class DwmNative
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void ApplyModernFrame(Form form)
        {
            try
            {
                int preference = 2;
                DwmSetWindowAttribute(form.Handle, 33, ref preference, sizeof(int));
                int borderColor = ColorTranslator.ToWin32(UiTheme.Border);
                DwmSetWindowAttribute(form.Handle, 34, ref borderColor, sizeof(int));
            }
            catch
            {
                form.Region = new Region(UiShape.Round(new Rectangle(0, 0, form.Width, form.Height), 12));
            }
        }
    }

    internal static class UiShape
    {
        internal static GraphicsPath Round(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            if (r.Width < d) d = Math.Max(2, r.Width);
            if (r.Height < d) d = Math.Max(2, r.Height);
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static void DrawVectorIcon(Graphics g, string kind, Rectangle box, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(color, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                int cx = box.X + box.Width / 2;
                int cy = box.Y + box.Height / 2;

                if (kind == "CPU")
                {
                    g.DrawRectangle(p, cx - 7, cy - 7, 14, 14);
                    g.DrawRectangle(p, cx - 3, cy - 3, 6, 6);
                    for (int i = -4; i <= 4; i += 4)
                    {
                        g.DrawLine(p, cx + i, cy - 11, cx + i, cy - 7);
                        g.DrawLine(p, cx + i, cy + 7, cx + i, cy + 11);
                        g.DrawLine(p, cx - 11, cy + i, cx - 7, cy + i);
                        g.DrawLine(p, cx + 7, cy + i, cx + 11, cy + i);
                    }
                }
                else if (kind == "GPU")
                {
                    g.DrawRectangle(p, cx - 10, cy - 6, 20, 12);
                    g.DrawEllipse(p, cx - 6, cy - 4, 8, 8);
                    g.DrawLine(p, cx + 5, cy - 3, cx + 7, cy - 3);
                    g.DrawLine(p, cx + 5, cy + 3, cx + 7, cy + 3);
                    g.DrawLine(p, cx - 12, cy - 8, cx - 10, cy - 8);
                    g.DrawLine(p, cx - 10, cy - 8, cx - 10, cy + 8);
                }
                else if (kind == "RAM")
                {
                    g.DrawRectangle(p, cx - 10, cy - 5, 20, 10);
                    for (int i = -6; i <= 6; i += 4)
                    {
                        g.DrawLine(p, cx + i, cy - 2, cx + i, cy + 2);
                        g.DrawLine(p, cx + i, cy + 5, cx + i, cy + 9);
                    }
                }
                else if (kind == "DISK")
                {
                    g.DrawRectangle(p, cx - 8, cy - 10, 16, 20);
                    g.DrawLine(p, cx - 8, cy + 4, cx + 8, cy + 4);
                    using (var b = new SolidBrush(color))
                        g.FillEllipse(b, cx + 3, cy + 6, 3, 3);
                }
                else if (kind == "SHIELD")
                {
                    g.DrawPolygon(p, new[] {
                        new Point(cx, cy - 10),
                        new Point(cx + 8, cy - 6),
                        new Point(cx + 7, cy + 3),
                        new Point(cx, cy + 10),
                        new Point(cx - 7, cy + 3),
                        new Point(cx - 8, cy - 6)
                    });
                }
                else if (kind == "BOLT")
                {
                    g.DrawPolygon(p, new[] {
                        new Point(cx + 2, cy - 10),
                        new Point(cx - 7, cy + 1),
                        new Point(cx - 1, cy + 1),
                        new Point(cx - 3, cy + 10),
                        new Point(cx + 7, cy - 1),
                        new Point(cx + 1, cy - 1)
                    });
                }
                else if (kind == "WRENCH")
                {
                    g.DrawEllipse(p, cx - 8, cy - 8, 10, 10);
                    g.DrawLine(p, cx, cy, cx + 9, cy + 9);
                }
                else
                {
                    g.DrawEllipse(p, cx - 8, cy - 8, 16, 16);
                    g.DrawLine(p, cx, cy, cx + 5, cy - 5);
                }
            }
        }
    }

    public class TopHeaderBar : Control
    {
        string title = "Limpiador de Basura";
        string subtitle = "Panel de rendimiento y mantenimiento en tiempo real";
        int hoverBtn = -1;

        public event Action MinimizeClicked;
        public event Action MaximizeClicked;
        public event Action CloseClicked;

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        public string Title
        {
            get { return title; }
            set { title = value; Invalidate(); }
        }

        public string Subtitle
        {
            get { return subtitle; }
            set { subtitle = value; Invalidate(); }
        }

        public TopHeaderBar()
        {
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            BackColor = UiTheme.BgCanvas;
        }

        Rectangle GetButtonRect(int index)
        {
            int btnW = 42, btnH = 32;
            int rightMargin = 16;
            int spacing = 4;
            int x = Width - rightMargin - (3 - index) * btnW - (2 - index) * spacing;
            return new Rectangle(x, (Height - btnH) / 2, btnW, btnH);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int found = -1;
            for (int i = 0; i < 3; i++)
            {
                if (GetButtonRect(i).Contains(e.Location))
                {
                    found = i;
                    break;
                }
            }
            Cursor = found >= 0 ? Cursors.Hand : Cursors.Default;
            if (found != hoverBtn)
            {
                hoverBtn = found;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverBtn != -1)
            {
                hoverBtn = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            for (int i = 0; i < 3; i++)
            {
                if (GetButtonRect(i).Contains(e.Location))
                {
                    if (i == 0 && MinimizeClicked != null) MinimizeClicked();
                    else if (i == 1 && MaximizeClicked != null) MaximizeClicked();
                    else if (i == 2 && CloseClicked != null) CloseClicked();
                    return;
                }
            }

            Form f = FindForm();
            if (f != null)
            {
                if (e.Clicks == 2 && MaximizeClicked != null)
                {
                    MaximizeClicked();
                    return;
                }
                ReleaseCapture();
                SendMessage(f.Handle, 0xA1, 0x2, 0);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.BgCanvas);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var fTitle = new Font("Segoe UI", 13.5f, FontStyle.Bold))
            using (var fSub = new Font("Segoe UI", 8.5f))
            {
                TextRenderer.DrawText(e.Graphics, title, fTitle, new Point(28, 8), UiTheme.TextLight);
                TextRenderer.DrawText(e.Graphics, subtitle, fSub, new Point(29, 36), UiTheme.TextMuted);
            }

            using (var fBadge = new Font("Segoe UI Semibold", 8f, FontStyle.Bold))
            {
                string badgeText = "MOTOR EN TIEMPO REAL";
                Size textSz = TextRenderer.MeasureText(badgeText, fBadge);
                int badgeW = textSz.Width + 38;
                int badgeH = 28;
                var badgeRect = new Rectangle(Width - 165 - badgeW, (Height - badgeH) / 2, badgeW, badgeH);

                if (badgeRect.X > 340)
                {
                    using (var path = UiShape.Round(badgeRect, badgeH / 2))
                    using (var bg = new SolidBrush(Color.FromArgb(16, 36, 46)))
                    using (var pen = new Pen(Color.FromArgb(20, 120, 100), 1f))
                    {
                        e.Graphics.FillPath(bg, path);
                        e.Graphics.DrawPath(pen, path);
                    }
                    using (var dot = new SolidBrush(UiTheme.Mint))
                        e.Graphics.FillEllipse(dot, badgeRect.X + 12, badgeRect.Y + (badgeH - 8) / 2, 8, 8);

                    var textRect = new Rectangle(badgeRect.X + 24, badgeRect.Y, badgeRect.Width - 28, badgeH);
                    TextRenderer.DrawText(e.Graphics, badgeText, fBadge, textRect, UiTheme.Mint, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
            }

            for (int i = 0; i < 3; i++)
            {
                Rectangle r = GetButtonRect(i);
                bool isHovered = hoverBtn == i;
                if (isHovered)
                {
                    Color hoverBg = i == 2 ? UiTheme.Danger : UiTheme.CardHover;
                    using (var path = UiShape.Round(r, 7))
                    using (var b = new SolidBrush(hoverBg))
                        e.Graphics.FillPath(b, path);
                }

                Color iconCol = isHovered ? Color.White : UiTheme.TextMuted;
                using (var pen = new Pen(iconCol, 1.7f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    int cx = r.X + r.Width / 2;
                    int cy = r.Y + r.Height / 2;

                    if (i == 0)
                        e.Graphics.DrawLine(pen, cx - 5, cy + 1, cx + 5, cy + 1);
                    else if (i == 1)
                        e.Graphics.DrawRectangle(pen, cx - 5, cy - 5, 10, 10);
                    else
                    {
                        e.Graphics.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                        e.Graphics.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                    }
                }
            }

            using (var linePen = new Pen(Color.FromArgb(24, 34, 54), 1f))
                e.Graphics.DrawLine(linePen, 28, Height - 1, Width - 16, Height - 1);
        }
    }

    public class RoundedPanel : Panel
    {
        public Color FillColor = UiTheme.CardBg;
        public Color BorderColor = UiTheme.Border;
        public Color TopAccentColor = Color.Empty;
        public bool DrawBorder = true;
        public int Radius = 12;

        public RoundedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.BgCanvas;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color parentBg = Parent != null ? Parent.BackColor : UiTheme.BgCanvas;
            e.Graphics.Clear(parentBg);
            if (Width < 4 || Height < 4) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiShape.Round(rect, Radius))
            using (var brush = new SolidBrush(FillColor))
            {
                e.Graphics.FillPath(brush, path);
                if (TopAccentColor != Color.Empty)
                {
                    using (var glowPen = new Pen(Color.FromArgb(90, TopAccentColor), 2f))
                        e.Graphics.DrawLine(glowPen, Radius, 1, Width - Radius, 1);
                }
                if (DrawBorder)
                {
                    using (var pen = new Pen(BorderColor, 1f))
                        e.Graphics.DrawPath(pen, path);
                }
            }
            base.OnPaint(e);
        }
    }

    public class TelemetryCard : Control
    {
        readonly string title;
        readonly string kind;
        string valueText = "0%";
        string detailText = "Analizando...";
        int currentVal = 0;
        readonly List<int> history = new List<int>();

        public TelemetryCard(string cardTitle, string iconKind)
        {
            title = cardTitle;
            kind = iconKind;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.BgCanvas;
            for (int i = 0; i < 24; i++) history.Add(15);
        }

        public void PushSample(int percentage, string formattedValue, string subtitle)
        {
            currentVal = Math.Max(0, Math.Min(100, percentage));
            valueText = formattedValue;
            detailText = subtitle;
            history.Add(currentVal);
            if (history.Count > 24) history.RemoveAt(0);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.BgCanvas);
            if (Width < 10 || Height < 10) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color accent = kind == "RAM" ? UiTheme.Mint : (kind == "DISK" ? UiTheme.Cyan : UiTheme.Primary);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            using (var path = UiShape.Round(rect, 12))
            using (var bg = new SolidBrush(UiTheme.CardBg))
            {
                e.Graphics.FillPath(bg, path);

                if (history.Count > 1)
                {
                    var oldClip = e.Graphics.Clip;
                    e.Graphics.SetClip(path);

                    int chartTop = Height - 58;
                    int chartBottom = Height - 14;
                    int chartH = Math.Max(10, chartBottom - chartTop);
                    float stepX = (float)(Width - 2) / (history.Count - 1);

                    var pts = new PointF[history.Count];
                    for (int i = 0; i < history.Count; i++)
                    {
                        float x = 1 + i * stepX;
                        float y = chartBottom - (history[i] / 100f) * chartH;
                        pts[i] = new PointF(x, y);
                    }

                    using (var areaPath = new GraphicsPath())
                    {
                        areaPath.AddLines(pts);
                        areaPath.AddLine(pts[pts.Length - 1].X, Height, pts[0].X, Height);
                        areaPath.CloseFigure();

                        using (var grad = new LinearGradientBrush(new Rectangle(0, chartTop - 5, Width, chartH + 25), Color.FromArgb(55, accent), Color.FromArgb(4, accent), LinearGradientMode.Vertical))
                            e.Graphics.FillPath(grad, areaPath);
                    }

                    using (var linePen = new Pen(accent, 2f))
                        e.Graphics.DrawLines(linePen, pts);

                    e.Graphics.Clip = oldClip;
                }

                int barW = (int)((Width - 32) * (currentVal / 100.0));
                using (var trackB = new SolidBrush(UiTheme.TrackDark))
                    e.Graphics.FillRectangle(trackB, 16, Height - 10, Width - 32, 4);
                if (barW > 2)
                {
                    using (var valB = new SolidBrush(accent))
                        e.Graphics.FillRectangle(valB, 16, Height - 10, barW, 4);
                }

                using (var pen = new Pen(UiTheme.Border, 1f))
                    e.Graphics.DrawPath(pen, path);
            }

            var iconBox = new Rectangle(16, 16, 40, 40);
            using (var iconPath = UiShape.Round(iconBox, 10))
            using (var iconBg = new SolidBrush(Color.FromArgb(28, accent)))
                e.Graphics.FillPath(iconBg, iconPath);
            UiShape.DrawVectorIcon(e.Graphics, kind, iconBox, accent);

            using (var fTitle = new Font("Segoe UI Semibold", 10f, FontStyle.Bold))
            using (var fSub = new Font("Segoe UI", 8.2f))
            using (var fVal = new Font("Segoe UI", 20f, FontStyle.Bold))
            {
                Size valSize = TextRenderer.MeasureText(valueText, fVal);
                int valX = Width - valSize.Width - 16;
                TextRenderer.DrawText(e.Graphics, valueText, fVal, new Point(valX, 14), UiTheme.TextLight);

                int textMaxW = Math.Max(60, valX - 72);
                TextRenderer.DrawText(e.Graphics, title, fTitle, new Rectangle(68, 15, textMaxW, 22), UiTheme.TextLight, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(e.Graphics, detailText, fSub, new Rectangle(68, 36, textMaxW, 20), UiTheme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    public class OptionTile : Control
    {
        bool isChecked;
        bool hover;
        readonly string title;
        readonly string subtitle;
        readonly string iconKind;
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    Invalidate();
                    if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public OptionTile(string mainText, string subText, string kind, bool defaultChecked)
        {
            title = mainText;
            subtitle = subText;
            iconKind = kind;
            isChecked = defaultChecked;
            Size = new Size(280, 56);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.CardBg;
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; Invalidate(); };
            Click += delegate { if (Enabled) Checked = !Checked; };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color parentBg = Parent != null ? Parent.BackColor : UiTheme.CardBg;
            e.Graphics.Clear(parentBg);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = isChecked ? Color.FromArgb(20, 38, 68) : (hover ? UiTheme.CardHover : UiTheme.CardAlt);
            Color border = isChecked ? UiTheme.Primary : UiTheme.Border;

            using (var path = UiShape.Round(rect, 10))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, isChecked ? 1.5f : 1f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            var iconBox = new Rectangle(12, (Height - 34) / 2, 34, 34);
            Color accent = isChecked ? UiTheme.Cyan : UiTheme.TextMuted;
            using (var p = UiShape.Round(iconBox, 8))
            using (var b = new SolidBrush(Color.FromArgb(25, accent)))
                e.Graphics.FillPath(b, p);
            UiShape.DrawVectorIcon(e.Graphics, iconKind, iconBox, accent);

            int textW = Math.Max(40, Width - 94);
            int topY = (Height - 38) / 2;
            using (var fTitle = new Font("Segoe UI Semibold", 9f, FontStyle.Bold))
            using (var fSub = new Font("Segoe UI", 7.8f))
            {
                TextRenderer.DrawText(e.Graphics, title, fTitle, new Rectangle(54, topY, textW, 20), UiTheme.TextLight, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(e.Graphics, subtitle, fSub, new Rectangle(54, topY + 19, textW, 18), UiTheme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            var checkRect = new Rectangle(Width - 32, (Height - 20) / 2, 20, 20);
            using (var path = UiShape.Round(checkRect, 6))
            {
                if (isChecked)
                {
                    using (var brush = new SolidBrush(UiTheme.Mint))
                        e.Graphics.FillPath(brush, path);
                    using (var pen = new Pen(Color.FromArgb(10, 20, 30), 2.2f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        e.Graphics.DrawLine(pen, checkRect.X + 5, checkRect.Y + 10, checkRect.X + 9, checkRect.Y + 14);
                        e.Graphics.DrawLine(pen, checkRect.X + 9, checkRect.Y + 14, checkRect.X + 15, checkRect.Y + 6);
                    }
                }
                else
                {
                    using (var pen = new Pen(UiTheme.Border, 1.5f))
                        e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }

    public class ModernButton : Button
    {
        bool hover;
        public bool IsPrimary;
        public bool IsDanger;

        public ModernButton(string text, bool primary = false, bool danger = false)
        {
            Text = text;
            IsPrimary = primary;
            IsDanger = danger;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            Size = new Size(165, 38);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.BgCanvas;
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color clearCol = Parent != null ? Parent.BackColor : UiTheme.BgCanvas;
            e.Graphics.Clear(clearCol);
            if (Width < 2 || Height < 2) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            using (var path = UiShape.Round(rect, 8))
            {
                if (IsPrimary)
                {
                    Color c1 = hover ? Color.FromArgb(79, 150, 255) : UiTheme.Primary;
                    Color c2 = hover ? Color.FromArgb(26, 205, 149) : UiTheme.Mint;
                    using (var brush = new LinearGradientBrush(rect, c1, c2, LinearGradientMode.Horizontal))
                        e.Graphics.FillPath(brush, path);
                }
                else if (IsDanger)
                {
                    Color bg = hover ? UiTheme.Danger : Color.FromArgb(48, 22, 34);
                    using (var brush = new SolidBrush(bg))
                    using (var pen = new Pen(UiTheme.Danger, 1.2f))
                    {
                        e.Graphics.FillPath(brush, path);
                        e.Graphics.DrawPath(pen, path);
                    }
                }
                else
                {
                    Color bg = hover ? Color.FromArgb(38, 52, 82) : Color.FromArgb(26, 36, 58);
                    using (var brush = new SolidBrush(bg))
                    using (var pen = new Pen(hover ? UiTheme.Primary : UiTheme.Border, 1f))
                    {
                        e.Graphics.FillPath(brush, path);
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            }

            var textRect = new Rectangle(6, 0, Width - 12, Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, UiTheme.TextLight, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public class PillButton : Button
    {
        bool hover;
        public PillButton(string text)
        {
            Text = text;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Segoe UI", 9.8f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.CardBg;
            ForeColor = Color.White;
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color clearCol = Parent != null ? Parent.BackColor : UiTheme.CardBg;
            e.Graphics.Clear(clearCol);
            if (Width < 2 || Height < 2) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color c1 = hover ? Color.FromArgb(80, 155, 255) : UiTheme.Primary;
            Color c2 = hover ? Color.FromArgb(30, 215, 155) : UiTheme.Mint;

            using (var path = UiShape.Round(rect, Height / 2))
            using (var brush = new LinearGradientBrush(rect, c1, c2, LinearGradientMode.Horizontal))
                e.Graphics.FillPath(brush, path);

            var textRect = new Rectangle(10, 0, Width - 20, Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public class SegmentedSelector : Control
    {
        readonly List<string> items = new List<string>();
        int selectedIndex = 0;
        int hoverIndex = -1;
        public event EventHandler SelectedIndexChanged;

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value >= 0 && value < items.Count && selectedIndex != value)
                {
                    selectedIndex = value;
                    Invalidate();
                    if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
                }
            }
        }

        public string SelectedItem
        {
            get { return selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : ""; }
        }

        public SegmentedSelector(params string[] options)
        {
            items.AddRange(options);
            Size = new Size(520, 38);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.BgCanvas;
        }

        public void SetItems(IEnumerable<string> newItems)
        {
            items.Clear();
            items.AddRange(newItems);
            selectedIndex = items.Count > 0 ? 0 : -1;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (items.Count == 0) return;
            int segW = Width / items.Count;
            int idx = Math.Min(items.Count - 1, Math.Max(0, e.X / Math.Max(1, segW)));
            if (idx != hoverIndex) { hoverIndex = idx; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverIndex = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (items.Count == 0) return;
            int segW = Width / items.Count;
            int idx = Math.Min(items.Count - 1, Math.Max(0, e.X / Math.Max(1, segW)));
            SelectedIndex = idx;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color clearCol = Parent != null ? Parent.BackColor : UiTheme.BgCanvas;
            e.Graphics.Clear(clearCol);
            if (items.Count == 0 || Width < 10) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var outer = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiShape.Round(outer, 8))
            using (var bg = new SolidBrush(UiTheme.TrackDark))
            using (var pen = new Pen(UiTheme.Border, 1f))
            {
                e.Graphics.FillPath(bg, path);
                e.Graphics.DrawPath(pen, path);
            }

            int segW = Width / items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                int x = i * segW + 3;
                int w = (i == items.Count - 1) ? (Width - x - 3) : (segW - 4);
                var r = new Rectangle(x, 3, w, Height - 7);

                if (i == selectedIndex)
                {
                    using (var p = UiShape.Round(r, 6))
                    using (var b = new LinearGradientBrush(r, UiTheme.Primary, Color.FromArgb(30, 100, 210), LinearGradientMode.Horizontal))
                        e.Graphics.FillPath(b, p);
                }
                else if (i == hoverIndex)
                {
                    using (var p = UiShape.Round(r, 6))
                    using (var b = new SolidBrush(UiTheme.CardHover))
                        e.Graphics.FillPath(b, p);
                }

                Color tc = i == selectedIndex ? Color.White : UiTheme.TextMuted;
                using (var f = new Font("Segoe UI Semibold", 8.2f, i == selectedIndex ? FontStyle.Bold : FontStyle.Regular))
                    TextRenderer.DrawText(e.Graphics, items[i], f, r, tc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    public class ToggleSwitch : Control
    {
        bool isChecked;
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    Invalidate();
                    if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public ToggleSwitch()
        {
            Size = new Size(46, 24);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            BackColor = UiTheme.CardBg;
            Click += delegate { Checked = !Checked; };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color clearCol = Parent != null ? Parent.BackColor : UiTheme.CardBg;
            e.Graphics.Clear(clearCol);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiShape.Round(rect, Height / 2))
            {
                if (isChecked)
                {
                    using (var brush = new LinearGradientBrush(rect, UiTheme.Primary, UiTheme.Mint, LinearGradientMode.Horizontal))
                        e.Graphics.FillPath(brush, path);
                }
                else
                {
                    using (var brush = new SolidBrush(UiTheme.TrackDark))
                    using (var pen = new Pen(UiTheme.Border, 1f))
                    {
                        e.Graphics.FillPath(brush, path);
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            }

            int knobSize = Height - 6;
            int knobX = isChecked ? Width - knobSize - 3 : 3;
            using (var brush = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(brush, knobX, 3, knobSize, knobSize);
        }
    }

    public class NavigationButton : Button
    {
        readonly string caption;
        readonly string subtitle;
        readonly int icon;
        bool active;
        bool hover;

        public bool Active
        {
            get { return active; }
            set { active = value; Invalidate(); }
        }

        public NavigationButton(string text, string sub, int symbol)
        {
            caption = text;
            subtitle = sub;
            icon = symbol;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Text = "";
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            BackColor = UiTheme.BgSidebar;
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.BgSidebar);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(12, 2, Width - 24, Height - 4);

            if (active || hover)
            {
                Color bg = active ? Color.FromArgb(20, 32, 56) : Color.FromArgb(15, 22, 38);
                using (var path = UiShape.Round(rect, 10))
                using (var brush = new SolidBrush(bg))
                    e.Graphics.FillPath(brush, path);

                if (active)
                {
                    using (var pen = new Pen(Color.FromArgb(45, 90, 170), 1f))
                    using (var path = UiShape.Round(rect, 10))
                        e.Graphics.DrawPath(pen, path);

                    using (var accentBrush = new LinearGradientBrush(new Rectangle(12, 10, 4, Height - 20), UiTheme.Cyan, UiTheme.Primary, LinearGradientMode.Vertical))
                    using (var barPath = UiShape.Round(new Rectangle(12, 10, 4, Height - 20), 2))
                        e.Graphics.FillPath(accentBrush, barPath);
                }
            }

            var iconBox = new Rectangle(22, (Height - 32) / 2, 32, 32);
            Color iconColor = active ? UiTheme.Cyan : (hover ? UiTheme.TextLight : UiTheme.TextMuted);
            if (active)
            {
                using (var p = UiShape.Round(iconBox, 8))
                using (var b = new SolidBrush(Color.FromArgb(28, UiTheme.Cyan)))
                    e.Graphics.FillPath(b, p);
            }

            string[] kinds = { "DASH", "BOLT", "RAM", "SHIELD", "GPU", "DISK", "WRENCH" };
            UiShape.DrawVectorIcon(e.Graphics, kinds[icon % kinds.Length], iconBox, iconColor);

            int textW = Width - 78;
            int topY = (Height - 36) / 2;
            using (var fMain = new Font("Segoe UI Semibold", 9.5f, active ? FontStyle.Bold : FontStyle.Regular))
            using (var fSub = new Font("Segoe UI", 7.6f))
            {
                Color cMain = active ? Color.White : Color.FromArgb(210, 220, 235);
                TextRenderer.DrawText(e.Graphics, caption, fMain, new Rectangle(62, topY, textW, 20), cMain, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(e.Graphics, subtitle, fSub, new Rectangle(62, topY + 18, textW, 18), UiTheme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    public class LogoControl : Control
    {
        public LogoControl()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            BackColor = UiTheme.BgSidebar;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.BgSidebar);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var badgeRect = new Rectangle(18, 16, 44, 44);
            using (var path = UiShape.Round(badgeRect, 12))
            using (var brush = new LinearGradientBrush(badgeRect, UiTheme.Primary, UiTheme.Mint, LinearGradientMode.ForwardDiagonal))
                e.Graphics.FillPath(brush, path);

            UiShape.DrawVectorIcon(e.Graphics, "BOLT", badgeRect, Color.White);

            using (var fBrand = new Font("Segoe UI", 15f, FontStyle.Bold))
            using (var fTag = new Font("Segoe UI Semibold", 7.2f, FontStyle.Bold))
            {
                int xOpti = 70;
                TextRenderer.DrawText(e.Graphics, "Opti", fBrand, new Point(xOpti, 12), Color.White, TextFormatFlags.NoPadding);
                Size szOpti = TextRenderer.MeasureText(e.Graphics, "Opti", fBrand, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, "Guille", fBrand, new Point(xOpti + szOpti.Width + 1, 12), UiTheme.Cyan, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, "PRO GAMING & OS SUITE", fTag, new Point(71, 42), UiTheme.TextMuted, TextFormatFlags.NoPadding);
            }
        }
    }

    public class DonutGauge : Control
    {
        int value = 88;
        public int Value
        {
            get { return value; }
            set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }
        public string StatusTitle = "SISTEMA ÓPTIMO";
        public string CenterCaption = "Índice de salud";

        public DonutGauge()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.CardBg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color clearCol = Parent != null ? Parent.BackColor : UiTheme.CardBg;
            e.Graphics.Clear(clearCol);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int d = Math.Min(Width, Height) - 30;
            if (d < 40) return;
            var r = new Rectangle((Width - d) / 2, (Height - d) / 2, d, d);

            int cx = Width / 2, cy = Height / 2;
            int tickRadius = d / 2 + 10;
            using (var tickPen = new Pen(Color.FromArgb(40, 60, 95), 1.5f))
            {
                for (int i = 0; i < 36; i++)
                {
                    double ang = i * 10 * Math.PI / 180.0;
                    int x1 = cx + (int)((tickRadius - 3) * Math.Cos(ang));
                    int y1 = cy + (int)((tickRadius - 3) * Math.Sin(ang));
                    int x2 = cx + (int)(tickRadius * Math.Cos(ang));
                    int y2 = cy + (int)(tickRadius * Math.Sin(ang));
                    e.Graphics.DrawLine(tickPen, x1, y1, x2, y2);
                }
            }

            float penW = d < 140 ? 11f : 14f;
            using (var p = new Pen(UiTheme.TrackDark, penW))
                e.Graphics.DrawArc(p, r, -90, 360);

            if (value > 0)
            {
                using (var brush = new LinearGradientBrush(r, UiTheme.Cyan, UiTheme.Mint, LinearGradientMode.ForwardDiagonal))
                using (var p = new Pen(brush, penW))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    e.Graphics.DrawArc(p, r, -90, Math.Max(2, value * 3.6f));
                }
            }

            bool compact = d < 145;
            float mainPt = compact ? 18f : 24f;
            float subPt = compact ? 7f : 8f;

            using (var fVal = new Font("Segoe UI", mainPt, FontStyle.Bold))
            using (var fStat = new Font("Segoe UI Semibold", subPt, FontStyle.Bold))
            using (var fCap = new Font("Segoe UI", subPt))
            {
                int topY = cy - (compact ? 26 : 34);
                TextRenderer.DrawText(e.Graphics, value + "%", fVal, new Rectangle(0, topY, Width, compact ? 30 : 40), UiTheme.TextLight, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics, StatusTitle, fStat, new Rectangle(0, cy + (compact ? 4 : 8), Width, 18), UiTheme.Mint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics, CenterCaption, fCap, new Rectangle(0, cy + (compact ? 18 : 24), Width, 18), UiTheme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    public class TableColumnSpec
    {
        public string Title;
        public float Weight;
        public bool IsBadge;
        public bool IsMemoryBar;
    }

    public class TableRowData
    {
        public string[] Cells;
        public object Tag;
        public double NumericMetric;
    }

    public class ModernTableControl : Control
    {
        readonly List<TableColumnSpec> columns = new List<TableColumnSpec>();
        readonly List<TableRowData> rows = new List<TableRowData>();
        int selectedIndex = -1;
        int hoverIndex = -1;
        int scrollOffset = 0;
        const int HeaderHeight = 38;
        const int RowHeight = 40;

        public TableRowData SelectedRow
        {
            get { return selectedIndex >= 0 && selectedIndex < rows.Count ? rows[selectedIndex] : null; }
        }

        public ModernTableControl()
        {
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.CardBg;
        }

        public void AddColumn(string title, float weight, bool isBadge = false, bool isMemoryBar = false)
        {
            columns.Add(new TableColumnSpec { Title = title, Weight = weight, IsBadge = isBadge, IsMemoryBar = isMemoryBar });
        }

        public void SetRows(IEnumerable<TableRowData> data)
        {
            rows.Clear();
            rows.AddRange(data);
            if (selectedIndex >= rows.Count) selectedIndex = rows.Count - 1;
            if (selectedIndex < 0 && rows.Count > 0) selectedIndex = 0;
            ClampScroll();
            Invalidate();
        }

        int MaxVisibleRows
        {
            get { return Math.Max(1, (Height - HeaderHeight - 4) / RowHeight); }
        }

        void ClampScroll()
        {
            int maxScroll = Math.Max(0, rows.Count - MaxVisibleRows);
            if (scrollOffset > maxScroll) scrollOffset = maxScroll;
            if (scrollOffset < 0) scrollOffset = 0;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int delta = e.Delta > 0 ? -2 : 2;
            scrollOffset += delta;
            ClampScroll();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Y <= HeaderHeight)
            {
                if (hoverIndex != -1) { hoverIndex = -1; Invalidate(); }
                return;
            }
            int idx = scrollOffset + (e.Y - HeaderHeight) / RowHeight;
            if (idx >= rows.Count) idx = -1;
            if (idx != hoverIndex)
            {
                hoverIndex = idx;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverIndex != -1) { hoverIndex = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Y > HeaderHeight)
            {
                int idx = scrollOffset + (e.Y - HeaderHeight) / RowHeight;
                if (idx >= 0 && idx < rows.Count)
                {
                    selectedIndex = idx;
                    Invalidate();
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClampScroll();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.BgCanvas);
            if (Width < 20 || Height < 20) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var outer = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiShape.Round(outer, 12))
            using (var bg = new SolidBrush(UiTheme.CardBg))
            {
                e.Graphics.FillPath(bg, path);

                var oldClip = e.Graphics.Clip;
                e.Graphics.SetClip(path);

                using (var hBrush = new SolidBrush(Color.FromArgb(14, 20, 34)))
                    e.Graphics.FillRectangle(hBrush, 0, 0, Width, HeaderHeight);

                float totalWeight = 0;
                foreach (var c in columns) totalWeight += c.Weight;
                if (totalWeight <= 0) totalWeight = 1;

                int usableWidth = Width - 16;
                float curX = 0;
                using (var fHead = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold))
                {
                    foreach (var col in columns)
                    {
                        float colW = usableWidth * (col.Weight / totalWeight);
                        var hRect = new Rectangle((int)curX + 16, 0, (int)colW - 16, HeaderHeight);
                        TextRenderer.DrawText(e.Graphics, col.Title.ToUpperInvariant(), fHead, hRect, UiTheme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                        curX += colW;
                    }
                }

                using (var sep = new Pen(UiTheme.Border, 1f))
                    e.Graphics.DrawLine(sep, 0, HeaderHeight - 1, Width, HeaderHeight - 1);

                int visibleCount = MaxVisibleRows + 1;
                using (var fPrimary = new Font("Segoe UI Semibold", 9f, FontStyle.Bold))
                using (var fRegular = new Font("Segoe UI", 8.8f))
                using (var fBadge = new Font("Segoe UI Semibold", 8f, FontStyle.Bold))
                {
                    for (int i = 0; i < visibleCount; i++)
                    {
                        int rowIdx = scrollOffset + i;
                        if (rowIdx >= rows.Count) break;

                        int y = HeaderHeight + i * RowHeight;
                        var rowRect = new Rectangle(0, y, Width, RowHeight);
                        bool isSel = rowIdx == selectedIndex;
                        bool isHov = rowIdx == hoverIndex;

                        Color rowBg = isSel ? Color.FromArgb(26, 46, 82) : (isHov ? UiTheme.CardHover : (rowIdx % 2 == 0 ? UiTheme.CardBg : UiTheme.CardAlt));
                        using (var rb = new SolidBrush(rowBg))
                            e.Graphics.FillRectangle(rb, rowRect);

                        if (isSel)
                        {
                            using (var barB = new SolidBrush(UiTheme.Cyan))
                                e.Graphics.FillRectangle(barB, 0, y + 6, 4, RowHeight - 12);
                        }

                        var rowData = rows[rowIdx];
                        curX = 0;
                        for (int c = 0; c < columns.Count && c < rowData.Cells.Length; c++)
                        {
                            var col = columns[c];
                            float colW = usableWidth * (col.Weight / totalWeight);
                            string text = rowData.Cells[c] ?? "";
                            var cellRect = new Rectangle((int)curX + 16, y, (int)colW - 20, RowHeight);

                            if (c == 0)
                            {
                                var avatarRect = new Rectangle(cellRect.X, y + 8, 24, 24);
                                Color avCol = isSel ? UiTheme.Cyan : UiTheme.Primary;
                                using (var avPath = UiShape.Round(avatarRect, 6))
                                using (var avBg = new SolidBrush(Color.FromArgb(35, avCol)))
                                    e.Graphics.FillPath(avBg, avPath);

                                string letter = text.Length > 0 ? text.Substring(0, 1).ToUpperInvariant() : "•";
                                TextRenderer.DrawText(e.Graphics, letter, fBadge, avatarRect, avCol, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                                var nameRect = new Rectangle(cellRect.X + 32, y, cellRect.Width - 32, RowHeight);
                                TextRenderer.DrawText(e.Graphics, text, fPrimary, nameRect, UiTheme.TextLight, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                            }
                            else if (col.IsBadge)
                            {
                                Color badgeCol = UiTheme.Cyan;
                                if (text.Contains("HKLM") || text.Contains("Sistema") || text.Contains("High")) badgeCol = UiTheme.Purple;
                                else if (text.Contains("Hibernado") || text.Contains("Below")) badgeCol = UiTheme.Amber;
                                else if (text.Contains("Activo")) badgeCol = UiTheme.Mint;

                                Size ts = TextRenderer.MeasureText(text, fBadge);
                                int bw = Math.Min(cellRect.Width, ts.Width + 20);
                                var pill = new Rectangle(cellRect.X, y + (RowHeight - 22) / 2, bw, 22);
                                using (var pPath = UiShape.Round(pill, 11))
                                using (var pBg = new SolidBrush(Color.FromArgb(28, badgeCol)))
                                using (var pPen = new Pen(Color.FromArgb(90, badgeCol), 1f))
                                {
                                    e.Graphics.FillPath(pBg, pPath);
                                    e.Graphics.DrawPath(pPen, pPath);
                                }
                                TextRenderer.DrawText(e.Graphics, text, fBadge, pill, badgeCol, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                            }
                            else if (col.IsMemoryBar)
                            {
                                int barMaxW = 70;
                                var barTrack = new Rectangle(cellRect.X, y + (RowHeight - 6) / 2, barMaxW, 6);
                                using (var tb = new SolidBrush(UiTheme.TrackDark))
                                    e.Graphics.FillRectangle(tb, barTrack);

                                double ratio = Math.Max(0.05, Math.Min(1.0, rowData.NumericMetric / 1200.0));
                                int fillW = (int)(barMaxW * ratio);
                                Color mCol = rowData.NumericMetric > 500 ? UiTheme.Amber : UiTheme.Mint;
                                using (var fb = new SolidBrush(mCol))
                                    e.Graphics.FillRectangle(fb, barTrack.X, barTrack.Y, fillW, 6);

                                var memTextRect = new Rectangle(cellRect.X + barMaxW + 10, y, cellRect.Width - barMaxW - 10, RowHeight);
                                TextRenderer.DrawText(e.Graphics, text, fPrimary, memTextRect, UiTheme.TextLight, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                            }
                            else
                            {
                                TextRenderer.DrawText(e.Graphics, text, fRegular, cellRect, UiTheme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                            }

                            curX += colW;
                        }
                    }
                }

                if (rows.Count > MaxVisibleRows)
                {
                    int trackH = Height - HeaderHeight - 8;
                    int thumbH = Math.Max(28, (int)(trackH * ((double)MaxVisibleRows / rows.Count)));
                    int maxScroll = Math.Max(1, rows.Count - MaxVisibleRows);
                    int thumbY = HeaderHeight + 4 + (int)((trackH - thumbH) * ((double)scrollOffset / maxScroll));
                    var thumbRect = new Rectangle(Width - 9, thumbY, 5, thumbH);
                    using (var tPath = UiShape.Round(thumbRect, 2))
                    using (var tBrush = new SolidBrush(Color.FromArgb(70, 95, 140)))
                        e.Graphics.FillPath(tBrush, tPath);
                }

                e.Graphics.Clip = oldClip;
                using (var pen = new Pen(UiTheme.Border, 1f))
                    e.Graphics.DrawPath(pen, path);
            }
        }
    }

    public class BlockMapControl : Control
    {
        readonly int[] states = new int[96];
        readonly Random rng = new Random(42);

        public BlockMapControl()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.CardBg;
            ResetBlocks(false);
        }

        public void ResetBlocks(bool optimized)
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (optimized)
                    states[i] = i < 74 ? (i % 3 == 0 ? 2 : 1) : 0;
                else
                    states[i] = rng.Next(0, 10) < 2 ? 3 : (rng.Next(0, 10) < 6 ? 1 : (rng.Next(0, 10) < 8 ? 2 : 0));
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color clearCol = Parent != null ? Parent.BackColor : UiTheme.CardBg;
            e.Graphics.Clear(clearCol);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int cols = 24, rows = 4;
            int pad = 5;
            int cellW = Math.Max(8, (Width - (cols - 1) * pad) / cols);
            int cellH = Math.Max(8, (Height - (rows - 1) * pad) / rows);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    Color color = UiTheme.TrackDark;
                    if (states[idx] == 1) color = UiTheme.Primary;
                    else if (states[idx] == 2) color = UiTheme.Mint;
                    else if (states[idx] == 3) color = UiTheme.Amber;

                    var rect = new Rectangle(c * (cellW + pad), r * (cellH + pad), cellW, cellH);
                    using (var path = UiShape.Round(rect, 4))
                    using (var b = new SolidBrush(color))
                        e.Graphics.FillPath(b, path);
                }
            }
        }
    }
}