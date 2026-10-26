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

        internal static void Log(string message)
        {
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "setuna-avalonia-debug.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }

        public bool Initialize(Window ownerWindow)
        {
            Instance = this;
            Log($"Initialize start, base={AppContext.BaseDirectory}");
            _ownerWindow = ownerWindow;

            // 配置：与旧版同一文件（exe 目录 SetunaConfig.xml），零迁移成本
            Option = LoadOption();
            ScrapUiOptionProvider.Option = Option;
            ScrapUiOptionProvider.Styles = Option.Styles;
            ScrapUiOptionProvider.WClickStyleID = Option.Scrap.WClickStyleID;
            ScrapUiOptionProvider.SubMenuStyles = Option.Scrap.SubMenuStyles;
            ScrapUiOptionProvider.ClotheslineEnabled = Option.ClotheslineEnabled;

            ScrapBook = new ScrapBook(this);
            ScrapBook.DustBox = new System.Collections.Generic.Queue<IScrap>();
            ScrapBook.DustBoxCapacity = (short)Option.Setuna.DustBoxCapacity;

            ScrapBookAccessor.Register(() => ScrapBook);
            ScrapBookAccessor.RegisterHost(this);

            ScrapUiFactory.CreateScrap = () => new ScrapWindow();
            ScrapUiFactory.StyleListener = () => OptionStyleListener.Instance;
            ScrapUiFactory.MenuListener = () => NullMenuListener.Instance;

            HostCommands.RequestCapture = () => Task.Run(() => StartCapture());
            HostCommands.RequestOption = () =>
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var editor = new Views.OptionWindow(Option, () =>
                    {
                        SaveOption();
                        ScrapUiOptionProvider.Styles = Option.Styles;
                        ScrapUiOptionProvider.SubMenuStyles = Option.Scrap.SubMenuStyles;
                        ScrapUiOptionProvider.ClotheslineEnabled = Option.ClotheslineEnabled;
                        ApplyHotkeysFromOption();
                        ApplyClotheslineOption();
                    });
                    // MainWindow 已隐藏不可作 owner（review 教训：隐藏 owner 抛 InvalidOperation）
                    editor.Show();
                });
            };
            HostCommands.RequestShutdown = () =>
            {
                // 优雅关闭：ShutdownRequested 里统一 SaveOption（Environment.Exit 会跳过）
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    (global::Avalonia.Application.Current.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
                        ?.MainWindow?.Close());
            };

            RegisterStylePanels();

            // UI 线程未处理异常记录（右键"选项"等路径崩溃定位）
            global::Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (s, e) =>
                Log($"UIThread UNHANDLED: {e.Exception}");

            SetupTray();
            SetupHotkeys();

            // 晾绳订阅必须先于缓存恢复（缓存挂起状态依赖事件桥）
            SetupClothesline();

            // 恢复缓存贴图
            var cacheManager = CacheManager.Instance;
            Log($"config resolved, clothesline={Option.ClotheslineEnabled}, styles={Option.Styles.Count}");
            cacheManager.Init(ScrapBook);
            Log($"cache init done, scraps={ScrapBook.ScrapCount}");

            return true;
        }

        /// <summary>设置保存后重注册热键（对齐旧 Mainform 选项变更重注册）。</summary>
        private void ApplyHotkeysFromOption()
        {
            if (!PlatformServices.IsRegistered)
            {
                return;
            }
            var hotkey = PlatformServices.Instance.Hotkey;
            hotkey.UnregisterAll();
            SetupHotkeys();
        }

        // ===== 晾绳模式（期 5b） =====

        private void SetupClothesline()
        {
            // 事件订阅常驻（不依赖模式开关，对齐旧 Mainform 常驻订阅语义）
            EnsureClotheslineController();
            ApplyClotheslineOption();
        }

        private void EnsureClotheslineController()
        {
            if (_clotheslineController != null)
            {
                return;
            }

            _clotheslineController = new AvaloniaClotheslineController(
                () =>
                {
                    var workArea = PlatformServices.Instance.ScreenCapture.GetMonitors()[0].WorkingArea;
                    var vertical = _clotheslineController != null
                        ? _clotheslineController.ResolveVertical(Option.ClotheslineRopeOrientation)
                        : Option.ClotheslineRopeOrientation == ClotheslineRopeOrientation.Vertical
                            || (Option.ClotheslineRopeOrientation == ClotheslineRopeOrientation.Auto
                                && workArea.Height > workArea.Width);
                    return Option.ClotheslineRopePositionSaved
                        ? Option.ClotheslineRopePosition
                        : AvaloniaClotheslineController.CalculateDefaultRopePosition(vertical, workArea);
                },
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
            _clotheslineController.PreviewMode = Option.ClotheslinePreviewLocation;

            // 空格预览：低级键盘过滤（命中缩略图时消费空格切换预览）
            var hotkey = PlatformServices.IsRegistered ? PlatformServices.Instance.Hotkey : null;
            hotkey?.SetKeyFilter(vk =>
            {
                if (vk != 0x20 /*VK_SPACE*/ || !_clotheslineModeActive)
                {
                    return false;
                }
                var modifier = PlatformServices.Instance.Hotkey.GetModifierState();
                if (modifier != 0)
                {
                    return false; // 修饰键组合不接管
                }
                var hit = _clotheslineController.FindItemAtCursor() != null
                    || (_clotheslineController.IsPreviewOpen && IsCursorOverPreview());
                var consume = _clotheslineController.OnSpaceKey(isDown: true, hit);
                if (consume)
                {
                    // keyup 也需消费（状态机轮次配对）：注册一次性 keyup 过滤由状态机内部处理
                    // SetKeyFilter 的 filter 会被持续调用，keyup 时 OnSpaceKey(false) 走轮次收尾
                }
                return consume;
            });

            // 贴图增删 → 绳子集合同步（对齐旧 Mainform.Scrap_HangingChanged 桥）
            ScrapBook.ScrapAdded += (s, e) =>
            {
                if (e.scrap != null)
                {
                    // 挂起状态变化 → 上绳/取下（旧 Scrap_HangingChanged）
                    e.scrap.ClotheslineHangingChanged += Scrap_HangingChanged;
                    // 图像/样式变化 → 缩略图刷新（旧 UpdateScrap 订阅）
                    e.scrap.addScrapImageChangedEvent(ScrapUpdateListener.Instance);
                    e.scrap.addScrapStyleAppliedEvent(ScrapUpdateListener.Instance);

                    if (_clotheslineModeActive)
                    {
                        if (!e.scrap.IsClotheslineHanging)
                        {
                            e.scrap.SetClotheslineHanging(true);
                        }
                        else
                        {
                            // 已挂起（缓存恢复路径预设）：事件已过，直接补上绳集合
                            _clotheslineController?.AttachScrap(e.scrap);
                        }
                    }
                }
                RefreshTrayMenu();
            };
            ScrapBook.ScrapRemoved += (s, e) =>
            {
                if (e.scrap != null)
                {
                    e.scrap.ClotheslineHangingChanged -= Scrap_HangingChanged;
                    _clotheslineController?.RemoveScrap(e.scrap);
                }
                RefreshTrayMenu();
            };
        }

        /// <summary>鼠标是否在预览窗上（空格切换判定）。</summary>
        private bool IsCursorOverPreview()
        {
            var capture = PlatformServices.Instance.ScreenCapture;
            var pos = capture.GetCursorPosition();
            return _clotheslineController?.IsCursorOverPreview(pos) ?? false;
        }

        /// <summary>挂起状态变化桥（对齐旧 Mainform.Scrap_HangingChanged）：挂→上绳、取下→摘绳。</summary>
        private void Scrap_HangingChanged(object sender, ScrapEventArgs e)
        {
            if (e.scrap == null)
            {
                return;
            }
            if (e.scrap.IsClotheslineHanging)
            {
                _clotheslineController?.AttachScrap(e.scrap);
            }
            else
            {
                _clotheslineController?.DetachScrap(e.scrap);
            }
        }

        /// <summary>图像/样式变化监听桥：刷新绳上缩略图（对齐旧 Mainform.Scrap_ImageChanged）。</summary>
        private sealed class ScrapUpdateListener : IScrapImageChangedListener, IScrapStyleAppliedListener
        {
            public static readonly ScrapUpdateListener Instance = new ScrapUpdateListener();

            void IScrapImageChangedListener.ScrapImageChanged(object sender, ScrapEventArgs e)
            {
                if (e.scrap != null)
                {
                    AvaloniaHost.Instance?.UpdateClotheslineScrap(e.scrap);
                }
            }

            void IScrapStyleAppliedListener.ScrapStyleApplied(object sender, ScrapEventArgs e)
            {
                if (e.scrap != null)
                {
                    AvaloniaHost.Instance?.UpdateClotheslineScrap(e.scrap);
                }
            }
        }

        internal static AvaloniaHost Instance { get; private set; }

        internal void UpdateClotheslineScrap(IScrap scrap)
        {
            _clotheslineController?.UpdateScrap(scrap);
        }

        /// <summary>设置保存后重应用晾绳模式（对齐旧 OptionApply → ApplyClotheslineOption）。</summary>
        private void ApplyClotheslineOption()
        {
            EnsureClotheslineController();

            if (Option.ClotheslineEnabled)
            {
                // 开启（或启动即开启）：全量同步——订阅缺失的补订阅、未挂的挂绳、
                // 已挂但未进绳集合的补 Attach（幂等，AttachScrap 内有 ReferenceEquals 去重）
                _clotheslineModeActive = true;
                foreach (var scrap in ScrapBook)
                {
                    scrap.ClotheslineHangingChanged -= Scrap_HangingChanged;
                    scrap.ClotheslineHangingChanged += Scrap_HangingChanged;
                    if (!scrap.IsClotheslineHanging)
                    {
                        scrap.SetClotheslineHanging(true);
                    }
                    else
                    {
                        _clotheslineController.AttachScrap(scrap);
                    }
                }
                _clotheslineController.Show();
            }
            else if (_clotheslineModeActive)
            {
                // 关闭：统一取下恢复展示
                _clotheslineModeActive = false;
                foreach (var scrap in ScrapBook)
                {
                    if (scrap.IsClotheslineHanging)
                    {
                        scrap.SetClotheslineHanging(false);
                    }
                }
                _clotheslineController.Hide();
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
                return panel == null
                    ? System.Threading.Tasks.Task.FromResult<SETUNA.Main.Style.StyleUiFactory.IStyleItemEditDialog>(null)
                    : System.Threading.Tasks.Task.FromResult<SETUNA.Main.Style.StyleUiFactory.IStyleItemEditDialog>(new PanelDialogHost(panel, _ownerWindow));
            };

            var owner = _ownerWindow;
            StyleUiFactory.TrimDialogFactory = scrap =>
                System.Threading.Tasks.Task.FromResult<SETUNA.Main.Style.StyleUiFactory.ITrimDialog>(
                    new TrimDialogHost(new Views.TrimWindow((IScrap)scrap), owner));

            StyleUiFactory.CompactFormFactory = (scrap, item, clickPoint) =>
            {
                // 收缩窗：期 4 简化版——隐藏原图 + 50×50 局部缩略窗
                CreateCompactWindow((IScrap)scrap, (CCompactStyleItem)item);
            };

            // 消息框：buttons 对齐 WinForms MessageBoxButtons（0=OK, 4=YesNo, 3=YesNoCancel）
            // 异步：调用方（Core Apply 循环）await 完成后继续，不在 UI 线程阻塞
            StyleUiFactory.ShowMessageBox = async (text, caption, buttons) =>
            {
                var dialog = new Views.MessageBoxWindow(caption ?? "SETUNA", text ?? string.Empty, buttons);
                var result = await dialog.ShowDialog<int?>(_ownerWindow);
                return result ?? 2;
            };

            // 另存为对话框（异步）：返回选定路径，取消 null（对齐旧 SaveFileDialog.ShowDialog 语义）
            StyleUiFactory.ShowSaveFileDialog = async (folder, fileName, filter) =>
            {
                var storage = global::Avalonia.Controls.TopLevel.GetTopLevel(_ownerWindow)?.StorageProvider;
                if (storage == null)
                {
                    return null;
                }
                var options = new global::Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "保存",
                    SuggestedFileName = fileName,
                    DefaultExtension = System.IO.Path.GetExtension(fileName ?? string.Empty).TrimStart('.'),
                };
                var file = await storage.SaveFilePickerAsync(options);
                return file?.Path.LocalPath;
            };

            StyleUiFactory.PicasaUploadBar = null; // 服务已停，不提供
        }

        private void CreateCompactWindow(IScrap scrap, CCompactStyleItem item)
        {
            Log($"compact window creating, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
            try
            {
                // 挂绳中的贴图不弹收缩窗（对齐旧版：挂起压制收缩窗创建）
                if (scrap.IsClotheslineHanging)
                {
                    Log("compact window suppressed (hanging)");
                    return;
                }
                // 对齐旧 CompactScrap：原图隐藏，缩略窗显示
                scrap.Visible = false;
                var compact = new Views.CompactWindow(scrap, item);
                // 关联 StyleForm：挂绳/取下时跟随隐藏/恢复（对齐旧 ScrapBase.StyleForm）
                if (scrap is Views.ScrapWindow sw)
                {
                    sw.StyleForm = compact;
                    compact.Closed += (s, e) => { if (sw.StyleForm == compact) sw.StyleForm = null; };
                }
                compact.Show(); // 收缩窗独立于主窗生命周期（MainWindow 已隐藏，不可作 owner）
                Log("compact window shown");
            }
            catch (Exception ex)
            {
                Log($"compact window FAILED: {ex.Message}");
            }
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

            public async System.Threading.Tasks.Task<int> ShowEditDialog()
            {
                // owner 可见走 ShowDialog；隐藏时退化为独立显示（关闭回调取结果）
                if (_owner.IsVisible)
                {
                    var result = await _window.ShowDialog<int?>(_owner);
                    return result ?? 2;
                }
                var tcs = new TaskCompletionSource<int>();
                _window.Closed += (s, e) =>
                {
                    tcs.TrySetResult(((Views.Panels.StyleItemPanelBase)_window).EditedStyleItem != null ? 1 : 2);
                };
                _window.Show();
                return await tcs.Task;
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

            public async System.Threading.Tasks.Task<int> ShowTrimDialog()
            {
                // TrimWindow：从 TrimRectangle 判定结果；owner 可见走 ShowDialog，否则独立显示
                var tcs = new TaskCompletionSource<int>();
                _window.Closed += (s, e) => tcs.TrySetResult(_window.TrimRectangle.Width > 0 ? 1 : 2);
                if (_owner.IsVisible)
                {
                    await _window.ShowDialog<int?>(_owner);
                    return tcs.Task.GetAwaiter().GetResult();
                }
                _window.Show();
                return await tcs.Task;
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
                Log($"capture done, bounds={monitor.Bounds}, brightness={SETUNA.Platform.Windows.WindowsScreenCaptureService.ProbeBrightness(snapshot)}");
                // 截图含光标（对齐旧 CaptureForm.CopyFromScreen 的 CursorEnabled 语义）
                if (Option.Setuna.CursorEnabled)
                {
                    var cursorPos = capture.GetCursorPosition();
                    capture.DrawCursorToImage(snapshot, new System.Drawing.Point(
                        cursorPos.X - monitor.Bounds.X, cursorPos.Y - monitor.Bounds.Y));
                }

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
                var spec = ToSpec(captureKey);
                var registered = hotkey.Register(Core.Platform.HotkeyId.Capture, spec, () =>
                {
                    Log("hotkey fired");
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(StartCapture);
                });
                Log($"hotkey register={(registered ? "ok" : "FAILED")}, key={captureKey}");
                if (!registered)
                {
                    // 对齐旧版（注册失败仅忽略）；托盘提示用户热键被占用
                    PlatformServices.Instance.Tray.ShowBalloon("SETUNA",
                        $"截图热键 {captureKey} 注册失败（可能被其他程序占用）");
                }
            }

            // Function1：全部显示/隐藏切换（对齐旧 Mainform.SetAllScrapsActive）
            if (Option.ScrapHotKeys.Length > (int)SETUNA.Main.HotKeyID.Function1)
            {
                var funcKey = Option.ScrapHotKeys[(int)SETUNA.Main.HotKeyID.Function1];
                if (funcKey != System.Windows.Forms.Keys.None)
                {
                    hotkey.Register(Core.Platform.HotkeyId.Function1, ToSpec(funcKey), () =>
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(ToggleAllScraps));
                }
            }
        }

        /// <summary>全部贴图显示/隐藏切换（对齐旧 Mainform.SetAllScrapsActive 语义）。</summary>
        private bool _allScrapActive;

        private void ToggleAllScraps()
        {
            _allScrapActive = !_allScrapActive;
            foreach (var scrap in ScrapBook)
            {
                scrap.Visible = _allScrapActive;
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
            PlatformServices.Instance.Tray.Initialize(BuildTraySpec(iconData));

            // 名单/回收站变化时重建托盘菜单（对齐旧版每次展开前刷新的动态行为）
            ScrapBook.ScrapAdded += (s, e) => RefreshTrayMenu();
            ScrapBook.ScrapRemoved += (s, e) => RefreshTrayMenu();
        }

        private TrayIconSpec BuildTraySpec(byte[] iconData)
        {
            return new TrayIconSpec
            {
                IconData = iconData,
                ToolTipText = "SETUNA",
                ClickAction = () => PasteFromClipboard(),
                MenuItems = new[]
                {
                    new TrayMenuItem { Header = "参考图名单", Items = BuildScrapListItems() },
                    new TrayMenuItem { Header = "回收站", Items = BuildDustBoxItems() },
                    new TrayMenuItem { Header = "清空回收站", ClickAction = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => ScrapBook.EraseDustBox()) },
                    new TrayMenuItem { IsSeparator = true },
                    new TrayMenuItem { Header = "关闭所有截图", ClickAction = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => ScrapBook.CloseAllScrap()) },
                    new TrayMenuItem { IsSeparator = true },
                    new TrayMenuItem { Header = "粘贴贴图", ClickAction = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(PasteFromClipboard) },
                    new TrayMenuItem { Header = "全屏贴图", ClickAction = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(StartCapture) },
                    new TrayMenuItem { IsSeparator = true },
                    new TrayMenuItem { Header = "设置", ClickAction = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => HostCommands.RequestOption?.Invoke()) },
                    new TrayMenuItem { Header = "退出", ClickAction = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => HostCommands.RequestShutdown?.Invoke()) },
                },
            };
        }

        private void RefreshTrayMenu()
        {
            try
            {
                PlatformServices.Instance.Tray.RebuildMenu(BuildTraySpec(TryLoadIconData()));
            }
            catch (Exception ex)
            {
                Log($"RefreshTrayMenu: {ex.Message}");
            }
        }

        /// <summary>参考图名单子项（对齐旧 ToolStripScrapList：显示中贴图 → 激活置顶）。</summary>
        private TrayMenuItem[] BuildScrapListItems()
        {
            var items = new List<TrayMenuItem>();
            foreach (var scrap in ScrapBook)
            {
                var captured = scrap;
                items.Add(new TrayMenuItem
                {
                    Header = captured.Name ?? "(untitled)",
                    ClickAction = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => captured.Visible = true),
                });
            }
            if (items.Count == 0)
            {
                items.Add(new TrayMenuItem { Header = "（无贴图）", IsEnabled = false });
            }
            return items.ToArray();
        }

        /// <summary>回收站子项（对齐旧 ToolStripDustboxList：点击恢复）。</summary>
        private TrayMenuItem[] BuildDustBoxItems()
        {
            var items = new List<TrayMenuItem>();
            foreach (var scrap in ScrapBook.DustBoxArray)
            {
                if (scrap is IScrap s)
                {
                    var captured = s;
                    items.Add(new TrayMenuItem
                    {
                        Header = captured.Name ?? "(untitled)",
                        ClickAction = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => RestoreScrap(captured)),
                    });
                }
            }
            if (items.Count == 0)
            {
                items.Add(new TrayMenuItem { Header = "（空）", IsEnabled = false });
            }
            return items.ToArray();
        }

        /// <summary>恢复回收站贴图（对齐旧 Mainform.RestoreScrap：出队→重建→其余按原序回填）。</summary>
        private void RestoreScrap(IScrap scrap)
        {
            ScrapBook.RestoreFromDustBox(scrap);
        }

        private byte[] TryLoadIconData()
        {
            try
            {
                var candidates = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "SETUNA.ico"),
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\SETUNA\bin\Debug\net9.0-windows\SETUNA.ico")),
                };
                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        return File.ReadAllBytes(path);
                    }
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
            var path = ResolveConfigPath();
            try
            {
                if (path == null)
                {
                    return SetunaOption.GetDefaultOption();
                }
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(SetunaOption), SetunaOption.GetAllType());
                using var stream = new FileStream(path, FileMode.Open);
                return (SetunaOption)serializer.Deserialize(stream);
            }
            catch
            {
                return SetunaOption.GetDefaultOption();
            }
        }

        /// <summary>
        /// 配置查找：本 exe 目录 → 旧 WinForms 版 bin 目录（两版共存期的兼容回退）。
        /// </summary>
        private static string ResolveConfigPath()
        {
            if (File.Exists(SetunaOption.ConfigFile))
            {
                return SetunaOption.ConfigFile;
            }
            var legacy = System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\SETUNA\bin\Debug\net9.0-windows");
            try
            {
                var full = System.IO.Path.GetFullPath(legacy);
                var candidate = System.IO.Path.Combine(full, "SetunaConfig.xml");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
            }
            return null;
        }

        // ===== 空监听者（期 3 透明度/激活态事件无宿主消费者） =====

        /// <summary>样式事件监听（对齐旧 Mainform 的 IScrapStyleListener 实现）。</summary>
        private sealed class OptionStyleListener : IScrapStyleListener
        {
            public static readonly OptionStyleListener Instance = new OptionStyleListener();

            public void ScrapCreated(object sender, ScrapEventArgs e)
            {
                var option = ScrapUiOptionProvider.Option;
                var cstyle = option?.FindStyle(option.Scrap.CreateStyleID);
                cstyle?.Apply(ref e.scrap);
            }

            public void ScrapInactived(object sender, ScrapEventArgs e)
            {
                ApplyInactiveOpacity(e.scrap);
            }

            public void ScrapActivated(object sender, ScrapEventArgs e)
            {
                // 旧版激活时只读配置不做透明度变化
            }

            public void ScrapInactiveMouseOver(object sender, ScrapEventArgs e)
            {
                var option = ScrapUiOptionProvider.Option;
                if (option == null)
                {
                    return;
                }
                e.scrap.RollOverOpacity = option.Scrap.MouseOverAlphaChange
                    ? 1.0 - option.Scrap.MouseOverAlphaValue / 100.0
                    : e.scrap.ActiveOpacity;
            }

            public void ScrapInactiveMouseOut(object sender, ScrapEventArgs e)
            {
                ApplyInactiveOpacity(e.scrap);
            }

            private static void ApplyInactiveOpacity(IScrap scrap)
            {
                var option = ScrapUiOptionProvider.Option;
                if (option == null)
                {
                    return;
                }
                scrap.InactiveOpacity = option.Scrap.InactiveAlphaChange
                    ? 1.0 - option.Scrap.InactiveAlphaValue / 100.0
                    : scrap.ActiveOpacity;
            }
        }

        private sealed class NullMenuListener : IScrapMenuListener
        {
            public static readonly NullMenuListener Instance = new NullMenuListener();
            public void ScrapMenuOpening(object sender, ScrapMenuArgs e) { }
        }
    }
}
