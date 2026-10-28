using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SETUNA.Core.Platform;
using SETUNA.Main;
using DrawingRect = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using DrawingPoint = System.Drawing.Point;

namespace SETUNA.Avalonia.Views
{
    /// <summary>绳上条目：所属贴图 ID 与缩略图。</summary>
    public class AvaloniaClotheslineItem
    {
        public IScrap Scrap { get; }
        public Bitmap Thumbnail { get; }

        public AvaloniaClotheslineItem(IScrap scrap, Bitmap thumbnail)
        {
            Scrap = scrap;
            Thumbnail = thumbnail;
        }
    }

    /// <summary>
    /// 晾绳窗（对应旧 ClotheslineForm）：透明窗，画绳线/夹子/白框缩略图，
    /// 整体拖动，滚轮沿主轴滚动，双击取下。布局复用旧版几何常量（等比缩略上限 180×140）。
    /// </summary>
    public class ClotheslineWindow : Window
    {
        public const int FramePadding = 5;
        public const int ItemGap = 12;
        public const int ContentPadding = 16;
        public const int RopeY = 18;
        public const int RopeX = 18;
        public const int ClipWidth = 10;
        public const int ClipHeight = 14;
        public const int MaxThumbWidth = 180;
        public const int MaxThumbHeight = 140;
        public const int BottomMargin = 8;
        public const int RightMargin = 8;

        /// <summary>双击缩略图（宿主接"取下"）。</summary>
        public event Action<IScrap> ThumbnailDoubleClicked;

        private readonly List<AvaloniaClotheslineItem> _items = new();
        private List<DrawingRect> _itemRects = new();
        private bool _vertical;
        private int _scrollOffset;
        private int _contentLength;
        private bool _draggingRope;
        private PixelPoint _dragStart;

        private static ClotheslineWindow InstanceRef;

        public ClotheslineWindow()
        {
            InstanceRef = this;
            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            Title = "晾绳";

            PointerPressed += OnPressed;
            PointerMoved += OnMoved;
            PointerReleased += OnReleased;
            PointerWheelChanged += OnWheel;
            DoubleTapped += OnDoubleTapped;
        }

        public bool IsVertical => _vertical;

        public DrawingPoint RopeLocation => new System.Drawing.Point(Position.X, Position.Y);

        public int ItemCount => _items.Count;

        public void SetOrientation(bool vertical)
        {
            _vertical = vertical;
            LayoutItems();
            InvalidateVisual();
        }

        public void SetItems(List<AvaloniaClotheslineItem> items)
        {
            // 缩略图生命周期归控制器（Attach/Update/Remove 各自释放），此处只换引用
            _items.Clear();
            _items.AddRange(items);
            LayoutItems();
            InvalidateVisual();
        }

        public void ScrollToLatest()
        {
            if (_items.Count == 0)
            {
                return;
            }
            ScrollToIndex(_items.Count - 1);
        }

        public void ScrollToIndex(int index)
        {
            if (index < 0 || index >= _itemRects.Count)
            {
                return;
            }
            var max = MaxScrollOffset();
            var rect = _itemRects[index];
            if (_vertical)
            {
                if (rect.Y - _scrollOffset < ContentPadding)
                {
                    _scrollOffset = Math.Max(0, Math.Min(rect.Y - ContentPadding, max));
                }
                else if (rect.Bottom - _scrollOffset > ClientMainLength() - ContentPadding)
                {
                    _scrollOffset = Math.Max(0, Math.Min(rect.Bottom - (int)Height + ContentPadding, max));
                }
            }
            else
            {
                if (rect.X - _scrollOffset < ContentPadding)
                {
                    _scrollOffset = Math.Max(0, Math.Min(rect.X - ContentPadding, max));
                }
                else if (rect.Right - _scrollOffset > ClientMainLength() - ContentPadding)
                {
                    _scrollOffset = Math.Max(0, Math.Min(rect.Right - (int)Width + ContentPadding, max));
                }
            }
            InvalidateVisual();
        }

        /// <summary>屏幕坐标 → 命中条目索引；夹子与间隙不算。</summary>
        public int FindItemIndexAt(global::Avalonia.Point screenPoint)
        {
            var client = screenPoint - new global::Avalonia.Point(Position.X, Position.Y);
            return FindItemIndexAt(_itemRects, new DrawingPoint((int)client.X, (int)client.Y), _scrollOffset, _vertical);
        }

        /// <summary>屏幕坐标 → 命中条目索引（静态包装：供空格钩子线程做命中检测）。</summary>
        public static int FindItemIndexAtPublic(DrawingPoint screenPoint, DrawingPoint ropeLocation, bool vertical)
        {
            return InstanceRef?.FindItemIndexAtScreen(screenPoint, ropeLocation, vertical) ?? -1;
        }

