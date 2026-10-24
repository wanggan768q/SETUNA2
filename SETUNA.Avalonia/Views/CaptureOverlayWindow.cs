using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SETUNA.Avalonia.Bridging;
using SETUNA.Core.Platform;

namespace SETUNA.Avalonia.Views
{
    /// <summary>
    /// 全屏截图遮罩（对应旧 CaptureForm 的期 5 核心）：
    /// 先抓屏快照（平台服务），再全屏显示快照 + 半透明遮罩，拖拽选区，抬起完成。
    /// ESC/右键取消。结果经 CaptureCompleted 回调（物理像素 GDI Bitmap + 起点）。
    /// ponytail: 放大镜/十字辅助线/窗口吸附期 6 按需补；核心选区语义一致。
    /// </summary>
    public class CaptureOverlayWindow : Window
    {
        /// <summary>截图完成（物理像素区域，含图像）。取消时 image 为 null。</summary>
        public Action<System.Drawing.Bitmap, System.Drawing.Point> CaptureCompleted;

        private readonly System.Drawing.Bitmap _snapshot;
        private readonly System.Drawing.Point _snapshotOrigin;

        private Canvas _canvas;
        private global::Avalonia.Controls.Shapes.Rectangle _dim;
        private global::Avalonia.Controls.Shapes.Rectangle _selection;
        private global::Avalonia.Controls.Image _snapshotView;
        private global::Avalonia.Point _start;
        private bool _dragging;

        public CaptureOverlayWindow(System.Drawing.Bitmap snapshot, System.Drawing.Point snapshotOrigin, MonitorInfo monitor)
        {
            _snapshot = snapshot;
            _snapshotOrigin = snapshotOrigin;

            SystemDecorations = SystemDecorations.None;
            Topmost = true;
            CanResize = false;
            ShowInTaskbar = false;
            WindowState = WindowState.Normal;

            Position = new PixelPoint(monitor.Bounds.X, monitor.Bounds.Y);
            Width = monitor.Bounds.Width;
            Height = monitor.Bounds.Height;

            using var stream = new System.IO.MemoryStream();
            snapshot.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            _snapshotView = new global::Avalonia.Controls.Image
            {
                Source = new Bitmap(stream),
                Width = monitor.Bounds.Width,
                Height = monitor.Bounds.Height,
            };

            _dim = new global::Avalonia.Controls.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                Width = monitor.Bounds.Width,
                Height = monitor.Bounds.Height,
            };
            _selection = new global::Avalonia.Controls.Shapes.Rectangle
            {
                Stroke = Brushes.Orange,
                StrokeThickness = 1,
                IsVisible = false,
            };

            _canvas = new Canvas();
            _canvas.Children.Add(_snapshotView);
            _canvas.Children.Add(_dim);
            _canvas.Children.Add(_selection);
            Content = _canvas;

            PointerPressed += OnPressed;
            PointerMoved += OnMoved;
            PointerReleased += OnReleased;
            KeyDown += OnKeyDown;
        }

        private void OnPressed(object sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsRightButtonPressed)
            {
                Finish(null);
                return;
            }
            _dragging = true;
            _start = e.GetPosition(_canvas);
            e.Pointer.Capture(_canvas);
        }

        private void OnMoved(object sender, PointerEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }
            var pos = e.GetPosition(_canvas);
            var x = Math.Min(_start.X, pos.X);
            var y = Math.Min(_start.Y, pos.Y);
            Canvas.SetLeft(_selection, x);
            Canvas.SetTop(_selection, y);
            _selection.Width = Math.Abs(pos.X - _start.X);
            _selection.Height = Math.Abs(pos.Y - _start.Y);
            _selection.IsVisible = true;
        }

        private void OnReleased(object sender, PointerReleasedEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }
            _dragging = false;
            e.Pointer.Capture(null);

            var w = (int)_selection.Width;
            var h = (int)_selection.Height;
            // 最小选区 10px（对齐旧 EntryCapture）
            if (w < 10 || h < 10)
            {
                Finish(null);
                return;
            }

            var x = (int)Canvas.GetLeft(_selection);
            var y = (int)Canvas.GetTop(_selection);
            var clip = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = System.Drawing.Graphics.FromImage(clip))
            {
                g.DrawImage(_snapshot, new System.Drawing.Rectangle(0, 0, w, h),
                    new System.Drawing.Rectangle(_snapshotOrigin.X + x, _snapshotOrigin.Y + y, w, h),
                    System.Drawing.GraphicsUnit.Pixel);
            }
            Finish(clip, new System.Drawing.Point(_snapshotOrigin.X + x, _snapshotOrigin.Y + y));
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Finish(null);
            }
        }

        private void Finish(System.Drawing.Bitmap clip) => Finish(clip, System.Drawing.Point.Empty);

        private void Finish(System.Drawing.Bitmap clip, System.Drawing.Point origin)
        {
            var handler = CaptureCompleted;
            CaptureCompleted = null;
            Close();
            handler?.Invoke(clip, origin);
        }
    }
}
