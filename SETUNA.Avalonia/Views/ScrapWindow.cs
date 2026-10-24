using System;
using System.Collections.Generic;
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

        private void ScrapWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 对齐 ScrapBase.OnClosed → ScrapBook.ScrapClose 的回收站流转
            _manager?.ScrapClose(this, new ScrapEventArgs(this));
        }

        // ===== 指针交互 =====

        private void ScrapWindow_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed)
            {
                _isDragging = true;
                _dragStart = new PixelPoint((int)e.GetCurrentPoint(null).Position.X - Position.X, (int)e.GetCurrentPoint(null).Position.Y - Position.Y);
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
            var screen = e.GetCurrentPoint(null).Position.ToPixelPoint();
            Position = new PixelPoint(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
        }

        private void ScrapWindow_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
        }

        private void ShowStyleContextMenu(PointerPressedEventArgs e)
        {
            if (_manager == null)
            {
                return;
            }

            var menu = new ContextMenu();

            var styles = ScrapUiOptionProvider.Styles;
            if (styles != null)
            {
                foreach (var cstyle in styles)
                {
                    var captured = cstyle;
                    var menuItem = new MenuItem { Header = cstyle.StyleName };
                    menuItem.Click += (s, args) =>
                    {
                        var scrap = (IScrap)this;
                        captured.Apply(ref scrap);
                    };
                    menu.Items.Add(menuItem);
                }
            }

            var closeItem = new MenuItem { Header = "关闭" };
            closeItem.Click += (s, args) => Close();
            menu.Items.Add(new Separator());
            menu.Items.Add(closeItem);

            menu.Open(this);
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
        public DrawingSize Size { get => new DrawingSize((int)Width, (int)Height); set { Width = value.Width; Height = value.Height; } }
        public DrawingSize ClientSize { get => Size; set => Size = value; }
        int IScrap.Left { get => Location.X; set => Location = new DrawingPoint(value, Location.Y); }
        int IScrap.Top { get => Location.Y; set => Location = new DrawingPoint(Location.X, value); }
        int IScrap.Width { get => (int)Width; }
        int IScrap.Height { get => (int)Height; }
        public int Scale { get => _scale; set => _scale = value; }

        void IScrap.SetBounds(int x, int y, int width, int height, int specified)
        {
            Position = new PixelPoint(x, y);
            Width = width;
            Height = height;
        }

        bool IScrap.Visible { get => _visibleFlag; set => SetVisibleCore(value); }

        public new void Show()
        {
            SetVisibleCore(true);
            base.Show();
        }

        public new void Hide()
        {
            SetVisibleCore(false);
            base.Hide();
        }

        private void SetVisibleCore(bool value)
        {
            if (_isClotheslineHanging)
            {
                value = false;
            }
            _visibleFlag = value;
        }

        void IScrap.Close() => Close();
        public new void Refresh()
        {
            // 图像重绘：位图变更时由 Image 属性 setter 已更新；此处触发布局
            InvalidateVisual();
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
                    _viewBitmap?.Dispose();
                    using var stream = new System.IO.MemoryStream();
                    // 直接读原始字节：GDI Image → PNG 流 → Avalonia Bitmap
                    ((System.Drawing.Bitmap)value).Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;
                    _viewBitmap = new Bitmap(stream);
                    _imageControl.Source = _viewBitmap;
                    Width = value.Width;
                    Height = value.Height;
                }
                ScrapImageChangedEvent?.Invoke(this, new ScrapEventArgs(this));
            }
        }

        public DrawingImage GetViewImage()
        {
            var bmp = new System.Drawing.Bitmap((int)Width, (int)Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
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
            _isClotheslineHanging = hanging;

            if (hanging)
            {
                SetVisibleCore(false);
                base.Hide();
            }
            else
            {
                _isClotheslineHanging = false;
                if (_visibleFlag)
                {
                    base.Show();
                }
                _isClotheslineHanging = true;
            }
            _isClotheslineHanging = hanging;

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
