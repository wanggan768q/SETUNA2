using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SETUNA.Main.Clothesline
{
    /// <summary>
    /// 晾绳上的一个条目：所属 ScrapBase 与缩略图绘制所需的图像引用。
    /// Thumbnail 所有权归 Controller，窗体只读不释放。
    /// </summary>
    public class ClotheslineItem
    {
        public ScrapBase Scrap { get; }
        public Image Thumbnail { get; }

        public ClotheslineItem(ScrapBase scrap, Image thumbnail)
        {
            Scrap = scrap;
            Thumbnail = thumbnail;
        }
    }

    /// <summary>
    /// 绳子窗体：透明底 GDI 绘制绳线/夹子/白框，整体拖动，滚轮横向滚动。
    /// 布局与命中计算提取为静态纯函数供测试。
    /// </summary>
    public class ClotheslineForm : BaseForm
    {
        public const int FramePadding = 5;
        public const int ItemGap = 12;
        public const int ContentPadding = 16;
        public const int RopeY = 18;
        public const int RopeX = 18;    // 竖向绳线 X 位（对称 RopeY）
        public const int ClipWidth = 10;
        public const int ClipHeight = 14;
        public const int MaxThumbWidth = 180;
        public const int MaxThumbHeight = 140;

        private const int BottomMargin = 8;
        private const int RightMargin = 8;

        private List<ClotheslineItem> _items = new List<ClotheslineItem>();
        private List<Rectangle> _itemRects = new List<Rectangle>();
        private int _contentLength;
        private int _scrollOffset;
        private Point _dragStartPoint;
        private bool _dragging;
        private bool _vertical;

        /// <summary>当前是否竖向（绳线在左、条目纵向排列、最新在下）。</summary>
        public bool IsVertical => _vertical;

        /// <summary>双击缩略图（照片矩形范围内）触发。</summary>
        public event Action<ScrapBase> ThumbnailDoubleClicked;

        /// <summary>整体拖动结束触发，供 Controller 回传新位置。</summary>
        public event Action DragFinished;

        public ClotheslineForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
        }

        /// <summary>当前窗体位置（宿主持久化用）。</summary>
        public Point RopeLocation => Location;

        /// <summary>设置方向并立即重排：滚动清零（内容长度变化），位置按工作区重新夹紧。</summary>
        public void SetOrientation(bool vertical)
        {
            if (_vertical == vertical)
            {
                return;
            }
            _vertical = vertical;
            LayoutItems();
            _scrollOffset = 0;
            Invalidate();
        }

        /// <summary>替换全部条目并重排；滚动偏移夹紧到新内容范围。</summary>
        public void SetItems(List<ClotheslineItem> items)
        {
            _items = items ?? new List<ClotheslineItem>();
            LayoutItems();
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, MaxScrollOffset()));
            Invalidate();
        }

        /// <summary>内容不变、仅某条目图像更新时调用。</summary>
        public void RefreshItems()
        {
            Invalidate();
        }

        /// <summary>横向滚动到最右（最新照片）。</summary>
        public void ScrollToLatest()
        {
            _scrollOffset = MaxScrollOffset();
            Invalidate();
        }

        /// <summary>滚动使指定条目进入可视区（截图列表激活挂起项时定位，不显示原图）。</summary>
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
                else if (rect.Bottom - _scrollOffset > ClientSize.Height - ContentPadding)
                {
                    _scrollOffset = Math.Max(0, Math.Min(rect.Bottom - ClientSize.Height + ContentPadding, max));
                }
            }
            else
            {
                if (rect.X - _scrollOffset < ContentPadding)
                {
                    _scrollOffset = Math.Max(0, Math.Min(rect.X - ContentPadding, max));
                }
                else if (rect.Right - _scrollOffset > ClientSize.Width - ContentPadding)
                {
                    _scrollOffset = Math.Max(0, Math.Min(rect.Right - ClientSize.Width + ContentPadding, max));
                }
            }
            Invalidate();
        }

        /// <summary>命中检测：屏幕坐标 → 命中的条目；夹子与间隙不算命中。</summary>
        public ClotheslineItem FindItemAt(Point screenPoint)
        {
            var index = FindItemIndexAt(_itemRects, PointToClient(screenPoint), _scrollOffset, _vertical);
            return index >= 0 ? _items[index] : null;
        }

        /// <summary>条目数（供 Controller 越界检查）。</summary>
        public int ItemCount => _items.Count;

        /// <summary>指定条目照片矩形的屏幕坐标（含当前滚动偏移）。</summary>
        public Rectangle GetItemScreenRectangle(int index)
        {
            if (index < 0 || index >= _itemRects.Count)
            {
                return Rectangle.Empty;
            }
            var rect = _itemRects[index];
            if (_vertical)
            {
                rect.Offset(0, -_scrollOffset);
            }
            else
            {
                rect.Offset(-_scrollOffset, 0);
            }
            rect.Offset(Location);
            return rect;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var max = MaxScrollOffset();
            if (max <= 0)
            {
                return;
            }
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset + Math.Sign(e.Delta) * 120, max));
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragStartPoint = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                var location = Location;
                Location = new Point(location.X + e.X - _dragStartPoint.X, location.Y + e.Y - _dragStartPoint.Y);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
                DragFinished?.Invoke();
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            var clientPoint = PointToClient(Cursor.Position);
            var index = FindItemIndexAt(_itemRects, clientPoint, _scrollOffset, _vertical);
            if (index >= 0)
            {
                ThumbnailDoubleClicked?.Invoke(_items[index].Scrap);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 绳线：一条略带弧度的贝塞尔（横向水平，竖向垂直）
            using (var ropePen = new Pen(Color.FromArgb(120, 80, 40), 2f))
            {
                if (_vertical)
                {
                    var h = ClientSize.Height;
                    g.DrawBezier(ropePen, RopeX, -10, RopeX - 4, h / 3, RopeX + 4, h * 2 / 3, RopeX, h + 10);
                }
                else
                {
                    g.DrawBezier(ropePen, -10, RopeY, ClientSize.Width / 3, RopeY - 4, ClientSize.Width * 2 / 3, RopeY + 4, ClientSize.Width + 10, RopeY);
                }
            }

            for (var i = 0; i < _itemRects.Count && i < _items.Count; i++)
            {
                var rect = _itemRects[i];
                // 应用滚动偏移（与命中检测 GetItemScreenRectangle 同一换算）
                if (_vertical)
                {
                    rect.Offset(0, -_scrollOffset);
                    // 跳过视口外条目
                    if (rect.Bottom < 0 || rect.Y > ClientSize.Height)
                    {
                        continue;
                    }
                    // 夹子（照片矩形左侧，压住绳线；旋转 90°：宽 ClipHeight 高 ClipWidth）
                    using (var clipBrush = new SolidBrush(Color.FromArgb(70, 70, 70)))
                    {
                        g.FillRectangle(clipBrush, RopeX - 2, rect.Y + rect.Height / 2 - ClipWidth / 2, ClipHeight, ClipWidth);
                    }
                }
                else
                {
                    rect.Offset(-_scrollOffset, 0);
                    // 跳过视口外条目
                    if (rect.Right < 0 || rect.X > ClientSize.Width)
                    {
                        continue;
                    }
                    // 夹子（照片矩形上方，压住绳线）
                    using (var clipBrush = new SolidBrush(Color.FromArgb(70, 70, 70)))
                    {
                        g.FillRectangle(clipBrush, rect.X + rect.Width / 2 - ClipWidth / 2, RopeY - 2, ClipWidth, ClipHeight);
                    }
                }
                // 照片白色边框
                using (var framePen = new Pen(Color.White, FramePadding * 2 - 2))
                {
                    g.DrawRectangle(framePen, rect);
                }
                var thumbnail = _items[i].Thumbnail;
                if (thumbnail != null)
                {
                    try
                    {
                        g.DrawImage(thumbnail, rect);
                    }
                    catch (ArgumentException)
                    {
                        // 缩略图已被释放（正在替换/移除），跳过本次绘制
                    }
                }
            }
        }

        /// <summary>重排条目矩形与窗体尺寸；内容超出工作区时窗体沿主轴夹紧（靠滚动浏览）。</summary>
        private void LayoutItems()
        {
            _itemRects = LayoutItemRects(_items, _vertical);

            int contentLength;    // 内容主轴长度（横=宽，竖=高）
            int maxCross;         // 最大交叉轴尺寸
            if (_vertical)
            {
                contentLength = _itemRects.Count > 0 ? _itemRects[_itemRects.Count - 1].Bottom - RopeY + ContentPadding : 0;
                maxCross = _itemRects.Count > 0 ? _itemRects.Max(r => r.Width) : 0;
            }
            else
            {
                contentLength = _itemRects.Count > 0 ? _itemRects[_itemRects.Count - 1].Right - RopeY + ContentPadding : 0;
                maxCross = _itemRects.Count > 0 ? _itemRects.Max(r => r.Height) : 0;
            }
            _contentLength = contentLength;

            var workArea = Screen.FromPoint(Location).WorkingArea;
            int width, height;
            if (_vertical)
            {
                width = Math.Max(Math.Min(RopeX + ClipHeight + maxCross + ContentPadding, workArea.Width), _items.Count > 0 ? ContentPadding * 2 : 0);
                height = Math.Max(Math.Min(_contentLength, workArea.Height), 40);
            }
            else
            {
                width = Math.Max(Math.Min(_contentLength, workArea.Width), _items.Count > 0 ? ContentPadding * 2 : 0);
                height = RopeY + ClipHeight + maxCross + BottomMargin;
                if (height < 40)
                {
                    height = 40;
                }
            }
            // 加宽/加高后仍保持整个窗体落在工作区内
            var location = Location;
            var newX = Math.Max(workArea.Left, Math.Min(location.X, workArea.Right - width - (_vertical ? RightMargin : 0)));
            var newY = Math.Max(workArea.Top, Math.Min(location.Y, workArea.Bottom - height - (_vertical ? 0 : BottomMargin)));
            SetBounds(newX, newY, width, height);
        }

        private int MaxScrollOffset()
        {
            var clientMain = _vertical ? ClientSize.Height : ClientSize.Width;
            var max = _contentLength - clientMain;
            return max > 0 ? max : 0;
        }

        // —— 纯计算（供测试） ——

        /// <summary>布局纯函数：条目矩形序列。横向从左到右（最新在右）；竖向从上到下（最新在下）。</summary>
        public static List<Rectangle> LayoutItemRects(List<ClotheslineItem> items, bool vertical)
        {
            var rects = new List<Rectangle>();
            var x = ContentPadding;
            var y = RopeY + ClipHeight;
            foreach (var item in items)
            {
                var size = item.Thumbnail != null
                    ? CalculateThumbnailSize(item.Thumbnail.Size)
                    : new Size(MaxThumbWidth, MaxThumbHeight);
                if (vertical)
                {
                    rects.Add(new Rectangle(RopeX + ClipHeight, y, size.Width, size.Height));
                    y += size.Height + ItemGap;
                }
                else
                {
                    rects.Add(new Rectangle(x, RopeY + ClipHeight, size.Width, size.Height));
                    x += size.Width + ItemGap;
                }
            }
            return rects;
        }

        /// <summary>等比缩略图尺寸：等比缩小到上限内，不裁切、不放大。</summary>
        public static Size CalculateThumbnailSize(Size imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return new Size(MaxThumbWidth, MaxThumbHeight);
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
            return new Size(width, height);
        }

        /// <summary>给定条目矩形（未应用滚动）与滚动偏移，计算命中条目下标；夹子（矩形与绳线之间区域）与间隙不算。竖向按 Y 偏移换算。</summary>
        public static int FindItemIndexAt(List<Rectangle> itemRects, Point clientPoint, int scrollOffset, bool vertical)
        {
            for (var i = 0; i < itemRects.Count; i++)
            {
                var rect = itemRects[i];
                // 矩形按滚动偏移换算到客户区坐标后判定
                var scrolled = vertical
                    ? new Rectangle(rect.X, rect.Y - scrollOffset, rect.Width, rect.Height)
                    : new Rectangle(rect.X - scrollOffset, rect.Y, rect.Width, rect.Height);
                if (scrolled.Contains(clientPoint))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
