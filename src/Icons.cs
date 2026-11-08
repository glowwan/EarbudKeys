// Icons: the app/tray icon is drawn at runtime with System.Drawing,
// so the project needs no .ico file to compile. (The installer script
// uses a generated app.ico for shortcuts; see tools/make-icon.py.)
// C# 5 compatible (in-box csc.exe).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

internal static class Icons
{
    private static Icon _appIcon;
    private static Bitmap _appBitmap;

    public static Icon AppIcon
    {
        get
        {
            if (_appIcon == null)
            {
                Bitmap bitmap = AppBitmap;
                IntPtr handle = bitmap.GetHicon();
                _appIcon = Icon.FromHandle(handle);
            }
            return _appIcon;
        }
    }

    public static Bitmap AppBitmap
    {
        get
        {
            if (_appBitmap == null) _appBitmap = Draw(32);
            return _appBitmap;
        }
    }

    /// <summary>Blue rounded square with a white microphone (voice input).</summary>
    public static Bitmap Draw(int size)
    {
        var bitmap = new Bitmap(size, size);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            int pad = size / 16;
            using (var brush = new SolidBrush(Color.FromArgb(31, 111, 235)))
            using (var path = RoundedRect(pad, pad, size - pad * 2, size - pad * 2, size / 5))
            {
                g.FillPath(brush, path);
            }
            using (var pen = new Pen(Color.White, Math.Max(2f, size / 14f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float cx = size / 2f;
                float micW = size * 0.24f;
                float micTop = size * 0.24f;
                float micBottom = size * 0.56f;
                using (var path = new GraphicsPath())
                {
                    path.AddArc(cx - micW / 2, micTop, micW, size * 0.30f, 180, 180);
                    g.DrawPath(pen, path);
                }
                g.DrawLine(pen, cx - micW / 2, micTop + size * 0.15f, cx - micW / 2, micBottom - size * 0.05f);
                g.DrawLine(pen, cx + micW / 2, micTop + size * 0.15f, cx + micW / 2, micBottom - size * 0.05f);
                g.DrawLine(pen, cx - micW / 2, micBottom, cx + micW / 2, micBottom);
                using (var arcPath = new GraphicsPath())
                {
                    arcPath.AddArc(cx - size * 0.20f, micBottom - size * 0.10f, size * 0.40f, size * 0.22f, 0, 180);
                    g.DrawPath(pen, arcPath);
                }
                g.DrawLine(pen, cx, micBottom + size * 0.12f, cx, micBottom + size * 0.20f);
                g.DrawLine(pen, cx - size * 0.09f, micBottom + size * 0.20f, cx + size * 0.09f, micBottom + size * 0.20f);
            }
        }
        return bitmap;
    }

    private static GraphicsPath RoundedRect(int x, int y, int width, int height, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
