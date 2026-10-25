using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SETUNA.Main;
using SETUNA.Main.StyleItems;

namespace SETUNA.Avalonia.Views
{
    /// <summary>
    /// 收缩窗（对应旧 CompactScrap 的期 4 简化版）：小缩略图 + 单击还原原图。
    /// ponytail: 简化交互（单击还原）；原版双击/拖动边框语义期 6 按需补。
    /// </summary>
    public class CompactWindow : Window
    {
        private readonly IScrap _scrap;
        private readonly CCompactStyleItem _item;

        public CompactWindow(IScrap scrap, CCompactStyleItem item)
        {
            _scrap = scrap;
            _item = item;

            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            Title = "收缩";

            var image = scrap.Image;
            if (image != null)
            {
                // 固定 50×50 缩略（对齐 CompactScrap 缩略尺寸）
                const int Thumb = 50;
                var scale = Math.Min((float)Thumb / image.Width, (float)Thumb / image.Height);
                var w = Math.Max(1, (int)(image.Width * scale));
                var h = Math.Max(1, (int)(image.Height * scale));

                using var stream = new System.IO.MemoryStream();
                ((System.Drawing.Bitmap)image).Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;
                var bitmap = new Bitmap(stream);

                var img = new global::Avalonia.Controls.Image { Source = bitmap, Width = w, Height = h };
                var border = new Border
                {
                    Child = img,
                    BorderBrush = new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(
                        (byte)((item.LineColor >> 16) & 0xFF),
                        (byte)((item.LineColor >> 8) & 0xFF),
                        (byte)(item.LineColor & 0xFF))),
                    BorderThickness = new Thickness(1),
                    Opacity = Math.Max(0.1, item.Opacity / 100.0),
                    Background = Brushes.Transparent,
                };
                Content = border;
                Width = w;
                Height = h;

                // 居中于原图（物理像素）
                Position = new PixelPoint(
                    scrap.Location.X + (image.Width - w) / 2,
                    scrap.Location.Y + (image.Height - h) / 2);

                PointerPressed += (s, e) =>
                {
                    if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    {
                        // 单击还原
                        scrap.Visible = true;
                        Close();
                    }
                };
            }
            else
            {
                Width = 50;
                Height = 50;
                Background = Brushes.Gray;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 原图保持隐藏（收缩语义：原图 Hide，收缩窗关闭后可由缓存恢复）
            base.OnClosed(e);
        }
    }
}
