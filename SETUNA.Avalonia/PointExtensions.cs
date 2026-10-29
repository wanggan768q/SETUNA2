using System;
using Avalonia;
using Avalonia.Controls;
using DrawingPoint = System.Drawing.Point;

namespace SETUNA.Avalonia
{
    /// <summary>System.Drawing 与 Avalonia 像素类型互转。</summary>
    public static class PointExtensions
    {
        public static PixelPoint ToPixelPoint(this DrawingPoint p) => new PixelPoint(p.X, p.Y);

        public static DrawingPoint ToDrawingPoint(this PixelPoint p) => new DrawingPoint(p.X, p.Y);

        public static PixelPoint ToPixelPoint(this global::Avalonia.Point p) => new PixelPoint((int)Math.Round(p.X), (int)Math.Round(p.Y));

        /// <summary>RenderScaling 兜底：构造期可能为 0，统一换算入口。</summary>
        public static double RenderScale(this Window w) => w.RenderScaling > 0 ? w.RenderScaling : 1.0;
    }

    /// <summary>平台服务不可用时的布局兜底工作区（常见主屏 1080p）。</summary>
    internal static class ScreenFallback
    {
        internal static readonly System.Drawing.Rectangle WorkArea = new(0, 0, 1920, 1080);
    }
}
