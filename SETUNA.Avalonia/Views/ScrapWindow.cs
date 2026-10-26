using System;
using System.Collections.Generic;
using System.Linq;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;
using DrawingColor = System.Drawing.Color;
using DrawingPadding = System.Windows.Forms.Padding;
using DrawingImage = System.Drawing.Image;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia;
using Avalonia.Media.Imaging;
using SETUNA.Main;
using SETUNA.Main.Cache;
using SETUNA.Main.Style;
using SETUNA.Main.StyleItems;

namespace SETUNA.Avalonia.Views
{
    /// <summary>
    /// 贴图窗体（对应旧 ScrapBase）：无边框、透明、置顶、拖动、右键样式菜单、
    /// 三态透明度动画（照搬 timOpacity 逻辑）、位置持久化。
    /// 期 3 里程碑实现核心行为；裁剪/绘图等编辑窗在期 4 接入。
    /// </summary>
    public partial class ScrapWindow : Window, IScrap
    {
        // ===== 三态透明度（对齐 ScrapBase 语义） =====

        private double _opacity = 1.0;
        private double _activeOpacity = 1.0;
        private double _inactiveOpacity = 1.0;
        private double _rolloverOpacity = 1.0;
        private bool _isMouseEnter;
        private bool _visibleFlag = true;
        private global::Avalonia.Threading.DispatcherTimer _opacityTimer;
        private bool _blTargetSet;
        private DrawingPoint _ptTarget;

        // ===== 内容 =====

        private Image _imageControl;
        private Bitmap _viewBitmap;
        private DrawingImage _image;
        private int _scale = 100;
        private ScrapBook _manager;
        private CacheItem _cacheItem;
        private DateTime _datetime = DateTime.Now;
        private string _name;
        private int _styleID;
        private DrawingPoint _styleClickPoint;
        private bool _isClotheslineHanging;
        private bool _isDragging;
        private PixelPoint _dragStart;
        private bool _disposed;

        public ScrapWindow()
        {
            InitializeComponent();

            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            Title = "SETUNA Scrap";

            _name = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ssff");

            // 三态透明度动画：15ms 步进（对齐原 timOpacity.Interval=15）
            _opacityTimer = new global::Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(15),
            };
            _opacityTimer.Tick += TimOpacity_Tick;
        }

        private void InitializeComponent()
        {
            _imageControl = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
            };
            Content = _imageControl;