        /// <summary>按屏幕坐标命中（考虑滚动偏移）。</summary>
        public int FindItemIndexAtScreen(DrawingPoint screenPoint, DrawingPoint ropeLocation, bool vertical)
        {
            var client = new DrawingPoint(screenPoint.X - ropeLocation.X, screenPoint.Y - ropeLocation.Y);
            return FindItemIndexAt(_itemRects, client, _scrollOffset, vertical);
        }

        /// <summary>指定条目照片矩形的屏幕中心（含滚动偏移；供预览锚点定位）。</summary>
        public DrawingPoint GetItemScreenRectangleCenter(int index)
        {
            if (index < 0 || index >= _itemRects.Count)
            {
                return DrawingPoint.Empty;
            }
            var rect = _itemRects[index];
            var x = _vertical ? rect.X : rect.X - _scrollOffset;
            var y = _vertical ? rect.Y - _scrollOffset : rect.Y;
            return new DrawingPoint(x + rect.Width / 2, y + rect.Height / 2);
        }

        // ===== 交互 =====

        private void OnPressed(object sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsRightButtonPressed)
            {
                return;
            }
            _draggingRope = true;
            // 屏幕物理坐标 = 窗口物理位置 + 客户区 DIP × 缩放（对齐 ScrapWindow 拖动修正）
            var scale = RenderScaling > 0 ? RenderScaling : 1.0;
            var client = e.GetPosition(this);
            var screenPt = new PixelPoint(
                Position.X + (int)Math.Round(client.X * scale),
                Position.Y + (int)Math.Round(client.Y * scale));
            _dragStart = screenPt - Position;
            e.Pointer.Capture(this);
        }

        private void OnMoved(object sender, PointerEventArgs e)
        {
            if (!_draggingRope)
            {
                return;
            }
            var scale = RenderScaling > 0 ? RenderScaling : 1.0;
            var client = e.GetPosition(this);
            var screen = new PixelPoint(
                Position.X + (int)Math.Round(client.X * scale),
                Position.Y + (int)Math.Round(client.Y * scale));
            Position = new PixelPoint(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
        }

        private void OnReleased(object sender, PointerReleasedEventArgs e)
        {
            _draggingRope = false;
            e.Pointer.Capture(null);
        }

        private void OnWheel(object sender, PointerWheelEventArgs e)
        {
            var delta = (int)(e.Delta.Y * 60);
            _scrollOffset = Math.Max(0, Math.Min(MaxScrollOffset(), _scrollOffset + (_vertical ? delta : -delta)));
            InvalidateVisual();
            e.Handled = true;
        }

        private void OnDoubleTapped(object sender, global::Avalonia.Input.TappedEventArgs e)
        {
            var screen = (e.Source as Visual) is Visual v ? e.GetPosition(v).ToPixelPoint().ToDrawingPoint() : System.Drawing.Point.Empty;
            // 双击命中直接用窗口客户坐标
            var tapScale = RenderScaling > 0 ? RenderScaling : 1.0;
            var tapPos = e.GetPosition(this);
            var clientPt = new DrawingPoint(
                (int)Math.Round(tapPos.X * tapScale),
                (int)Math.Round(tapPos.Y * tapScale));
            var index = FindItemIndexAt(_itemRects, clientPt, _scrollOffset, _vertical);
            if (index >= 0 && index < _items.Count)
            {
                ThumbnailDoubleClicked?.Invoke(_items[index].Scrap);
            }
        }

        // ===== 布局（几何与旧版 ClotheslineForm.LayoutItemRects 一致） =====

        private int ClientMainLength()
        {
            var scale = RenderScaling > 0 ? RenderScaling : 1.0;
            return (int)Math.Round((_vertical ? Height : Width) * scale);
        }

        private int MaxScrollOffset()
        {
            var scale = RenderScaling > 0 ? RenderScaling : 1.0;
            var clientMain = (int)Math.Round((_vertical ? Height : Width) * scale);
            var max = _contentLength - clientMain;
            return max > 0 ? max : 0;
        }

        private void LayoutItems()
        {
            _itemRects = LayoutItemRects(_items, _vertical);

            var contentLength = 0;
            var maxCross = 0;
            if (_itemRects.Count > 0)
            {
                var last = _itemRects[_itemRects.Count - 1];
                contentLength = (_vertical ? last.Bottom : last.Right) - RopeY + ContentPadding;
                maxCross = _itemRects.Max(r => _vertical ? r.Width : r.Height);
            }
            _contentLength = contentLength;

            var screen = PlatformServices.IsRegistered
                ? PlatformServices.Instance.ScreenCapture.GetMonitorFromPoint(new DrawingPoint(Position.X, Position.Y))
                : null;
            var work = screen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);

            int width, height;
            if (_vertical)
            {
                width = Math.Max(Math.Min(RopeX + ClipHeight + maxCross + ContentPadding, work.Width), _items.Count > 0 ? ContentPadding * 2 : 0);
                height = Math.Max(Math.Min(_contentLength, work.Height), 40);
            }
            else
            {
                width = Math.Max(Math.Min(_contentLength, work.Width), _items.Count > 0 ? ContentPadding * 2 : 0);
                height = Math.Max(RopeY + ClipHeight + maxCross + BottomMargin, 40);
            }
            var newX = Math.Max(work.Left, Math.Min(Position.X, work.Right - width - (_vertical ? RightMargin : 0)));
            var newY = Math.Max(work.Top, Math.Min(Position.Y, work.Bottom - height - (_vertical ? 0 : BottomMargin)));
            Position = new PixelPoint(newX, newY);
            // 内容尺寸按物理像素计算 → 窗口 DIP = px / 缩放
            var scale = RenderScaling > 0 ? RenderScaling : 1.0;
            Width = width / scale;
            Height = height / scale;
        }

