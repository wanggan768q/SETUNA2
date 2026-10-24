using System;
using System.IO;
using GdiBitmap = System.Drawing.Bitmap;
using SETUNA.Main.Cache;
using SETUNA.Main.StyleItems;
using SETUNA.Main.Option;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SETUNA.Avalonia.Bridging;
using SETUNA.Avalonia.Views;
using SETUNA.Core.Platform;
using SETUNA.Main;
using SETUNA.Main.Cache;
using SETUNA.Main.StyleItems;
using SETUNA.Main.Style;

namespace SETUNA.Avalonia
{
    /// <summary>
    /// Avalonia 宿主控制器（对应旧 Mainform 的非 UI 职责）：
    /// 配置加载/保存、贴图集合、热键、缓存恢复、托盘菜单。
    /// </summary>
    public sealed class AvaloniaHost : ScrapBookAccessor.IScrapBookHost
    {
        public SetunaOption Option { get; private set; }

        public ScrapBook ScrapBook { get; private set; }

        private Window _ownerWindow;
        private TrayIcon _trayIcon;
        private AvaloniaClotheslineController _clotheslineController;
        private bool _clotheslineModeActive;

        public bool Initialize(Window ownerWindow)
        {
            _ownerWindow = ownerWindow;

            // 配置：与旧版同一文件（exe 目录 SetunaConfig.xml），零迁移成本
            Option = LoadOption();
            ScrapUiOptionProvider.Styles = Option.Styles;
            ScrapUiOptionProvider.WClickStyleID = Option.Scrap.WClickStyleID;

            ScrapBook = new ScrapBook(this);
            ScrapBook.DustBox = new System.Collections.Generic.Queue<IScrap>();
            ScrapBook.DustBoxCapacity = (short)Option.Setuna.DustBoxCapacity;

            ScrapBookAccessor.Register(() => ScrapBook);
            ScrapBookAccessor.RegisterHost(this);

            ScrapUiFactory.CreateScrap = () => new ScrapWindow();
            ScrapUiFactory.StyleListener = () => NullStyleListener.Instance;
            ScrapUiFactory.MenuListener = () => NullMenuListener.Instance;

            HostCommands.RequestCapture = () => Task.Run(() => StartCapture());
            HostCommands.RequestOption = () => { /* 期 6：设置窗口 */ };
            HostCommands.RequestShutdown = () =>
            {
                SaveOption();
                Environment.Exit(0);
            };

            RegisterStylePanels();

            SetupTray();
            SetupHotkeys();

            // 恢复缓存贴图
            var cacheManager = CacheManager.Instance;
            cacheManager.Init(ScrapBook);

            SetupClothesline();

            return true;
        }

        // ===== 晾绳模式（期 5b） =====

        private void SetupClothesline()
        {
            if (!Option.ClotheslineEnabled)
            {
                return;
            }

            _clotheslineController = new AvaloniaClotheslineController(
                () => Option.ClotheslineRopePositionSaved
                    ? Option.ClotheslineRopePosition
                    : AvaloniaClotheslineController.CalculateDefaultRopePosition(false, PlatformServices.Instance.ScreenCapture.GetMonitors()[0].WorkingArea),
                position =>
                {
                    Option.ClotheslineRopePosition = position;
                    Option.ClotheslineRopePositionSaved = true;
                    SaveOption();
                },
                () => Option.ClotheslineRopeOrientation);

            _clotheslineController.ThumbnailDoubleClicked += scrap =>
            {
                scrap.SetClotheslineHanging(false);
            };

            // 贴图增删 → 绳子集合同步
            ScrapBook.ScrapAdded += (s, e) =>
            {
                if (_clotheslineModeActive && e.scrap != null && !e.scrap.IsClotheslineHanging)
                {
                    e.scrap.SetClotheslineHanging(true);
                }
            };
            ScrapBook.ScrapRemoved += (s, e) =>
            {
                _clotheslineController?.RemoveScrap(e.scrap);
            };

            if (Option.ClotheslineEnabled)
            {
                _clotheslineModeActive = true;
                _clotheslineController.Show();
            }
        }

        // ===== 样式编辑面板注册（期 4） =====

