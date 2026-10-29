using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SETUNA.Core.Platform;
using SETUNA.Main.Option;

namespace SETUNA.Avalonia.Views
{
    /// <summary>
    /// 晾绳预览窗（对应旧 ClotheslinePreviewForm）：只读大图、不激活、白边框。
    /// 定位复用旧版纯函数语义（MonitorCenter/NearThumbnail/OriginalLocation + 工作区夹紧）。
    /// </summary>
    public class ClotheslinePreviewWindow : Window
    {
        private const int FramePadding = 4;

        private readonly global::Avalonia.Controls.Image _imageControl;

        public ClotheslinePreviewWindow()
        {
            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            Title = "预览";
            ShowActivated = false; // 不抢焦点（对齐旧 WS_EX_NOACTIVATE）

            _imageControl = new global::Avalonia.Controls.Image
            {
                Stretch = Stretch.Uniform,
            };
            Content = new Border
            {
                Child = _imageControl,
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new global::Avalonia.Thickness(1),
                Padding = new global::Avalonia.Thickness(FramePadding),
            };
        }

        /// <summary>显示预览（物理像素语义）。缩略图锚点/原图位置用于定位模式。</summary>
        public void ShowPreview(Bitmap image, System.Drawing.Rectangle workArea,
            ClotheslinePreviewLocation mode, System.Drawing.Point thumbnailAnchor, System.Drawing.Point originalLocation)
        {
            if (image == null || workArea.Width <= 0 || workArea.Height <= 0)
            {
                return;
            }

            var converted = image;
            _imageControl.Source = converted;
            var display = CalculateDisplayRectangle(converted.PixelSize.Width, converted.PixelSize.Height, workArea);
            var location = CalculatePreviewLocation(mode,
                new System.Drawing.Size(display.Width, display.Height), workArea, thumbnailAnchor, originalLocation);

            // 窗口尺寸 = 显示区 + 边框（物理像素换 DIP）
            var scale = this.RenderScale();
            Position = new PixelPoint(location.X - FramePadding, location.Y - FramePadding);
            Width = (display.Width + FramePadding * 2) / scale;
            Height = (display.Height + FramePadding * 2) / scale;
            if (!IsVisible)
            {
                Show(); // 首次显示；之后只 Update 位置/内容（顶层窗 Hide→Show 不可靠）
            }
        }

        public void ClosePreview()
        {
            _imageControl.Source = null;
            Hide();
        }

        /// <summary>预览窗屏幕矩形（物理像素，供命中/自动关闭判定）。</summary>
        public System.Drawing.Rectangle PreviewScreenRectangle
        {
            get
            {
                var scale = this.RenderScale();
                return new System.Drawing.Rectangle(Position.X, Position.Y,
                    (int)(Width * scale), (int)(Height * scale));
            }
        }

        // ===== 定位纯函数（数值对齐旧 ClotheslinePreviewForm） =====

        internal static System.Drawing.Rectangle CalculateDisplayRectangle(int imgW, int imgH, System.Drawing.Rectangle workArea)
        {
            if (imgW <= 0 || imgH <= 0 || workArea.Width <= 0 || workArea.Height <= 0)
            {
                return System.Drawing.Rectangle.Empty;
            }

            var width = imgW;
            var height = imgH;
            if (width > workArea.Width || height > workArea.Height)
            {
                if ((long)imgW * workArea.Height >= (long)imgH * workArea.Width)
                {
                    width = workArea.Width;
                    height = Math.Min(workArea.Height, (int)Math.Round((long)imgH * workArea.Width / (double)imgW));
                }
                else
                {
                    height = workArea.Height;
                    width = Math.Min(workArea.Width, (int)Math.Round((long)imgW * workArea.Height / (double)imgH));
                }
            }
            return new System.Drawing.Rectangle(0, 0, width, height);
        }

        internal static System.Drawing.Point CalculatePreviewLocation(ClotheslinePreviewLocation mode,
            System.Drawing.Size displaySize, System.Drawing.Rectangle workArea,
            System.Drawing.Point thumbnailAnchor, System.Drawing.Point originalLocation)
        {
            if (displaySize.IsEmpty || workArea.Width <= 0 || workArea.Height <= 0)
            {
                return workArea.Location;
            }

            System.Drawing.Point location;
            switch (mode)
            {
                case ClotheslinePreviewLocation.NearThumbnail:
                    location = new System.Drawing.Point(
                        thumbnailAnchor.X - displaySize.Width / 2,
                        thumbnailAnchor.Y - displaySize.Height / 2);
                    break;
                case ClotheslinePreviewLocation.OriginalLocation:
                    location = originalLocation;
                    break;
                default:
                    location = new System.Drawing.Point(
                        workArea.X + (workArea.Width - displaySize.Width) / 2,
                        workArea.Y + (workArea.Height - displaySize.Height) / 2);
                    break;
            }

            return new System.Drawing.Point(
                Math.Max(workArea.Left, Math.Min(location.X, workArea.Right - displaySize.Width)),
                Math.Max(workArea.Top, Math.Min(location.Y, workArea.Bottom - displaySize.Height)));
        }
    }
}