        internal static List<DrawingRect> LayoutItemRects(List<AvaloniaClotheslineItem> items, bool vertical)
        {
            var rects = new List<DrawingRect>();
            var x = ContentPadding;
            var y = RopeY + ClipHeight;
            foreach (var item in items)
            {
                var size = item.Thumbnail != null
                    ? CalculateThumbnailSize(new DrawingSize(item.Thumbnail.PixelSize.Width, item.Thumbnail.PixelSize.Height))
                    : new DrawingSize(MaxThumbWidth, MaxThumbHeight);
                if (vertical)
                {
                    rects.Add(new DrawingRect(RopeX + ClipHeight, y, size.Width, size.Height));
                    y += size.Height + ItemGap;
                }
                else
                {
                    rects.Add(new DrawingRect(x, RopeY + ClipHeight, size.Width, size.Height));
                    x += size.Width + ItemGap;
                }
            }
            return rects;
        }

        internal static DrawingSize CalculateThumbnailSize(DrawingSize imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return new DrawingSize(MaxThumbWidth, MaxThumbHeight);
            }
            var width = imageSize.Width;
            var height = imageSize.Height;
            if (width > MaxThumbWidth || height > MaxThumbHeight)
            {
                if ((long)width * MaxThumbHeight >= (long)height * MaxThumbWidth)
                {
                    width = MaxThumbWidth;
                    height = Math.Max(1, (int)Math.Round((long)imageSize.Height * MaxThumbWidth / (double)imageSize.Width));
                }
                else
                {
                    height = MaxThumbHeight;
                    width = Math.Max(1, (int)Math.Round((long)imageSize.Width * MaxThumbHeight / (double)imageSize.Height));
                }
            }
            return new DrawingSize(width, height);
        }

        internal static int FindItemIndexAt(List<DrawingRect> rects, DrawingPoint clientPoint, int scrollOffset, bool vertical)
        {
            for (var i = 0; i < rects.Count; i++)
            {
                var rect = rects[i];
                if (vertical)
                {
                    rect.Offset(0, -scrollOffset);
                }
                else
                {
                    rect.Offset(-scrollOffset, 0);
                }
                if (rect.Contains(clientPoint))
                {
                    return i;
                }
            }
            return -1;
        }

        // ===== 绘制 =====

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            // 绳线：深棕底线 + 浅棕高光错位叠画（拧绳质感）
            var ropePen = new Pen(new SolidColorBrush(Color.FromRgb(0x78, 0x50, 0x28)), 3);
            var ropeHighlightPen = new Pen(new SolidColorBrush(Color.FromRgb(0xA8, 0x78, 0x48)), 1);
            context.DrawGeometry(null, ropePen, BuildRopeGeometry(0, 0));
            context.DrawGeometry(null, ropeHighlightPen,
                BuildRopeGeometry(_vertical ? -1 : 0, _vertical ? 0 : -1));

            var framePen = new Pen(Brushes.White, FramePadding * 2 - 2);
            var shadowBrush = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0));

            for (var i = 0; i < _itemRects.Count && i < _items.Count; i++)
            {
                var rect = _itemRects[i];
                double rx, ry;
                if (_vertical)
                {
                    ry = rect.Y - _scrollOffset;
                    if (rect.Bottom - _scrollOffset < 0 || ry > ClientMainLength())
                    {
                        continue;
                    }
                    rx = rect.X;
                }
                else
                {
                    rx = rect.X - _scrollOffset;
                    if (rect.Right - _scrollOffset < 0 || rx > ClientMainLength())
                    {
                        continue;
                    }
                    ry = rect.Y;
                }

                // 物理像素 rect → 绘制坐标系（DIP）
                var drawScale = RenderScaling > 0 ? RenderScaling : 1.0;
                var avaloniaRect = new Rect(rx / drawScale, ry / drawScale, rect.Width / drawScale, rect.Height / drawScale);
                // 白框投影（环境光方向固定，不随摆动转）
                context.FillRectangle(shadowBrush, new Rect(avaloniaRect.X + 6, avaloniaRect.Y + 7, avaloniaRect.Width, avaloniaRect.Height));
                context.DrawRectangle(null, framePen, avaloniaRect);

                var thumbnail = _items[i].Thumbnail;
                if (thumbnail != null)
                {
                    context.DrawImage(thumbnail, avaloniaRect);
                }
                DrawClip(context, rx, ry, rect, drawScale);
            }
        }

        /// <summary>绳线几何（错位偏移用于高光叠画）。</summary>
        private StreamGeometry BuildRopeGeometry(double offsetX, double offsetY)
        {
            var geometry = new StreamGeometry();
            using (var gctx = geometry.Open())
            {
                if (_vertical)
                {
                    var h = Height;
                    gctx.BeginFigure(new global::Avalonia.Point(RopeX + offsetX, -10 + offsetY), false);
                    gctx.CubicBezierTo(
                        new global::Avalonia.Point(RopeX - 4 + offsetX, h / 3 + offsetY),
                        new global::Avalonia.Point(RopeX + 4 + offsetX, h * 2 / 3 + offsetY),
                        new global::Avalonia.Point(RopeX + offsetX, h + 10 + offsetY));
                }
                else
                {
                    var w = Width;
                    gctx.BeginFigure(new global::Avalonia.Point(-10 + offsetX, RopeY + offsetY), false);
                    gctx.CubicBezierTo(
                        new global::Avalonia.Point(w / 3 + offsetX, RopeY - 4 + offsetY),
                        new global::Avalonia.Point(w * 2 / 3 + offsetX, RopeY + 4 + offsetY),
                        new global::Avalonia.Point(w + 10 + offsetX, RopeY + offsetY));
                }
                gctx.EndFigure(false);
            }
            return geometry;
        }

        /// <summary>木夹子：木色圆角身 + 中缝 + 弹簧箍，压在绳线接触点（随 Task 5 的摆动一起旋转）。</summary>
        private void DrawClip(DrawingContext context, double rx, double ry, DrawingRect rect, double drawScale)
        {
            var bodyBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0x98, 0x58));
            var seamPen = new Pen(new SolidColorBrush(Color.FromRgb(0x8A, 0x62, 0x30)), 1);
            var springBrush = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50));

            Rect bodyRect;   // 物理像素 → DIP
            if (_vertical)
            {
                // 图左中压绳（与原灰矩形位置一致）
                bodyRect = new Rect(
                    (RopeX - 2) / drawScale,
                    (ry + rect.Height / 2.0 - ClipWidth / 2.0) / drawScale,
                    ClipHeight / drawScale, ClipWidth / drawScale);
            }
            else
            {
                // 图顶中压绳
                bodyRect = new Rect(
                    (rx + rect.Width / 2.0 - ClipWidth / 2.0) / drawScale,
                    (RopeY - 2) / drawScale,
                    ClipWidth / drawScale, ClipHeight / drawScale);
            }

            context.FillRectangle(bodyBrush, bodyRect, 2);

            // 中缝：沿夹子短轴方向贯穿长轴中线
            if (_vertical)
            {
                var midX = bodyRect.X + bodyRect.Width / 2;
                context.DrawLine(seamPen, new global::Avalonia.Point(midX, bodyRect.Y + 2),
                    new global::Avalonia.Point(midX, bodyRect.Bottom - 2));
            }
            else
            {
                var midY = bodyRect.Y + bodyRect.Height / 2;
                context.DrawLine(seamPen, new global::Avalonia.Point(bodyRect.X + 2, midY),
                    new global::Avalonia.Point(bodyRect.Right - 2, midY));
            }

            // 弹簧箍：两条位于长轴 1/3 与 2/3 处、沿短轴方向
            for (var i = 1; i <= 2; i++)
            {
                var f = i / 3.0;
                if (_vertical)
                {
                    var y = bodyRect.Y + bodyRect.Height * f;
                    context.FillRectangle(springBrush, new Rect(bodyRect.X, y, bodyRect.Width, 1.5));
                }
                else
                {
                    var x = bodyRect.X + bodyRect.Width * f;
                    context.FillRectangle(springBrush, new Rect(x, bodyRect.Y, 1.5, bodyRect.Height));
                }
            }
        }
    }
}
