using System;
using Avalonia;
using DrawingPoint = System.Drawing.Point;

namespace SETUNA.Avalonia
{
    /// <summary>System.Drawing 与 Avalonia 像素类型互转。</summary>
    public static class PointExtensions
    {
        public static PixelPoint ToPixelPoint(this DrawingPoint p) => new PixelPoint(p.X, p.Y);

        public static DrawingPoint ToDrawingPoint(this PixelPoint p) => new DrawingPoint(p.X, p.Y);

        public static PixelPoint ToPixelPoint(this global::Avalonia.Point p) => new PixelPoint((int)Math.Round(p.X), (int)Math.Round(p.Y));
    }
}
