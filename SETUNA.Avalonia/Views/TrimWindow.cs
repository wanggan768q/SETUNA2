using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SETUNA.Main;
using SETUNA.Main.Style;

namespace SETUNA.Avalonia.Views
{
    /// <summary>
    /// 裁剪窗（对应旧 TrimWindow）：覆盖原图位置，拖拽画选区，OK 返回裁剪矩形。
    /// </summary>
    public class TrimWindow : Window
    {
        private readonly IScrap _scrap;
        private Canvas _canvas;
        private global::Avalonia.Controls.Shapes.Rectangle _selection;
        private global::Avalonia.Point _start;
        private bool _dragging;

        public System.Drawing.Rectangle TrimRectangle { get; private set; }

        public int TrimLeft => TrimRectangle.X;

        public int TrimTop => TrimRectangle.Y;

        public TrimWindow(IScrap scrap)
        {
            _scrap = scrap;

            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = Brushes.Transparent;
            Topmost = true;
            CanResize = false;
            Title = "裁剪";

            // 覆盖原图（物理坐标）
            Position = new PixelPoint(scrap.Location.X, scrap.Location.Y);
            Width = scrap.Image?.Width ?? 100;
            Height = scrap.Image?.Height ?? 100;

            _canvas = new Canvas { Background = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)) };
            _selection = new global::Avalonia.Controls.Shapes.Rectangle
            {
                Stroke = Brushes.Orange,
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                IsVisible = false,
            };
            _canvas.Children.Add(_selection);
            Content = _canvas;

            PointerPressed += OnPressed;
            PointerMoved += OnMoved;
            PointerReleased += OnReleased;
            KeyDown += OnKeyDown;
        }

        private void OnPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                Close(2);
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
            var w = Math.Abs(pos.X - _start.X);
            var h = Math.Abs(pos.Y - _start.Y);
            Canvas.SetLeft(_selection, x);
            Canvas.SetTop(_selection, y);
            _selection.Width = w;
            _selection.Height = h;
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
            if (w < 4 || h < 4)
            {
                Close(2);
                return;
            }
            TrimRectangle = new System.Drawing.Rectangle(
                (int)Canvas.GetLeft(_selection), (int)Canvas.GetTop(_selection), w, h);
            Close(1);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close(2);
            }
        }
    }
}
