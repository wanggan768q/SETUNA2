using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SETUNA.Main;
using SETUNA.Main.Option;
using SETUNA.Avalonia.Views;

namespace SETUNA.Avalonia
{
    /// <summary>
    /// 晾绳控制器（对应旧 ClotheslineController 的 Avalonia 版）：
    /// 挂起集合、绳子窗生命周期、方向解析、捕获暂停。
    /// 复用旧版几何常量与纯函数（缩略尺寸/布局/命中在 ClotheslineWindow 内同实现）。
    /// </summary>
    public sealed class AvaloniaClotheslineController : IDisposable
    {
        private readonly Func<Point> _getRopeInitialPosition;
        private readonly Action<Point> _ropePositionChanged;
        private readonly Func<ClotheslineRopeOrientation> _getOrientationSource;
        private readonly List<AvaloniaClotheslineItem> _items = new();
        private ClotheslineWindow _ropeForm;
        private bool _visible;
        private bool _capturePaused;
        private bool _disposed;

        public AvaloniaClotheslineController(
            Func<Point> getRopeInitialPosition,
            Action<Point> ropePositionChanged,
            Func<ClotheslineRopeOrientation> getOrientation = null)
        {
            _getRopeInitialPosition = getRopeInitialPosition ?? (() => new Point(0, 0));
            _ropePositionChanged = ropePositionChanged;
            _getOrientationSource = getOrientation;
        }

        public bool IsRopeVertical => _ropeForm?.IsVertical ?? false;

        /// <summary>双击缩略图（宿主接"取下"）。</summary>
        public event Action<IScrap> ThumbnailDoubleClicked;

        public void Show()
        {
            _visible = true;
            if (_capturePaused)
            {
                return;
            }
            EnsureRopeForm().Show();
            UpdateRopeItems();
        }

        public void Hide()
        {
            _visible = false;
            _ropeForm?.Hide();
        }

        /// <summary>捕获暂停：截图取样前调用，避免绳子进入画面。</summary>
        public void SetCapturePaused(bool paused)
        {
            _capturePaused = paused;
            if (paused)
            {
                _ropeForm?.Hide();
            }
            else if (_visible)
            {
                EnsureRopeForm().Show();
                UpdateRopeItems();
            }
        }

        public void AttachScrap(IScrap scrap)
        {
            if (scrap == null || _items.Any(x => ReferenceEquals(x.Scrap, scrap)))
            {
                return;
            }
            _items.Add(CreateItem(scrap));
            _items.Sort((x, y) => x.Scrap.DateTime.CompareTo(y.Scrap.DateTime));
            UpdateRopeItems();
            if (_visible)
            {
                _ropeForm.ScrollToLatest();
            }
        }

        public void UpdateScrap(IScrap scrap)
        {
            var index = _items.FindIndex(x => ReferenceEquals(x.Scrap, scrap));
            if (index < 0)
            {
                return;
            }
            _items[index].Thumbnail.Dispose();
            _items[index] = CreateItem(scrap);
            UpdateRopeItems();
        }

        public void DetachScrap(IScrap scrap) => RemoveItem(scrap);

        public void RemoveScrap(IScrap scrap) => RemoveItem(scrap);

        public void Clear()
        {
            DisposeThumbnails();
            _items.Clear();
            UpdateRopeItems();
        }

        private AvaloniaClotheslineItem CreateItem(IScrap scrap)
        {
            return new AvaloniaClotheslineItem(scrap, CreateThumbnail(scrap.Image));
        }

        /// <summary>等比缩略图（上限 180×140，与旧 Controller.CreateThumbnail 同尺寸规则）。</summary>
        private static global::Avalonia.Media.Imaging.Bitmap CreateThumbnail(System.Drawing.Image sourceImage)
        {
            if (sourceImage == null)
            {
                return null;
            }
            var size = Views.ClotheslineWindow.CalculateThumbnailSize(sourceImage.Size);
            using var scaled = new System.Drawing.Bitmap(size.Width, size.Height);
            using (var g = System.Drawing.Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(sourceImage, 0, 0, size.Width, size.Height);
            }
            using var stream = new System.IO.MemoryStream();
            scaled.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            return new global::Avalonia.Media.Imaging.Bitmap(stream);
        }

        private void RemoveItem(IScrap scrap)
        {
            var index = _items.FindIndex(x => ReferenceEquals(x.Scrap, scrap));
            if (index < 0)
            {
                return;
            }
            _items[index].Thumbnail.Dispose();
            _items.RemoveAt(index);
            UpdateRopeItems();
        }

        private void UpdateRopeItems()
        {
            if (_ropeForm == null || !_ropeForm.IsVisible)
            {
                return;
            }
            _ropeForm.SetItems(new List<AvaloniaClotheslineItem>(_items));
        }

        private ClotheslineWindow EnsureRopeForm()
        {
            if (_ropeForm != null)
            {
                return _ropeForm;
            }

            _ropeForm = new ClotheslineWindow
            {
                Position = _getRopeInitialPosition().ToPixelPoint(),
            };
            _ropeForm.SetOrientation(ResolveVertical(_getOrientationSource?.Invoke() ?? ClotheslineRopeOrientation.Auto));
            _ropeForm.ThumbnailDoubleClicked += scrap =>
            {
                ThumbnailDoubleClicked?.Invoke(scrap);
            };
            _ropeForm.PositionChanged += (s, e) =>
            {
                // 拖动结束位置回传宿主（对齐旧 Controller._ropePositionChanged）
                _ropePositionChanged?.Invoke(_ropeForm.RopeLocation);
            };
            return _ropeForm;
        }

        /// <summary>方向解析（对齐旧 ClotheslineController.ResolveVertical 语义）。</summary>
        public bool ResolveVertical(ClotheslineRopeOrientation orientation)
        {
            if (orientation == ClotheslineRopeOrientation.Vertical)
            {
                return true;
            }
            if (orientation == ClotheslineRopeOrientation.Horizontal)
            {
                return false;
            }
            // Auto：宽屏横绳、竖屏竖绳
            var monitor = SETUNA.Core.Platform.PlatformServices.IsRegistered
                ? SETUNA.Core.Platform.PlatformServices.Instance.ScreenCapture.GetMonitors()[0]
                : null;
            var work = monitor?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            return work.Height > work.Width;
        }

        /// <summary>默认绳子位置：主屏顶部居中（横）/左侧居中（竖）。</summary>
        public static Point CalculateDefaultRopePosition(bool vertical, Rectangle workArea)
        {
            if (vertical)
            {
                return new Point(workArea.Left + 8, workArea.Top + workArea.Height / 4);
            }
            return new Point(workArea.Left + workArea.Width / 4, workArea.Top + 8);
        }

        private void DisposeThumbnails()
        {
            foreach (var item in _items)
            {
                item.Thumbnail?.Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            DisposeThumbnails();
            _items.Clear();
            _ropeForm?.Close();
            _ropeForm = null;
        }
    }
}
