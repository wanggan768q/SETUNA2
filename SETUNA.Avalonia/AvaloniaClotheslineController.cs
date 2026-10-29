using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SETUNA.Main;
using SETUNA.Main.Clothesline;
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
        private ClotheslinePreviewWindow _previewForm;
        private IScrap _previewSource;
        private global::Avalonia.Media.Imaging.Bitmap _previewImage;
        private readonly ClotheslineSpaceKeyStateMachine _spaceStateMachine = new();
        private bool _spaceRoundConsuming;
        private bool _visible;
        private bool _capturePaused;
        private bool _disposed;

        // ===== 边缘呼出（PicRope 交互）=====

        private global::Avalonia.Threading.DispatcherTimer _edgeTimer;
        private bool _edgeRevealEnabled;
        private DateTime _awaySince = DateTime.MaxValue;    // MaxValue=光标在绳/边缘上
        private DateTime _manualShowAt = DateTime.MinValue; // 手动呼出宽限起点

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
            _manualShowAt = DateTime.Now;
            _visible = true;
            if (_capturePaused)
            {
                return;
            }
            EnsureRopeForm();
            _ropeForm?.SetOpacityImmediate(1.0);
            _ropeForm.Show();
            UpdateRopeItems();
            UpdateVisibilitySnapshot();
        }

        public void Hide()
        {
            _visible = false;
            _ropeForm?.Hide();
            UpdateVisibilitySnapshot();
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
                // 捕获暂停可能打断了淡出，恢复时归位透明度，避免半透明常驻
                _ropeForm?.SetOpacityImmediate(1.0);
                EnsureRopeForm().Show();
                UpdateRopeItems();
                UpdateVisibilitySnapshot();
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

        /// <summary>切换预览（空格触发）：当前源已开 → 关闭；否则打开。</summary>
        public void TogglePreview(IScrap source)
        {
            AvaloniaHost.Log($"toggle: source={(source?.Name ?? "null")}, previewOpen={IsPreviewOpen}");
            if (IsPreviewOpen)
            {
                // 预览开着：命中另一张 → 切换；命中同一张/未命中 → 收起
                if (source != null && !ReferenceEquals(_previewSource, source))
                {
                    OpenPreview(source);
                }
                else
                {
                    ClosePreview();
                }
                return;
            }
            if (source == null)
            {
                return;
            }
            OpenPreview(source);
        }

        private void OpenPreview(IScrap source)
        {
            if (_ropeForm == null || !_ropeForm.IsVisible || source?.Image == null)
            {
                AvaloniaHost.Log($"open blocked: ropeNull={_ropeForm == null}, imgNull={source?.Image == null}");
                return;
            }

            _previewImage?.Dispose();
            // 预览显示原始图（旧版语义）。像素直拷（毫秒级）替代 PNG 编码（秒级卡顿）
            var gdi = (System.Drawing.Bitmap)source.Image;
            var wb = new global::Avalonia.Media.Imaging.WriteableBitmap(
                new global::Avalonia.PixelSize(gdi.Width, gdi.Height),
                new global::Avalonia.Vector(96, 96),
                global::Avalonia.Platform.PixelFormat.Bgra8888);
            using (var lockFrame = wb.Lock())
            {
                var bmpData = gdi.LockBits(
                    new System.Drawing.Rectangle(0, 0, gdi.Width, gdi.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    unsafe
                    {
                        var stride = Math.Min(bmpData.Stride, lockFrame.RowBytes);
                        for (var y = 0; y < gdi.Height; y++)
                        {
                            Buffer.MemoryCopy(
                                (void*)(bmpData.Scan0 + y * bmpData.Stride),
                                (void*)(lockFrame.Address + y * lockFrame.RowBytes),
                                lockFrame.RowBytes, stride);
                        }
                    }
                }
                finally
                {
                    gdi.UnlockBits(bmpData);
                }
            }
            _previewImage = wb;
            _previewSource = source;
            if (_previewForm == null)
            {
                _previewForm = new ClotheslinePreviewWindow();
            }

            var capture = SETUNA.Core.Platform.PlatformServices.IsRegistered
                ? SETUNA.Core.Platform.PlatformServices.Instance.ScreenCapture
                : null;
            var workArea = capture?.GetMonitors()[0].WorkingArea ?? ScreenFallback.WorkArea;

            // 缩略图锚点 = 绳上该项中心；原图位置 = 贴图当前位置
            var anchor = System.Drawing.Point.Empty;
            var index = _items.FindIndex(x => ReferenceEquals(x.Scrap, source));
            if (index >= 0)
            {
                anchor = _ropeForm.GetItemScreenRectangleCenter(index);
            }

            _previewForm.ShowPreview(_previewImage, workArea, PreviewMode, anchor, source.Location);
            IsPreviewOpen = true;
            AvaloniaHost.Log($"open done: anchor={anchor}, mode={PreviewMode}");
        }

        public void ClosePreview()
        {
            _previewForm?.ClosePreview();
            _previewSource = null;
            IsPreviewOpen = false;
        }

        /// <summary>预览位置模式（宿主从配置注入）。</summary>
        public ClotheslinePreviewLocation PreviewMode { get; set; } = ClotheslinePreviewLocation.MonitorCenter;

        /// <summary>空格键状态机驱动（由 IHotkeyService.SetKeyFilter 的过滤器回调）：
        /// 返回 true 表示消费该键。hit = 鼠标是否悬停在某缩略图上。</summary>
        public bool OnSpaceKey(bool isDown, bool hit)
        {
            var action = isDown ? _spaceStateMachine.OnKeyDown(hit) : _spaceStateMachine.OnKeyUp();
            if (action == SpaceKeyAction.TogglePreview)
            {
                var hitScrap = FindItemAtCursor();
                // UI 线程切换预览（钩子线程 → UI）
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => TogglePreview(hitScrap));
                return true; // Toggle/Consume 都吞掉
            }
            return action == SpaceKeyAction.Consume;
        }

        /// <summary>鼠标当前位置命中的绳上贴图（无命中 null）。</summary>
        public IScrap FindItemAtCursor()
        {
            // 键盘钩子线程调用：禁止触碰 Avalonia Visual（IsVisible 有线程校验），
            // 用宿主维护的快照状态替代
            var snapshot = _visibilitySnapshot;
            if (!snapshot.RopeVisible)
            {
                return null;
            }
            var capture = SETUNA.Core.Platform.PlatformServices.IsRegistered
                ? SETUNA.Core.Platform.PlatformServices.Instance.ScreenCapture
                : null;
            if (capture == null)
            {
                return null;
            }
            var pos = capture.GetCursorPosition();
            var index = Views.ClotheslineWindow.FindItemIndexAtPublic(pos, snapshot.RopeLocation, snapshot.Vertical);
            return index >= 0 && index < _items.Count ? _items[index].Scrap : null;
        }

        /// <summary>空格轮次是否消费中（延迟卸钩判定用）。</summary>
        public bool IsSpaceRoundConsuming => _spaceStateMachine.ConsumingRound;

        /// <summary>空格轮次是否已见 down（keyup 是否需进状态机收尾；钩子线程只读）。</summary>
        public bool IsSpaceRoundActive => _spaceStateMachine.RoundActive;

        /// <summary>绳窗可见性快照（UI 线程更新，钩子线程只读；避免跨线程触碰 Visual）。</summary>
        private (bool RopeVisible, System.Drawing.Point RopeLocation, bool Vertical) _visibilitySnapshot;

        /// <summary>开/关边缘呼出轮询（宿主在模式应用与选项同步时调用）。</summary>
        public void SetEdgeRevealEnabled(bool enabled)
        {
            if (_edgeRevealEnabled == enabled)
            {
                return;
            }
            _edgeRevealEnabled = enabled;
            if (enabled)
            {
                EnsureEdgeTimer().Start();
            }
            else
            {
                _edgeTimer?.Stop();
            }
        }

        private global::Avalonia.Threading.DispatcherTimer EnsureEdgeTimer()
        {
            if (_edgeTimer == null)
            {
                _edgeTimer = new global::Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100),
                };
                _edgeTimer.Tick += (s, e) => EdgeTick();
            }
            return _edgeTimer;
        }

        /// <summary>100ms 轮询：组装决策输入 → 纯函数决策 → 执行呼出/收起。</summary>
        private void EdgeTick()
        {
            if (_disposed || _capturePaused)
            {
                return;
            }
            var capture = SETUNA.Core.Platform.PlatformServices.IsRegistered
                ? SETUNA.Core.Platform.PlatformServices.Instance.ScreenCapture
                : null;
            if (capture == null)
            {
                return;
            }

            var cursor = capture.GetCursorPosition();
            var ropeLoc = _ropeForm != null ? _ropeForm.RopeLocation : System.Drawing.Point.Empty;
            var vertical = IsRopeVertical;
            var monitor = capture.GetMonitorFromPoint(ropeLoc);
            var work = monitor.WorkingArea;
            var edgeZoneHit = ClotheslineEdgeGeometry.IsEdgeZoneHit(cursor, work, vertical);
            var pointerNearRope = _ropeForm != null && IsPointerNearRopeForm(cursor);
            var fg = SETUNA.Core.Platform.PlatformServices.Instance.Window.GetForegroundWindow();
            // 最大化窗口含隐形边框会覆盖工作区但盖不住整屏，用整屏矩形判定
            var fullscreenCovers = fg != null && ClotheslineEdgeGeometry.CoversWorkArea(fg.Bounds, monitor.Bounds);

            if (edgeZoneHit || pointerNearRope)
            {
                _awaySince = DateTime.MaxValue;
            }
            else if (_awaySince == DateTime.MaxValue)
            {
                _awaySince = DateTime.Now;
            }
            var awayDuration = _awaySince == DateTime.MaxValue ? TimeSpan.Zero : DateTime.Now - _awaySince;
            var withinManualGrace = DateTime.Now - _manualShowAt < ClotheslineEdgeReveal.ManualGrace;

            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                _visible, _capturePaused, edgeZoneHit, pointerNearRope, fullscreenCovers,
                awayDuration, withinManualGrace);

            if (action == EdgeRevealAction.Reveal)
            {
                RevealWithFade();
            }
            else if (action == EdgeRevealAction.Retract)
            {
                RetractWithFade();
            }
        }

        /// <summary>光标是否在绳窗矩形外扩 12px 内（物理像素）。</summary>
        private bool IsPointerNearRopeForm(System.Drawing.Point cursor)
        {
            var scale = _ropeForm.RenderScale();
            var rect = new Rectangle(
                _ropeForm.Position.X,
                _ropeForm.Position.Y,
                (int)Math.Round(_ropeForm.Width * scale),
                (int)Math.Round(_ropeForm.Height * scale));
            rect.Inflate(12, 12);
            return rect.Contains(cursor);
        }

        /// <summary>边缘呼出：先零透明再 Show（立即路径），再淡入。</summary>
        private void RevealWithFade()
        {
            _ropeForm = EnsureRopeForm();
            _ropeForm.Opacity = 0;
            _visible = true;
            _ropeForm.Show();
            UpdateRopeItems();
            UpdateVisibilitySnapshot();
            _ropeForm.AnimateOpacity(fadeIn: true, completed: null);
        }

        /// <summary>边缘收起：关预览 → 淡出 → 完成回调真 Hide（更新可见性快照）。</summary>
        private void RetractWithFade()
        {
            if (_ropeForm == null || !_visible)
            {
                return;
            }
            ClosePreview();
            var retractStartedAt = DateTime.Now;
            _ropeForm.AnimateOpacity(fadeIn: false, completed: () =>
            {
                // 淡出期间发生手动呼出（Show 记录的新 _manualShowAt）：跳过收起，保留手动结果
                if (_manualShowAt > retractStartedAt)
                {
                    return;
                }
                Hide();
            });
        }

        /// <summary>UI 线程调用：刷新钩子线程用的绳窗可见性快照。</summary>
        public void UpdateVisibilitySnapshot()
        {
            _visibilitySnapshot = (
                _ropeForm != null && _visible,
                _ropeForm?.RopeLocation ?? System.Drawing.Point.Empty,
                IsRopeVertical);
        }

        /// <summary>预览窗是否打开（UI 线程维护的标志，钩子线程只读）。</summary>
        public bool IsPreviewOpen { get; private set; }

        /// <summary>鼠标是否悬停在预览窗或对应缩略图上（自动关闭/空格判定用）。</summary>
        public bool IsCursorOverPreview(System.Drawing.Point cursorPos)
        {
            if (_previewForm != null && _previewForm.IsVisible
                && _previewForm.PreviewScreenRectangle.Contains(cursorPos))
            {
                return true;
            }
            return FindItemAt(cursorPos) != null;
        }

        /// <summary>屏幕坐标命中的绳上贴图。</summary>
        public IScrap FindItemAt(System.Drawing.Point screenPos)
        {
            if (_ropeForm == null || !_ropeForm.IsVisible)
            {
                return null;
            }
            var index = Views.ClotheslineWindow.FindItemIndexAtPublic(
                screenPos, _ropeForm.RopeLocation, _ropeForm.IsVertical);
            return index >= 0 && index < _items.Count ? _items[index].Scrap : null;
        }

        public void Clear()
        {
            ClosePreview();
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
            // Auto：宽屏横绳、竖屏竖绳；解析屏来源对齐旧版（绳窗所在屏，初建前主屏）
            var capture = SETUNA.Core.Platform.PlatformServices.IsRegistered
                ? SETUNA.Core.Platform.PlatformServices.Instance.ScreenCapture
                : null;
            var work = _ropeForm != null
                ? capture.GetMonitorFromPoint(new Rectangle(_ropeForm.Position.X, _ropeForm.Position.Y, 0, 0).Location).WorkingArea
                : capture?.GetMonitors()[0].WorkingArea ?? ScreenFallback.WorkArea;
            return work.Height > work.Width;
        }

        /// <summary>默认绳子位置：横=顶部偏左；竖=右边缘垂直居中（对齐旧版公式，估算尺寸 60x200）。</summary>
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
            ClosePreview();
            _edgeTimer?.Stop();
            _previewImage?.Dispose();
            _previewImage = null;
            DisposeThumbnails();
            _items.Clear();
            _ropeForm?.Close();
            _ropeForm = null;
        }
    }
}
