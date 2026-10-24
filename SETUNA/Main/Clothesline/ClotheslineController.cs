using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using SETUNA.Main.Option;

namespace SETUNA.Main.Clothesline
{
    /// <summary>晾绳模式迁移动作（宿主 OptionApply 按此更新模型与钩子）。</summary>
    public enum ClotheslineModeAction
    {
        None,
        Enable,
        Disable
    }
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
        private readonly ClotheslinePauseState _pause = new ClotheslinePauseState();
        private bool _disposed;

        /// <summary>双击缩略图（供任务 4 接双击取下）。</summary>
        public event Action<ScrapBase> ThumbnailDoubleClicked;

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

        /// <summary>预览是否正在显示（钩子命中判定：预览打开时空格始终消费）。</summary>
        public bool IsPreviewOpen => _previewForm != null && _previewForm.Visible;

        /// <summary>绳子当前屏幕位置（宿主读取用于持久化判断）。</summary>
        public Point RopeLocation => _ropeForm != null ? _ropeForm.RopeLocation : Point.Empty;

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
            if (_pause.ShouldHide)
            {
                // 全部隐藏或捕获暂停生效中：保持隐藏，退出暂停由 UpdatePauseVisibility 恢复
                return;
            }
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

        /// <summary>全部隐藏（置顶热键套件）：绳子+预览一起隐藏；与捕获暂停分别记录互不清除。</summary>
        public void SetAllHidden(bool hidden)
        {
            _pause.SetAllHidden(hidden);
            UpdatePauseVisibility();
        }

        /// <summary>捕获暂停：截图取样前调用，避免绳子/预览进入画面；EndCapture 恢复。</summary>
        public void SetCapturePaused(bool paused)
        {
            _pause.SetCapturePaused(paused);
            UpdatePauseVisibility();
        }

        private void UpdatePauseVisibility()
        {
            if (_pause.ShouldHide)
            {
                ClosePreview();
                StopAutoCloseTimer();
                _ropeForm?.Hide();
            }
            else if (_visible)
            {
                EnsureRopeForm().Show();
                UpdateRopeItems();
            }
        }

        /// <summary>滚动绳子使指定截图的缩略图进入可视区（截图列表激活挂起项时定位）。</summary>
        public void ScrollToScrap(ScrapBase scrap)
        {
            if (_ropeForm == null || !_ropeForm.Visible || scrap == null)
            {
                return;
            }
            var index = _items.FindIndex(x => ReferenceEquals(x.Scrap, scrap));
            if (index >= 0)
            {
                _ropeForm.ScrollToIndex(index);
            }
        }

        /// <summary>按绳子→预览顺序把可见窗体置顶（LayerManager 刷新末尾经静态回调调用）。</summary>
        public void BringRopeWindowsToFront()
        {
            if (_ropeForm != null && _ropeForm.Visible)
            {
                _ropeForm.TopMost = true;
            }
            if (_previewForm != null && _previewForm.Visible)
            {
                _previewForm.TopMost = true;
            }
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

        /// <summary>挂起集合是否为空（宿主判断模式是否可用）。</summary>
        public bool HasItems => _items.Count > 0;

        // —— 预览管理 ——

        /// <summary>打开预览；重复 Open 同一源不重建。排队请求以最后一次为准。</summary>
        public void OpenPreview(ScrapBase source)
        {
            if (source == null || !_visible || _pause.ShouldHide)
            {
                // 关闭模式/全部隐藏/捕获暂停后到达的排队 Show 请求不得重新打开预览
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
                _ropePositionChanged?.Invoke(_ropeForm.RopeLocation);
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

        /// <summary>
        /// 模式应用决策（纯函数，供测试）：目标开关与当前激活态相同 → 不动（幂等）；
        /// 不同 → 开启/关闭动作。钩子安装失败时的回滚由宿主处理。
        /// </summary>
        public static ClotheslineModeAction DecideModeAction(bool optionEnabled, bool modeActive)
        {
            if (optionEnabled == modeActive)
            {
                return ClotheslineModeAction.None;
            }
            return optionEnabled ? ClotheslineModeAction.Enable : ClotheslineModeAction.Disable;
        }

        /// <summary>方向解析：Auto 按工作区宽高比，显式值直接生效。竖屏（高>宽）为竖。</summary>
        public static bool ResolveVertical(ClotheslineRopeOrientation orientation, Rectangle workArea)
        {
            switch (orientation)
            {
                case ClotheslineRopeOrientation.Horizontal:
                    return false;
                case ClotheslineRopeOrientation.Vertical:
                    return true;
                default:
                    return workArea.Height > workArea.Width;
            }
        }

        /// <summary>默认绳子位置：横=顶部偏左；竖=右边缘垂直居中（估算尺寸 60x200，仅保证落在工作区内）。</summary>
        public static Point CalculateDefaultRopePosition(bool vertical, Rectangle workArea)
        {
            if (!vertical)
            {
                return new Point(workArea.X + 100, workArea.Y + 8);
            }
            const int estimatedWidth = 60;
            const int estimatedHeight = 200;
            var height = Math.Min(estimatedHeight, workArea.Height);
            return new Point(workArea.Right - estimatedWidth - 8, workArea.Y + (workArea.Height - height) / 2);
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