            PointerPressed += ScrapWindow_PointerPressed;
            DoubleTapped += ScrapWindow_DoubleTapped;
            PointerMoved += ScrapWindow_PointerMoved;
            PointerReleased += ScrapWindow_PointerReleased;
            PointerEntered += (s, e) => { _isMouseEnter = true; ApplyOpacityForState(); };
            PointerExited += (s, e) => { _isMouseEnter = false; ApplyOpacityForState(); };
            GotFocus += (s, e) => ApplyOpacityForState();
            LostFocus += (s, e) => ApplyOpacityForState();
            PositionChanged += (s, e) => fireScrapLocationChanged();
            Closing += ScrapWindow_Closing;
        }

        // ===== 生命周期 =====

        /// <summary>关闭预备标志：ScrapClose 流程处置完成后置位，此时才允许窗口真关闭
        /// （对齐旧 ScrapBase.OnFormClosing 的 closePrepare + e.Cancel 语义）。</summary>
        private bool _closePrepare;

        /// <summary>挂绳前的期望可见状态（取下时恢复依据，对齐旧 ClotheslineDesiredVisibility）。</summary>
        private bool _preHangVisible = true;

        private void ScrapWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_closePrepare)
            {
                return;
            }
            // 首次关闭请求：取消窗口关闭，走回收站流转（贴图存活仅隐藏，可恢复）
            e.Cancel = true;
            _manager?.ScrapClose(this, new ScrapEventArgs(this));
        }

        // ===== 指针交互 =====

        private void ScrapWindow_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed)
            {
                _isDragging = true;
                // 屏幕物理坐标 = 窗口物理位置 + 客户区 DIP × 缩放
                var client = e.GetPosition(this);
                var scale = ScaleFactor;
                var screenPt = new PixelPoint(
                    Position.X + (int)Math.Round(client.X * scale),
                    Position.Y + (int)Math.Round(client.Y * scale));
                _dragStart = screenPt - Position;
                e.Pointer.Capture(_imageControl);
            }
            else if (props.IsRightButtonPressed)
            {
                ShowStyleContextMenu(e);
            }
        }

        private void ScrapWindow_PointerMoved(object sender, PointerEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }
            var client = e.GetPosition(this);
            var scale = ScaleFactor;
            var screen = new PixelPoint(
                Position.X + (int)Math.Round(client.X * scale),
                Position.Y + (int)Math.Round(client.Y * scale));
            Position = new PixelPoint(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
        }

        private void ScrapWindow_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
        }

        /// <summary>双击：Ctrl+双击 = 挂回绳子；普通双击 = 执行 WClickStyle
        /// （默认=收缩；对齐旧 ScrapBase_MouseDoubleClick）。</summary>
        private void ScrapWindow_DoubleTapped(object sender, global::Avalonia.Input.TappedEventArgs e)
        {
            if (_manager == null)
            {
                return;
            }
            if (e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control))
            {
                // Ctrl+双击 = 挂回绳子（验收需求）
                SetClotheslineHanging(true);
                return;
            }
            var screen = e.GetPosition(null).ToPixelPoint().ToDrawingPoint();
            _manager.WClickStyle(this, screen);
        }

        private void ShowStyleContextMenu(PointerPressedEventArgs e)
        {
            if (_manager == null)
            {
                return;
            }

            // 菜单结构对齐旧版（Mainform.SetSubMenu/ScrapMenuOpening + UpdateClotheslineMenuItem）：
            // 首项"挂回绳子"（模式开启且未挂起）→ 按 SubMenuStyles 顺序（负数=预置样式/分隔线）
            var menu = new ContextMenu();

            if (ScrapUiOptionProvider.ClotheslineEnabled && !IsClotheslineHanging)
            {
                var hangItem = new MenuItem { Header = "挂回绳子" };
                hangItem.Click += (s, args) => SetClotheslineHanging(true);
                menu.Items.Add(hangItem);
            }

            var subMenuStyles = ScrapUiOptionProvider.SubMenuStyles;
            if (subMenuStyles != null)
            {
                foreach (var id in subMenuStyles)
                {
                    if (id >= 0)
                    {
                        var cstyle = ScrapUiOptionProvider.Styles?.FirstOrDefault(x => x.StyleID == id);
                        if (cstyle != null)
                        {
                            menu.Items.Add(BuildStyleMenuItem(cstyle));
                        }
                    }
                    else
                    {
                        if (id == -4)
                        {
                            // CSeparatorStyle 的预置 ID（旧版 -4）
                            menu.Items.Add(new Separator());
                        }
                        else
                        {
                            var preStyle = SETUNA.Main.Style.CPreStyles.GetPreStyle(id);
                            if (preStyle != null)
                            {
                                menu.Items.Add(BuildStyleMenuItem(preStyle));
                            }
                        }
                    }
                }
            }
            else if (ScrapUiOptionProvider.Styles != null)
            {
                // 配置缺 SubMenuStyles 时回退全量平铺
                foreach (var cstyle in ScrapUiOptionProvider.Styles)
                {
                    menu.Items.Add(BuildStyleMenuItem(cstyle));
                }
            }

            menu.Open(this);
        }

        /// <summary>CSeparatorStyle 的预置 ID（旧版 -3；用于分隔线判断）。</summary>
        private MenuItem BuildStyleMenuItem(SETUNA.Main.Style.CStyle cstyle)
        {
            var menuItem = new MenuItem { Header = cstyle.StyleName };
            menuItem.Click += (s, args) =>
            {
                var scrap = (IScrap)this;
                cstyle.Apply(ref scrap);
            };
            return menuItem;
        }

        // ===== 三态透明度动画（照搬 ScrapBase.timOpacity_Tick） =====

        private void ApplyOpacityForState()
        {
            double target;
            if (IsActive)
            {
                target = _activeOpacity;
            }
            else if (_isMouseEnter)
            {
                target = _rolloverOpacity;
            }
            else
            {
                target = _inactiveOpacity;
            }

            _opacity = target;
            if (Opacity != target && !_opacityTimer.IsEnabled)
            {
                _opacityTimer.Start();
            }
        }

        private void TimOpacity_Tick(object sender, EventArgs e)
        {
            var delta = Opacity - _opacity;
            if (Math.Abs(delta) < 0.1)
            {
                Opacity = _opacity;
                _opacityTimer.Stop();
                return;
            }
            Opacity += delta > 0 ? -0.1 : 0.1;
        }

        private void fireScrapLocationChanged()
        {
            ScrapLocationChangedEvent?.Invoke(this, new ScrapEventArgs(this));
        }

        // ===== IScrap 实现 =====

        public event ScrapEventHandler ScrapCloseEvent;
        public event ScrapEventHandler ScrapCreateEvent;
        public event ScrapEventHandler ScrapActiveEvent;
        public event ScrapEventHandler ScrapInactiveEvent;
        public event ScrapEventHandler ScrapInactiveEnterEvent;
        public event ScrapEventHandler ScrapInactiveOutEvent;
        public event ScrapSubMenuHandler ScrapSubMenuOpening;
        public event ScrapEventHandler ScrapLocationChangedEvent;
        public event ScrapEventHandler ScrapImageChangedEvent;
        public event ScrapEventHandler ScrapStyleAppliedEvent;
        public event ScrapEventHandler ScrapStyleRemovedEvent;
        public event ScrapEventHandler ClotheslineHangingChanged;

        public double ActiveOpacity { get => _activeOpacity; set { _activeOpacity = value; ApplyOpacityForState(); } }
        public double InactiveOpacity { get => _inactiveOpacity; set { _inactiveOpacity = value; ApplyOpacityForState(); } }
        public double RollOverOpacity { get => _rolloverOpacity; set { _rolloverOpacity = value; ApplyOpacityForState(); } }
        public int ActiveMargin { get; set; }
        public int InactiveMargin { get; set; }
        public int RollOverMargin { get; set; }
        public bool SolidFrame { get; set; } = true;
        public object InterpolationMode { get; set; }
        public new bool Topmost { get => base.Topmost; set => base.Topmost = value; }
        bool IScrap.TopMost { get => base.Topmost; set => base.Topmost = value; }
        public new bool ShowInTaskbar { get => base.ShowInTaskbar; set => base.ShowInTaskbar = value; }
        public bool ControlBox { get; set; }
        int IScrap.FormBorderStyle { get; set; } = 0;
        public DrawingColor BackColor { get; set; } = DrawingColor.Gray;
        public DrawingPadding Padding { get; set; }

        public DrawingPoint Location { get => Position.ToDrawingPoint(); set => Position = value.ToPixelPoint(); }
        public DrawingPoint TargetLocation { get => Location; set { _ptTarget = value; _blTargetSet = true; StartOpacityTimerIfNeeded(); } }

        /// <summary>当前屏幕 DPI 缩放（Width/Height 是 DIP，IScrap 几何契约是物理像素，边界换算）。</summary>
        private double ScaleFactor => RenderScaling > 0 ? RenderScaling : 1.0;

        public DrawingSize Size
        {
            get => new DrawingSize((int)Math.Round(Width * ScaleFactor), (int)Math.Round(Height * ScaleFactor));
            set { Width = value.Width / ScaleFactor; Height = value.Height / ScaleFactor; }
        }

        public DrawingSize ClientSize { get => Size; set => Size = value; }
        int IScrap.Left { get => Location.X; set => Location = new DrawingPoint(value, Location.Y); }
        int IScrap.Top { get => Location.Y; set => Location = new DrawingPoint(Location.X, value); }
        int IScrap.Width { get => (int)Math.Round(Width * ScaleFactor); }
        int IScrap.Height { get => (int)Math.Round(Height * ScaleFactor); }
        public int Scale
        {
            get => _scale;
            set
            {
                _scale = Math.Max(-200, Math.Min(200, value));
                ApplyScaleToLayout();
            }
        }

        /// <summary>旧版 Scale 语义：窗口 = 图像 × Scale%（+边距），绘制时拉伸。</summary>
        private void ApplyScaleToLayout()
        {
            if (_image == null)
            {
                return;
            }
            var scale = _scale / 100.0;
            // Scale 为负时保持图像可见（旧版 imgView 尺寸不随负值缩放，仅窗口尺寸走 |scale| 路径时 clamp）
            var factor = scale <= 0 ? 1.0 : scale;
            Width = (_image.Width * factor) / ScaleFactor;
            Height = (_image.Height * factor) / ScaleFactor;
            InvalidateVisual();
        }

        void IScrap.SetBounds(int x, int y, int width, int height, int specified)
        {
            Position = new PixelPoint(x, y);
            Width = width / ScaleFactor;
            Height = height / ScaleFactor;
        }

        bool IScrap.Visible
        {
            get => _visibleFlag;
            set
            {
                if (value)
                {
                    Show();
                }
                else
                {
                    Hide();
                }
            }
        }

        public new void Show()
        {
            SetVisibleCore(true);
            // 挂起门禁：期望可见但挂起中不真显示（取下时按 _visibleFlag 恢复）
            if (!_isClotheslineHanging)
            {
                base.Show();
            }
        }

        public new void Hide()
        {
            SetVisibleCore(false);
            base.Hide();
        }

        /// <summary>期望状态：不做挂起改写（旧版 SetVisibleCore 的挂起强制置 false 
        /// 会导致取下后无法恢复——缓存恢复路径 Show 被抑制后 _visibleFlag 丢失）。</summary>
        private void SetVisibleCore(bool value)
        {
            _visibleFlag = value;
        }

        void IScrap.Close() => Close();
        public new void Refresh()
        {
            // 原地图像修改（反色/旋转 RotateFlip 等）不会走 Image setter：
            // 从 _image 重建显示位图，再触发布局（对齐 ScrapBase.Refresh 重绘语义）
            RebuildViewBitmap();
            InvalidateVisual();
        }

        private void RebuildViewBitmap()
        {
            if (_image == null)
            {
                return;
            }
            try
            {
                using var stream = new System.IO.MemoryStream();
                ((System.Drawing.Bitmap)_image).Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;
                var newBitmap = new Bitmap(stream);
                _viewBitmap?.Dispose();
                _viewBitmap = newBitmap;
                _imageControl.Source = _viewBitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ScrapWindow.RebuildViewBitmap: {ex.Message}");
            }
        }
        bool IScrap.Focus() => base.Focus();

        void IScrap.SuspendLayout() { }
        void IScrap.ResumeLayout() { }

        public DrawingImage Image
        {
            get => _image;
            set
            {
                _image = value;
                if (value != null)
                {
                    RebuildViewBitmap();
                    // 对齐旧版 Image setter：Scale=Scale 触发窗口尺寸按当前缩放重算（100% 时 1:1）
                    ApplyScaleToLayout();
                }
                ScrapImageChangedEvent?.Invoke(this, new ScrapEventArgs(this));
            }
        }

        public DrawingImage GetViewImage()
        {
            var bmp = new System.Drawing.Bitmap(IScrapWidth(), IScrapHeight(), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(DrawingColor.White);
                if (_image != null)
                {
                    g.DrawImage(_image, 0, 0, _image.Width, _image.Height);
                }
            }
            return bmp;
        }

        /// <summary>窗口物理像素宽/高（DIP × 缩放）。</summary>
        private int IScrapWidth() => (int)Math.Round(Width * ScaleFactor);

        private int IScrapHeight() => (int)Math.Round(Height * ScaleFactor);

        public string Name { get => _name; set => _name = value; }
        public DateTime DateTime { get => _datetime; set => _datetime = value; }

        public ScrapBook Manager
        {
            get => _manager;
            set
            {
                _manager = value;
                if (ScrapCloseEvent == null && value != null)
                {
                    ScrapCloseEvent += (s, e) => { };
                }
            }
        }

        public CacheItem CacheItem { get => _cacheItem; set => _cacheItem = value; }

        /// <summary>关联的收缩窗（对齐旧 ScrapBase.StyleForm）：挂绳一并隐藏，取下一并恢复。</summary>
        public global::Avalonia.Controls.Window StyleForm { set; get; }
        public int StyleID => _styleID;
        public DrawingPoint StyleClickPoint => _styleClickPoint;

        public void ApplyStyles(int styleID, CStyleItem[] styleItems, DrawingPoint clickpoint)
        {
            _styleID = styleID;
            _styleClickPoint = clickpoint;

            // 顺序执行样式命令（对齐 ScrapBase.StyleApplyTimer 的逐步 Apply）
            var wait = 0;
            foreach (var item in styleItems)
            {
                var scrap = (IScrap)this;
                item.Apply(ref scrap, out wait, clickpoint);
            }
            ScrapStyleAppliedEvent?.Invoke(this, new ScrapEventArgs(this));
        }

        public void ApplyStylesFromCache(CStyle style, DrawingPoint clickpoint, Action applyFinished)
        {
            try
            {
                ApplyStyles(style.StyleID, style.Items, clickpoint);
            }
            finally
            {
                applyFinished?.Invoke();
            }
        }

        public void RemoveStyle(Type styleItemType)
        {
            // 期 4：样式移除（收缩样式恢复）。当前无收缩窗实现。
            ScrapStyleRemovedEvent?.Invoke(this, new ScrapEventArgs(this));
        }

        public void ScrapClose()
        {
            // 回收站淘汰/清空路径：贴图真关闭（对齐旧 closePrepare 置位后 Close）
            _closePrepare = true;
            Close();
        }

        public bool GetVisibleFlag() => _visibleFlag;

        public bool IsClotheslineHanging => _isClotheslineHanging;

        public void SetClotheslineHanging(bool hanging)
        {
            if (_isClotheslineHanging == hanging)
            {
                return;
            }

            if (hanging)
            {
                // 记录挂起前的期望可见状态（取下时按此恢复）
                _preHangVisible = _visibleFlag;
                _isClotheslineHanging = true;
                base.Hide();
                if (StyleForm is CompactWindow compact && compact.IsVisible)
                {
                    compact.Hide();
                }
            }
            else
            {
                _isClotheslineHanging = false;
                if (_preHangVisible || _visibleFlag)
                {
                    base.Show();
                }
                if (StyleForm is CompactWindow compactRestore && compactRestore.IsVisible)
                {
                    compactRestore.Show();
                }
            }

            ClotheslineHangingChanged?.Invoke(this, new ScrapEventArgs(this));

            if (_cacheItem != null)
            {
                _cacheItem.ClotheslineHanging = hanging;
                _cacheItem.SaveInfo();
            }
        }

        void IScrap.addScrapStyleEvent(IScrapStyleListener listener)
        {
            ScrapCreateEvent += (s, e) => listener.ScrapCreated(s, e);
            ScrapInactiveEvent += (s, e) => listener.ScrapInactived(s, e);
            ScrapActiveEvent += (s, e) => listener.ScrapActivated(s, e);
            ScrapInactiveEnterEvent += (s, e) => listener.ScrapInactiveMouseOver(s, e);
            ScrapInactiveOutEvent += (s, e) => listener.ScrapInactiveMouseOut(s, e);
        }

        void IScrap.addScrapMenuEvent(IScrapMenuListener listener)
        {
            ScrapSubMenuOpening += (s, e) => listener.ScrapMenuOpening(s, e);
        }

        void IScrap.addScrapLocationChangedEvent(IScrapLocationChangedListener listener)
        {
            ScrapLocationChangedEvent += (s, e) => listener.ScrapLocationChanged(s, e);
        }

        void IScrap.addScrapImageChangedEvent(IScrapImageChangedListener listener)
        {
            ScrapImageChangedEvent += (s, e) => listener.ScrapImageChanged(s, e);
        }

        void IScrap.addScrapStyleAppliedEvent(IScrapStyleAppliedListener listener)
        {
            ScrapStyleAppliedEvent += (s, e) => listener.ScrapStyleApplied(s, e);
        }

        void IScrap.addScrapStyleRemovedEvent(IScrapStyleRemovedListener listener)
        {
            ScrapStyleRemovedEvent += (s, e) => listener.ScrapStyleRemoved(s, e);
        }

        public void OnScrapCreated()
        {
            ScrapCreateEvent?.Invoke(this, new ScrapEventArgs(this));
        }

        private void StartOpacityTimerIfNeeded()
        {
            if (!_opacityTimer.IsEnabled)
            {
                _opacityTimer.Start();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _opacityTimer?.Stop();
            _viewBitmap?.Dispose();
            _image?.Dispose();
            base.OnClosed(e);
        }

        void IDisposable.Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Close();
        }
    }
}
