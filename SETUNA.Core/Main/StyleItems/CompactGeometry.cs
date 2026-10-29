using System.Drawing;

namespace SETUNA.Main.StyleItems
{
    /// <summary>
    /// 收缩窗裁剪几何（纯函数，供测试）：对齐旧 CompactScrap 语义——
    /// 固定 50×50 窗，以双击点为中心取原图 50×50 源矩形；不 clamp（越界由透明绘制呈现）。
    /// </summary>
    public static class CompactGeometry
    {
        /// <summary>收缩窗边长（物理像素，对齐旧 Designer 固定 50×50）。</summary>
        public const int WindowSize = 50;

        /// <summary>计算源裁剪矩形；clickPoint 为 Empty 时取图中心（旧版构造分支）。</summary>
        public static Rectangle CalculateCropRect(Size imageSize, Point clickPoint)
        {
            var cx = clickPoint == Point.Empty ? imageSize.Width / 2 : clickPoint.X;
            var cy = clickPoint == Point.Empty ? imageSize.Height / 2 : clickPoint.Y;
            return new Rectangle(cx - WindowSize / 2, cy - WindowSize / 2, WindowSize, WindowSize);
        }
    }
}
