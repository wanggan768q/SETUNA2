using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using SETUNA.Main.Option;

namespace SETUNA.Main.Clothesline
{
    /// <summary>
    /// 晾绳模式控制器：挂起集合、绳子/预览窗体生命周期与自动关闭计时。
    /// 纯 UI 线程组件；不读写 SetunaOption 持久化（初始位置注入，位置变更回传宿主）。
    /// </summary>
    public class ClotheslineController : IDisposable
    {
        private const int AutoCloseCheckIntervalMs = 50;
        private const int AutoCloseDelayMs = 200;

        private readonly Func<Point> _getRopeInitialPosition;
        private readonly Action<Point> _ropePositionChanged;

        private readonly List<ClotheslineItem> _items = new List<ClotheslineItem>();
        private ClotheslineForm _ropeForm;
        private ClotheslinePreviewForm _previewForm;
        private ScrapBase _previewSource;
        private System.Windows.Forms.Timer _autoCloseTimer;
        private long? _outsideSince;
        private Image _previewImage;
        private ClotheslinePreviewLocation _previewMode = ClotheslinePreviewLocation.MonitorCenter;
        private bool _visible;
        private bool _disposed;

        /// <summary>双击缩略图（供任务 4 接双击取下）。</summary>
        public event Action<ScrapBase> ThumbnailDoubleClicked;

        /// <summary>绳子拖动结束，新位置回传宿主持久化。</summary>
        public event Action<Point> RopeDragged;

        /// <summary>预览显示/关闭变化。</summary>
        public event Action<bool> PreviewVisibilityChanged;

        public ClotheslineController(Func<Point> getRopeInitialPosition, Action<Point> ropePositionChanged)
        {
            _getRopeInitialPosition = getRopeInitialPosition ?? (() => Screen.PrimaryScreen.WorkingArea.Location);
            _ropePositionChanged = ropePositionChanged;
        }

        /// <summary>预览定位模式（宿主从 SetunaOption 注入，默认显示器居中）。</summary>
        public ClotheslinePreviewLocation PreviewMode
        {
            get => _previewMode;
            set => _previewMode = value;
        }

        // —— 挂起集合 ——

        public void AttachScrap(ScrapBase scrap)
        {
            if (scrap == null || _items.Any(x => ReferenceEquals(x.Scrap, scrap)))
            {
                return;
            }

            var item = new ClotheslineItem(scrap, CreateThumbnail(scrap.Image));
            _items.Add(item);
            _items.Sort((x, y) => x.Scrap.DateTime.CompareTo(y.Scrap.DateTime));
            UpdateRopeItems();
            if (_visible)
            {
                ScrollToLatest();
            }
        }

        public void UpdateScrap(ScrapBase scrap)
        {
            var index = _items.FindIndex(x => ReferenceEquals(x.Scrap, scrap));
            if (index < 0)
            {
                return;
            }

            _items[index].Thumbnail.Dispose();
            _items[index] = new ClotheslineItem(scrap, CreateThumbnail(scrap.Image));
            UpdateRopeItems();
            if (ReferenceEquals(_previewSource, scrap))
            {
                // 预览显示的是 ScrapBase.Image 的临时 Clone，图像内容变化即重建预览
                if (_previewSource != null && _previewSource.Image != null && _previewForm != null)
                {
                    ShowPreviewCore(_previewSource);
                }
            }
        }

        public void DetachScrap(ScrapBase scrap)
        {
            RemoveItem(scrap, closePreview: false);
        }

        public void RemoveScrap(ScrapBase scrap)
        {
            RemoveItem(scrap, closePreview: true);
        }

        public void Clear()
        {
            ClosePreview();
            DisposeThumbnails();
            _items.Clear();
            UpdateRopeItems();
        }

        public void Show()
        {
            _visible = true;
            EnsureRopeForm().Show();
            UpdateRopeItems();
        }

        public void Hide()
        {
            _visible = false;
            ClosePreview();
            StopAutoCloseTimer();
            _ropeForm?.Hide();
        }

        /// <summary>命中检测：屏幕坐标 → 命中的缩略图对应 ScrapBase（照片矩形范围内才算）。</summary>
        public ScrapBase FindThumbnailAt(Point screenPoint)
        {
            if (_ropeForm == null || !_ropeForm.Visible)
            {
                return null;
            }
            return _ropeForm.FindItemAt(screenPoint)?.Scrap;
        }

