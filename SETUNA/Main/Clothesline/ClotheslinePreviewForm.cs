using System;
using System.Drawing;
using System.Windows.Forms;
using SETUNA.Main.Option;

namespace SETUNA.Main.Clothesline
{
    /// <summary>
    /// 单实例只读预览窗体：等比完整显示、不抢焦点、始终置顶；传入图像所有权归调用方，窗体不 Dispose。
    /// </summary>
    public class ClotheslinePreviewForm : BaseForm
    {
        private const int FramePadding = 4;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private Image _image;

        public ClotheslinePreviewForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var createParams = base.CreateParams;
                createParams.ExStyle |= WS_EX_NOACTIVATE;
                return createParams;
            }
        }

        /// <summary>实际显示矩形的屏幕坐标（含白边框），供计时器命中检测。</summary>
        public Rectangle PreviewScreenRectangle => new Rectangle(Location, Size);

        /// <summary>显示指定图像；workArea 限制尺寸与坐标，定位参数按模式使用。</summary>
        public void ShowPreview(Image image, Rectangle workArea, ClotheslinePreviewLocation mode, Point thumbnailAnchor, Point originalLocation)
        {
            if (image == null || workArea.Width <= 0 || workArea.Height <= 0)
            {
                return;
            }

            _image = image;
            var display = CalculateDisplayRectangle(image.Size, workArea);
            var location = CalculatePreviewLocation(mode, display.Size, workArea, thumbnailAnchor, originalLocation);
            SetBounds(location.X - FramePadding, location.Y - FramePadding, display.Width + FramePadding * 2, display.Height + FramePadding * 2);
            if (Visible)
            {
                Invalidate();
            }
            else
            {
                Show();
            }
        }

        /// <summary>隐藏预览；不 Dispose 传入图像。</summary>
        public void ClosePreview()
        {
            _image = null;
            Hide();
        }

        // —— 纯计算（供测试） ——

        /// <summary>等比显示尺寸：优先原图尺寸，超出工作区时等比缩小，不裁切、不放大。</summary>
        public static Rectangle CalculateDisplayRectangle(Size imageSize, Rectangle workArea)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || workArea.Width <= 0 || workArea.Height <= 0)
            {
                return Rectangle.Empty;
            }

            var width = imageSize.Width;
            var height = imageSize.Height;
            if (width > workArea.Width || height > workArea.Height)
            {
                if ((long)imageSize.Width * workArea.Height >= (long)imageSize.Height * workArea.Width)
                {
                    // 宽度受限
                    width = workArea.Width;
                    height = Math.Min(workArea.Height, (int)Math.Round((long)imageSize.Height * workArea.Width / (double)imageSize.Width));
                }
                else
                {
                    // 高度受限
                    height = workArea.Height;
                    width = Math.Min(workArea.Width, (int)Math.Round((long)imageSize.Width * workArea.Height / (double)imageSize.Height));
                }
            }

            return new Rectangle(0, 0, width, height);
        }

        /// <summary>三种定位模式计算预览左上角，并夹紧进工作区（要求 displaySize 不大于工作区）。</summary>
        public static Point CalculatePreviewLocation(ClotheslinePreviewLocation mode, Size displaySize, Rectangle workArea, Point thumbnailAnchor, Point originalLocation)
        {
            if (displaySize.IsEmpty || workArea.Width <= 0 || workArea.Height <= 0)
            {
                return workArea.Location;
            }

            Point location;
            switch (mode)
            {
                case ClotheslinePreviewLocation.NearThumbnail:
                    location = new Point(thumbnailAnchor.X - displaySize.Width / 2, thumbnailAnchor.Y - displaySize.Height / 2);
                    break;
                case ClotheslinePreviewLocation.OriginalLocation:
                    location = originalLocation;
                    break;
                default:
                    // MonitorCenter：目标显示器居中
                    location = new Point(workArea.X + (workArea.Width - displaySize.Width) / 2, workArea.Y + (workArea.Height - displaySize.Height) / 2);
                    break;
            }

            return ClampInto(workArea, location, displaySize);
        }

        private static Point ClampInto(Rectangle workArea, Point location, Size size)
        {
            var x = Math.Max(workArea.Left, Math.Min(location.X, workArea.Right - size.Width));
            var y = Math.Max(workArea.Top, Math.Min(location.Y, workArea.Bottom - size.Height));
            return new Point(x, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(BackColor);
            if (_image != null)
            {
                g.DrawImage(_image, FramePadding, FramePadding, ClientSize.Width - FramePadding * 2, ClientSize.Height - FramePadding * 2);
            }
        }
    }
}