        private void RegisterStylePanels()
        {
            StyleUiFactory.EditDialogFactory = item =>
            {
                global::Avalonia.Controls.Window panel = item switch
                {
                    CMoveStyleItem i => new Views.Panels.MoveStyleItemPanel(i),
                    CScaleStyleItem i => new Views.Panels.ScaleStyleItemPanel(i),
                    COpacityStyleItem i => new Views.Panels.OpacityStyleItemPanel(i),
                    CRotateStyleItem i => new Views.Panels.RotateStyleItemPanel(i),
                    CCopyStyleItem i => new Views.Panels.CopyStyleItemPanel(i),
                    CTimerStyleItem i => new Views.Panels.TimerStyleItemPanel(i),
                    CWindowStyleItem i => new Views.Panels.WindowStyleItemPanel(i),
                    CMarginStyleItem i => new Views.Panels.MarginStyleItemPanel(i),
                    CCompactStyleItem i => new Views.Panels.CompactStyleItemPanel(i),
                    CImageBmpStyleItem i => new Views.Panels.ImageBmpStyleItemPanel(i),
                    CImageJpegStyleItem i => new Views.Panels.ImageJpegStyleItemPanel(i),
                    CImagePngStyleItem i => new Views.Panels.ImagePngStyleItemPanel(i),
                    _ => null,
                };
                return panel == null ? null : new PanelDialogHost(panel, _ownerWindow);
            };

            var owner = _ownerWindow;
            StyleUiFactory.TrimDialogFactory = scrap => new TrimDialogHost(new Views.TrimWindow((IScrap)scrap), owner);

            StyleUiFactory.CompactFormFactory = (scrap, item, clickPoint) =>
            {
                // 收缩窗：期 4 简化版——隐藏原图 + 50×50 局部缩略窗
                CreateCompactWindow((IScrap)scrap, (CCompactStyleItem)item);
            };

            StyleUiFactory.PicasaUploadBar = null; // 服务已停，不提供
        }

        private void CreateCompactWindow(IScrap scrap, CCompactStyleItem item)
        {
            // ponytail: 简化收缩窗（原图隐藏 + 居中缩略），完整收缩窗交互期 6 按需补
            var compact = new Views.CompactWindow(scrap, item);
            compact.Show(_ownerWindow);
        }

        /// <summary>面板窗 → IStyleItemEditDialog 适配。</summary>
        private sealed class PanelDialogHost : StyleUiFactory.IStyleItemEditDialog
        {
            private readonly global::Avalonia.Controls.Window _window;
            private readonly global::Avalonia.Controls.Window _owner;

            public PanelDialogHost(global::Avalonia.Controls.Window window, global::Avalonia.Controls.Window owner)
            {
                _window = window;
                _owner = owner;
            }

            public object EditedStyleItem => ((Views.Panels.StyleItemPanelBase)_window).EditedStyleItem;

            public int ShowEditDialog()
            {
                var result = _window.ShowDialog<int?>(_owner).GetAwaiter().GetResult();
                return result ?? 2;
            }

            public void Dispose() { }
        }

        /// <summary>裁剪窗 → ITrimDialog 适配。</summary>
        private sealed class TrimDialogHost : StyleUiFactory.ITrimDialog
        {
            private readonly Views.TrimWindow _window;
            private readonly global::Avalonia.Controls.Window _owner;

            public TrimDialogHost(Views.TrimWindow window, global::Avalonia.Controls.Window owner)
            {
                _window = window;
                _owner = owner;
            }

            public System.Drawing.Rectangle TrimRectangle => _window.TrimRectangle;

            public int TrimLeft => _window.TrimLeft;

            public int TrimTop => _window.TrimTop;

            public int ShowTrimDialog()
            {
                var result = _window.ShowDialog<int?>(_owner).GetAwaiter().GetResult();
                return result ?? 2;
            }

            public void Dispose() { }
        }

        // ===== IScrapBookHost =====

        void ScrapBookAccessor.IScrapBookHost.AddImageList(ScrapSource src)
        {
            // 期 3：截屏流程未接（期 5），仅处理已完成的图像源
            if (src is ScrapSourceImage imageSource && src.IsDone)
            {
                CreateScrapFromImage((GdiBitmap)imageSource.GetImage());
            }
        }

        // ===== 截图（期 3：剪贴板/拖入文件入口；全屏抓取期 5） =====

        public void PasteFromClipboard()
        {
            if (!PlatformServices.IsRegistered)
            {
                return;
            }
            var clipboard = PlatformServices.Instance.Clipboard;
            if (clipboard.ContainsImage)
            {
                using var gdiImage = clipboard.GetImage();
                if (gdiImage != null)
                {
                    CreateScrapFromImage((GdiBitmap)gdiImage.Clone());
                }
            }
        }

        public void OpenImageFile(string path)
        {
            var bitmap = BitmapUtils.FromPath(path);
            if (bitmap != null)
            {
                CreateScrapFromImage(bitmap);
            }
        }

