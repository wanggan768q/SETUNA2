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
    /// 收缩窗（对齐旧 CompactScrap）：固定 50×50，以双击点为中心裁原图钉在原地；
    /// 样式边框（虚/实线+透明度），拖动同步写回原图位置，双击还原，Esc/Enter 关闭。
    /// </summary>
    public class CompactWindow : Window
    {
        private readonly IScrap scrap;
        private readonly CCompactStyleItem _item;
        private readonly System.Drawing.Point _clickPoint;

        public CompactWindow(IScrap scrap, CCompactStyleItem item, System.Drawing.Point clickPoint)
        {
            this.scrap = scrap;
            _item = item;
            _clickPoint = clickPoint;

            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            Title = "收缩";

            var image = scrap.Image;
            var size = CompactGeometry.WindowSize;
            if (image != null)
            {
                // 以双击点为中心裁原图 50×50 并直接画样式边框（旧 OnPaint 语义：
                // 白框 + 样式线虚/实；Avalonia Border 无虚线支持，故在 GDI 内一次画完）
                var crop = CompactGeometry.CalculateCropRect(image.Size, clickPoint);
                using var gdi = new System.Drawing.Bitmap(size, size);
                using (var g = System.Drawing.Graphics.FromImage(gdi))
                {
                    g.DrawImage((System.Drawing.Bitmap)image,
                        new System.Drawing.Rectangle(0, 0, size, size), crop,
                        System.Drawing.GraphicsUnit.Pixel);
                    using var whitePen = new System.Drawing.Pen(System.Drawing.Color.White, 1f);
                    g.DrawRectangle(whitePen, 0, 0, size - 1, size - 1);
                    using var stylePen = new System.Drawing.Pen(
                        System.Drawing.Color.FromArgb(item.LineColor), 1f);
                    if (!item.SoldLine)
                    {
                        stylePen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        stylePen.DashPattern = new float[] { 4f, 4f };
                    }
                    g.DrawRectangle(stylePen, 0, 0, size - 1, size - 1);
                }
                using var stream = new System.IO.MemoryStream();
                gdi.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;

                var img = new global::Avalonia.Controls.Image { Source = new Bitmap(stream) };
                var border = new Border
                {
                    Child = img,
                    Opacity = Math.Max(0.1, item.Opacity / 100.0),
                    Background = Brushes.Transparent,
                };
                Content = border;
            }
            else
            {
                Background = Brushes.Gray;
            }

            // 物理像素 50×50 → DIP；构造时 RenderScaling 可能未就绪（常为 1.0），
            // Opened 后按实际缩放校正（ApplyDpiLayout）
            ApplyDpiLayout();

            // 拖动 + 双击还原 + 键盘关闭（旧 CompactScrap_KeyDown）
            PointerPressed += OnPressed;
            PointerMoved += OnMoved;
            PointerReleased += OnReleased;
            DoubleTapped += OnDoubleTapped;
            KeyDown += OnKeyDown;
            Opened += (s, e) =>
            {
                ApplyDpiLayout();
                Activate(); // Esc/Enter 依赖键盘焦点，显示后主动抢焦（贴图窗随后不抢回）
            };
        }

        /// <summary>
        /// 按 RenderScaling 把 50 物理像素换算成 DIP 尺寸并定位（旧 ConvertScrapPosToCompact）。
        /// Opened 前后各调一次：构造时缩放常未就绪，显示后校正。
        /// </summary>
        private void ApplyDpiLayout()
        {
            var size = CompactGeometry.WindowSize;
            var scale = this.RenderScale();
            Width = size / scale;
            Height = size / scale;
            // 定位：原图位置 + 双击点 - 窗半径
            Position = new PixelPoint(
                scrap.Location.X + _clickPoint.X - size / 2,
                scrap.Location.Y + _clickPoint.Y - size / 2);
        }

        private bool _dragging;
        private PixelPoint _dragStart;

        private void OnPressed(object sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed)
            {
                _dragging = true;
                var scale = this.RenderScale();
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
            var scale = this.RenderScale();
            var client = e.GetPosition(this);
            var screen = new PixelPoint(
                Position.X + (int)Math.Round(client.X * scale),
                Position.Y + (int)Math.Round(client.Y * scale));
            Position = new PixelPoint(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
            // 反向写回原图位置（旧 ConvertCompactPosToScrap：原图跟随，双击点固定）
            scrap.Location = new System.Drawing.Point(
                Position.X - _clickPoint.X + CompactGeometry.WindowSize / 2,
                Position.Y - _clickPoint.Y + CompactGeometry.WindowSize / 2);
        }

        private void OnReleased(object sender, PointerReleasedEventArgs e)
        {
            _dragging = false;
            e.Pointer.Capture(null);
        }

        /// <summary>双击：Ctrl+双击 = 挂回绳子；普通双击 = 还原（对齐旧 CompactScrap）。</summary>
        private void OnDoubleTapped(object sender, global::Avalonia.Input.TappedEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control))
            {
                scrap.SetClotheslineHanging(true);
                Close();
                return;
            }
            if (scrap.IsClotheslineHanging)
            {
                scrap.SetClotheslineHanging(false);
            }
            else
            {
                scrap.Visible = true;
            }
            Close();
        }

        /// <summary>Esc/Enter 关闭（旧 CompactScrap_KeyDown 语义）。</summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter)
            {
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