        public void ScrollToLatest()
        {
            _ropeForm?.ScrollToLatest();
        }

        // —— 预览管理 ——

        /// <summary>打开预览；重复 Open 同一源不重建。排队请求以最后一次为准。</summary>
        public void OpenPreview(ScrapBase source)
        {
            if (source == null || !_visible)
            {
                return;
            }

            if (ReferenceEquals(_previewSource, source) && _previewForm != null && _previewForm.Visible)
            {
                RestartAutoCloseTimer();
                return;
            }

            ShowPreviewCore(source);
        }

        public void ClosePreview()
        {
            var wasVisible = _previewForm != null && _previewForm.Visible;
            _previewSource = null;
            _previewForm?.ClosePreview();
            _previewImage?.Dispose();
            _previewImage = null;
            StopAutoCloseTimer();
            _outsideSince = null;
            if (wasVisible)
            {
                PreviewVisibilityChanged?.Invoke(false);
            }
        }

        /// <summary>重置自动关闭计时（任务 4 空格切换复用）。</summary>
        public void RestartAutoCloseTimer()
        {
            _outsideSince = null;
            if (_autoCloseTimer == null)
            {
                _autoCloseTimer = new System.Windows.Forms.Timer { Interval = AutoCloseCheckIntervalMs };
                _autoCloseTimer.Tick += AutoCloseTimer_Tick;
            }
            _autoCloseTimer.Start();
        }

        // —— 生命周期 ——

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            Clear();
            StopAutoCloseTimer();
            _autoCloseTimer?.Dispose();
            _autoCloseTimer = null;
            _previewImage?.Dispose();
            _previewImage = null;

            if (_ropeForm != null)
            {
                _ropeForm.ThumbnailDoubleClicked -= RopeForm_ThumbnailDoubleClicked;
                _ropeForm.DragFinished -= RopeForm_DragFinished;
                _ropeForm.Dispose();
                _ropeForm = null;
            }
            if (_previewForm != null)
            {
                _previewForm.Dispose();
                _previewForm = null;
            }
            SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
        }

        private ClotheslineForm EnsureRopeForm()
        {
            if (_ropeForm == null)
            {
                _ropeForm = new ClotheslineForm { Location = _getRopeInitialPosition() };
                _ropeForm.ThumbnailDoubleClicked += RopeForm_ThumbnailDoubleClicked;
                _ropeForm.DragFinished += RopeForm_DragFinished;
                SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
            }
            return _ropeForm;
        }

        private void RopeForm_ThumbnailDoubleClicked(ScrapBase scrap)
        {
            ThumbnailDoubleClicked?.Invoke(scrap);
        }

        private void RopeForm_DragFinished()
        {
            if (_ropeForm != null)
            {
                RopeDragged?.Invoke(_ropeForm.RopeLocation);
            }
        }

        private void DisplaySettingsChanged(object sender, EventArgs e)
        {
            // SystemEvents 在内部线程触发，UI 工作投递回 UI 线程
            var ropeForm = _ropeForm;
            if (ropeForm == null || ropeForm.IsDisposed || !ropeForm.IsHandleCreated)
            {
                return;
            }
            ropeForm.BeginInvoke(new Action(() =>
            {
                if (_disposed || _ropeForm == null || !_ropeForm.Visible)
                {
                    return;
                }
                // 显示器变化：把绳子夹回任一可见工作区，保证可见可拖回
                var location = _ropeForm.Location;
                var clamped = false;
                foreach (var screen in Screen.AllScreens)
                {
                    var area = screen.WorkingArea;
                    if (area.Contains(location))
                    {
                        clamped = true;
                        break;
                    }
                }
                if (!clamped)
                {
                    _ropeForm.Location = Screen.PrimaryScreen.WorkingArea.Location;
                }
            }));
        }

        private void UpdateRopeItems()
        {
            if (_ropeForm != null)
            {
                _ropeForm.SetItems(_items.ToList());
            }
        }