        /// <summary>全屏截图：抓快照 → 遮罩选区 → 贴图（对齐旧 CaptureForm 流程）。</summary>
        public void StartCapture()
        {
            if (!PlatformServices.IsRegistered)
            {
                return;
            }
            try
            {
                _clotheslineController?.SetCapturePaused(true);
                var capture = PlatformServices.Instance.ScreenCapture;
                var monitor = capture.GetMonitors()[0];
                var snapshot = capture.CaptureScreen(monitor.Bounds);

                // 遮罩窗在 UI 线程创建/显示；完成后回调建贴图
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var overlay = new Views.CaptureOverlayWindow(snapshot, monitor.Bounds.Location, monitor);
                    overlay.Closed += (s, e) => _clotheslineController?.SetCapturePaused(false);
                    overlay.CaptureCompleted = (clip, origin) =>
                    {
                        if (clip != null)
                        {
                            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                var scrap = ScrapUiFactory.NewScrap();
                                scrap.Image = clip;
                                scrap.SetBounds(origin.X, origin.Y, clip.Width, clip.Height, 15);
                                ScrapBook.AddScrapThenDo(scrap);
                            });
                        }
                        else
                        {
                            snapshot.Dispose();
                        }
                    };
                    overlay.Show();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StartCapture Exception: {ex.Message}");
            }
        }

        private void CreateScrapFromImage(GdiBitmap image)
        {
            if (image == null)
            {
                return;
            }

            var scrap = ScrapUiFactory.NewScrap();
            scrap.Image = image;
            scrap.SetBounds(100, 100, image.Width, image.Height, 15);
            ScrapBook.AddScrapThenDo(scrap);
        }

        // ===== 热键 =====

        private void SetupHotkeys()
        {
            if (!PlatformServices.IsRegistered)
            {
                return;
            }
            var hotkey = PlatformServices.Instance.Hotkey;
            var captureKey = Option.ScrapHotKeys[0];
            if (captureKey != System.Windows.Forms.Keys.None)
            {
                hotkey.Register(Core.Platform.HotkeyId.Capture, ToSpec(captureKey), () =>
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(StartCapture));
            }
        }

        internal static Core.Platform.HotkeySpec ToSpec(System.Windows.Forms.Keys keys)
        {
            var modifiers = 0;
            if ((keys & System.Windows.Forms.Keys.Shift) == System.Windows.Forms.Keys.Shift)
            {
                modifiers |= 4;
            }
            if ((keys & System.Windows.Forms.Keys.Alt) == System.Windows.Forms.Keys.Alt)
            {
                modifiers |= 1;
            }
            if ((keys & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Control)
            {
                modifiers |= 2;
            }
            return new Core.Platform.HotkeySpec(modifiers, (int)(keys & System.Windows.Forms.Keys.KeyCode));
        }

        // ===== 托盘 =====

        private void SetupTray()
        {
            if (!PlatformServices.IsRegistered)
            {
                return;
            }
            var iconData = TryLoadIconData();
            PlatformServices.Instance.Tray.Initialize(new TrayIconSpec
            {
                IconData = iconData,
                ToolTipText = "SETUNA",
                ClickAction = () => PasteFromClipboard(),
                MenuItems = new[]
                {
                    new TrayMenuItem { Header = "粘贴贴图", ClickAction = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(PasteFromClipboard) },
                    new TrayMenuItem { Header = "全屏贴图", ClickAction = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(StartCapture) },
                    new TrayMenuItem { IsSeparator = true },
                    new TrayMenuItem { Header = "退出", ClickAction = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { SaveOption(); Environment.Exit(0); }) },
                },
            });
        }

        private byte[] TryLoadIconData()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "SETUNA.ico");
                if (File.Exists(path))
                {
                    return File.ReadAllBytes(path);
                }
            }
            catch
            {
            }
            return null;
        }

        // ===== 配置读写（与旧版 Mainform.SaveOption/LoadOption 一致） =====

        public void SaveOption()
        {
            try
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(Option.GetType(), SetunaOption.GetAllType());
                using var stream = new FileStream(SetunaOption.ConfigFile, FileMode.Create);
                serializer.Serialize(stream, Option);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveOption: {ex.Message}");
            }
        }

        private SetunaOption LoadOption()
        {
            try
            {
                if (!File.Exists(SetunaOption.ConfigFile))
                {
                    return SetunaOption.GetDefaultOption();
                }
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(SetunaOption), SetunaOption.GetAllType());
                using var stream = new FileStream(SetunaOption.ConfigFile, FileMode.Open);
                return (SetunaOption)serializer.Deserialize(stream);
            }
            catch
            {
                return SetunaOption.GetDefaultOption();
            }
        }

        // ===== 空监听者（期 3 透明度/激活态事件无宿主消费者） =====

        private sealed class NullStyleListener : IScrapStyleListener
        {
            public static readonly NullStyleListener Instance = new NullStyleListener();
            public void ScrapCreated(object sender, ScrapEventArgs e) { }
            public void ScrapInactived(object sender, ScrapEventArgs e) { }
            public void ScrapActivated(object sender, ScrapEventArgs e) { }
            public void ScrapInactiveMouseOver(object sender, ScrapEventArgs e) { }
            public void ScrapInactiveMouseOut(object sender, ScrapEventArgs e) { }
        }

        private sealed class NullMenuListener : IScrapMenuListener
        {
            public static readonly NullMenuListener Instance = new NullMenuListener();
            public void ScrapMenuOpening(object sender, ScrapMenuArgs e) { }
        }
    }
}
