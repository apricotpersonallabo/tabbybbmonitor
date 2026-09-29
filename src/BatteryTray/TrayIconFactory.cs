using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace BatteryTray;

internal static class TrayIconFactory
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.White);

        var darkBlue = Color.FromArgb(14, 62, 105);
        using var backgroundOutline = new Pen(darkBlue, 1.5f);
        graphics.DrawRoundedRectangle(backgroundOutline, new RectangleF(1f, 1f, 30f, 30f), 4f);

        using var batteryBrush = new SolidBrush(darkBlue);
        graphics.FillRoundedRectangle(batteryBrush, new RectangleF(4f, 8f, 21.5f, 16f), 3f);
        graphics.FillRectangle(batteryBrush, 25f, 12f, 3f, 8f);

        using var chargeBrush = new SolidBrush(Color.FromArgb(255, 214, 45));
        graphics.FillPolygon(chargeBrush,
        [
            new PointF(15f, 9.5f),
            new PointF(9.5f, 17f),
            new PointF(14f, 17f),
            new PointF(12f, 22.5f),
            new PointF(21f, 14f),
            new PointF(16.5f, 14f)
        ]);

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}

internal static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, RectangleF bounds, float radius)
    {
        using var path = CreateRoundedRectangle(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = CreateRoundedRectangle(bounds, radius);
        graphics.FillPath(brush, path);
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
