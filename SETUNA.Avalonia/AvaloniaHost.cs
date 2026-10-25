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
            Log($"Initialize start, base={AppContext.BaseDirectory}");
            _ownerWindow = ownerWindow;

            // 配置：与旧版同一文件（exe 目录 SetunaConfig.xml），零迁移成本
            Option = LoadOption();
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
            ScrapUiFactory.StyleListener = () => NullStyleListener.Instance;
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
                    });
                    editor.Show(_ownerWindow);
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

            SetupTray();
            SetupHotkeys();

            // 恢复缓存贴图
            var cacheManager = CacheManager.Instance;
            Log($"config resolved, clothesline={Option.ClotheslineEnabled}, styles={Option.Styles.Count}");
            cacheManager.Init(ScrapBook);
            Log($"cache init done, scraps={ScrapBook.ScrapCount}");

            SetupClothesline();

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

            // 消息框：buttons 对齐 WinForms MessageBoxButtons（0=OK, 4=YesNo, 0x103=YesNoCancel+默认钮2）
            StyleUiFactory.ShowMessageBox = (text, caption, buttons) =>
            {
                var tcs = new TaskCompletionSource<int>();
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var dialog = new Views.MessageBoxWindow(caption ?? "SETUNA", text ?? string.Empty, buttons);
                    var result = dialog.ShowDialog<int?>(_ownerWindow).GetAwaiter().GetResult();
                    tcs.TrySetResult(result ?? 2);
                });
                return tcs.Task.GetAwaiter().GetResult();
            };

            // 另存为对话框：返回选定路径，取消 null（对齐旧 SaveFileDialog.ShowDialog 语义）
            StyleUiFactory.ShowSaveFileDialog = (folder, fileName, filter) =>
            {
                var tcs = new TaskCompletionSource<string>();
                global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    var storage = global::Avalonia.Controls.TopLevel.GetTopLevel(_ownerWindow)?.StorageProvider;
                    if (storage == null)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }
                    // filter "描述|*.ext" → StorageProvider FilePickerFileType
                    var options = new global::Avalonia.Platform.Storage.FilePickerSaveOptions
                    {
                        Title = "保存",
                        SuggestedFileName = fileName,
                        DefaultExtension = System.IO.Path.GetExtension(fileName ?? string.Empty).TrimStart('.'),
                    };
                    var file = await storage.SaveFilePickerAsync(options);
                    tcs.TrySetResult(file?.Path.LocalPath);
                });
                return tcs.Task.GetAwaiter().GetResult();
            };

            StyleUiFactory.PicasaUploadBar = null; // 服务已停，不提供
        }

        private void CreateCompactWindow(IScrap scrap, CCompactStyleItem item)
        {
            // ponytail: 简化收缩窗（原图隐藏 + 居中缩略），完整收缩窗交互期 6 按需补
            var compact = new Views.CompactWindow(scrap, item);
            compact.Show(); // 收缩窗独立于主窗生命周期（MainWindow 已隐藏，不可作 owner）
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
                // owner 隐藏时退化为独立模态（ShowDialog 需要 visible owner）
                var result = _owner.IsVisible
                    ? _window.ShowDialog<int?>(_owner).GetAwaiter().GetResult()
                    : ShowAsModalStandalone();
                return result ?? 2;
            }

            private int ShowAsModalStandalone()
            {
                var tcs = new TaskCompletionSource<int>();
                _window.Closed += (s, e) =>
                {
                    // StyleItemPanelBase.Close(1/2) 把结果放 Tag；这里读 Dialog 结果约定：
                    // EditedStyleItem 非 null = OK(1)，否则取消
                    tcs.TrySetResult(((Views.Panels.StyleItemPanelBase)_window).EditedStyleItem != null ? 1 : 2);
                };
                _window.Show();
                return tcs.Task.GetAwaiter().GetResult();
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
                // TrimWindow.Close(1/2) 无 Tag 通道 → 从 TrimRectangle 判定；独立显示时结果经 Closed 事件
                var tcs = new TaskCompletionSource<int>();
                _window.Closed += (s, e) => tcs.TrySetResult(_window.TrimRectangle.Width > 0 ? 1 : 2);
                if (_owner.IsVisible)
                {
                    var shown = _window.ShowDialog<int?>(_owner);
                    // ShowDialog 路径下 Closed 已触发 tcs；等待同一结果
                    return tcs.Task.GetAwaiter().GetResult();
                }
                _window.Show();
                return tcs.Task.GetAwaiter().GetResult();
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