        private void ShowPreviewCore(ScrapBase source)
        {
            if (_previewForm == null)
            {
                _previewForm = new ClotheslinePreviewForm();
            }

            var wasVisible = _previewForm.Visible;
            var workArea = Screen.FromControl(_ropeForm ?? (Control)source).WorkingArea;
            var thumbnailAnchor = GetThumbnailScreenCenter(source);
            var originalLocation = source.TargetLocation;

            _previewSource = source;
            // 预览持有原图的临时 Clone：原图被替换/释放时预览不受影响；Controller 负责释放副本
            _previewImage?.Dispose();
            _previewImage = source.Image != null ? (Image)source.Image.Clone() : null;
            _previewForm.ShowPreview(_previewImage, workArea, _previewMode, thumbnailAnchor, originalLocation);
            if (_previewForm.Visible)
            {
                _previewForm.BringToFront();
                if (!wasVisible)
                {
                    // 仅不可见→可见时触发，切换源不重复通知
                    PreviewVisibilityChanged?.Invoke(true);
                }
                RestartAutoCloseTimer();
            }
        }

        private Point GetThumbnailScreenCenter(ScrapBase source)
        {
            var index = _items.FindIndex(x => ReferenceEquals(x.Scrap, source));
            if (index >= 0 && _ropeForm != null && _ropeForm.Visible && index < _ropeForm.ItemCount)
            {
                var rect = _ropeForm.GetItemScreenRectangle(index);
                return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            }
            return Cursor.Position;
        }

        // —— 自动关闭计时 ——

        private void AutoCloseTimer_Tick(object sender, EventArgs e)
        {
            if (_previewForm == null || !_previewForm.Visible || _previewSource == null)
            {
                StopAutoCloseTimer();
                _outsideSince = null;
                return;
            }

            var previewRect = _previewForm.PreviewScreenRectangle;
            var sourceRect = GetThumbnailScreenRectangle(_previewSource);
            var mouse = Cursor.Position;
            var now = Environment.TickCount;
            _outsideSince = NextOutsideSince(_outsideSince, IsPointerOverPreviewSurface(mouse, sourceRect, previewRect), now);
            // 持续离开超过宽限期才关闭；回到任一矩形即重置（跨间隙有 200ms 宽限）
            if (_outsideSince.HasValue && now - _outsideSince.Value >= AutoCloseDelayMs)
            {
                ClosePreview();
            }
        }

        /// <summary>
        /// 自动关闭宽限状态机（纯函数，供测试）：鼠标在矩形内 → 重置为 null；
        /// 离开中 → 首次记录 now，之后保持不变（供调用方比较持续时间）。
        /// </summary>
        public static long? NextOutsideSince(long? outsideSince, bool pointerOverSurface, long nowMs)
        {
            return pointerOverSurface ? null : (outsideSince ?? nowMs);
        }

        private Rectangle GetThumbnailScreenRectangle(ScrapBase source)
        {
            var index = _items.FindIndex(x => ReferenceEquals(x.Scrap, source));
            if (index >= 0 && _ropeForm != null && _ropeForm.Visible && index < _ropeForm.ItemCount)
            {
                return _ropeForm.GetItemScreenRectangle(index);
            }
            return Rectangle.Empty;
        }

        /// <summary>鼠标是否仍在源缩略图或预览矩形之一内（纯函数，供测试）。</summary>
        public static bool IsPointerOverPreviewSurface(Point mousePoint, Rectangle sourceRect, Rectangle previewRect)
        {
            return (!sourceRect.IsEmpty && sourceRect.Contains(mousePoint)) || (!previewRect.IsEmpty && previewRect.Contains(mousePoint));
        }

        private void StopAutoCloseTimer()
        {
            _autoCloseTimer?.Stop();
        }

        // —— 缩略图 ——

        private Image CreateThumbnail(Image sourceImage)
        {
            if (sourceImage == null)
            {
                return null;
            }
            var size = ClotheslineForm.CalculateThumbnailSize(sourceImage.Size);
            var thumbnail = new Bitmap(size.Width, size.Height);
            using (var g = Graphics.FromImage(thumbnail))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(sourceImage, 0, 0, size.Width, size.Height);
            }
            return thumbnail;
        }

        private void RemoveItem(ScrapBase scrap, bool closePreview)
        {
            var index = _items.FindIndex(x => ReferenceEquals(x.Scrap, scrap));
            if (index < 0)
            {
                return;
            }

            if (closePreview && ReferenceEquals(_previewSource, scrap))
            {
                ClosePreview();
            }
            _items[index].Thumbnail.Dispose();
            _items.RemoveAt(index);
            UpdateRopeItems();
        }

        private void DisposeThumbnails()
        {
            foreach (var item in _items)
            {
                item.Thumbnail?.Dispose();
            }
        }
    }
}