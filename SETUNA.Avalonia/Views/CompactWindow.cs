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
        private readonly IScrap scrap;
        private readonly CCompactStyleItem _item;

        public CompactWindow(IScrap scrap, CCompactStyleItem item)
        {
            this.scrap = scrap;
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

                // 拖动（对齐旧 CompactScrap：拖动同步写回 scrap 位置）+ 双击还原
                PointerPressed += OnPressed;
                PointerMoved += OnMoved;
                PointerReleased += OnReleased;
                DoubleTapped += OnDoubleTapped;
            }
            else
            {
                Width = 50;
                Height = 50;
                Background = Brushes.Gray;
            }
        }

        private bool _dragging;
        private PixelPoint _dragStart;

        private void OnPressed(object sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed)
            {
                _dragging = true;
                var scale = RenderScaling > 0 ? RenderScaling : 1.0;
                var client = e.GetPosition(this);
                var screenPt = new PixelPoint(
                    Position.X + (int)Math.Round(client.X * scale),
                    Position.Y + (int)Math.Round(client.Y * scale));
                _dragStart = screenPt - Position;
                e.Pointer.Capture(this);
            }
        }

        private void OnMoved(object sender, PointerEventArgs e)
        {
            if (!_dragging || scrap.IsClotheslineHanging)
            {
                return;
            }
            var scale = RenderScaling > 0 ? RenderScaling : 1.0;
            var client = e.GetPosition(this);
            var screen = new PixelPoint(
                Position.X + (int)Math.Round(client.X * scale),
                Position.Y + (int)Math.Round(client.Y * scale));
            Position = new PixelPoint(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
            // 同步写回原图位置（旧 CompactScrap 语义：原图跟随收缩窗居中）
            if (scrap.Image != null)
            {
                scrap.Location = new System.Drawing.Point(
                    Position.X - (scrap.Image.Width - (int)Width) / 2,
                    Position.Y - (scrap.Image.Height - (int)Height) / 2);
            }
        }

        private void OnReleased(object sender, PointerReleasedEventArgs e)
        {
            _dragging = false;
            e.Pointer.Capture(null);
        }

        /// <summary>双击：Ctrl+双击 = 挂回绳子；普通双击 = 还原（对齐旧 CompactScrap 双击语义：
        /// 先解除晾绳挂起（若挂绳中），再恢复原图显示）。</summary>
        private void OnDoubleTapped(object sender, global::Avalonia.Input.TappedEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control))
            {
                // Ctrl+双击 = 挂回绳子（验收需求）
                scrap.SetClotheslineHanging(true);
                Close();
                return;
            }
            if (scrap.IsClotheslineHanging)
            {
                // 挂绳中：取下（触发恢复显示），方窗关闭由挂起状态恢复逻辑接管
                scrap.SetClotheslineHanging(false);
            }
            else
            {
                scrap.Visible = true;
            }
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // 原图保持隐藏（收缩语义：原图 Hide，收缩窗关闭后可由缓存恢复）
            base.OnClosed(e);
        }
    }
}
