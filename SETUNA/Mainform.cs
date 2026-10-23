using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using com.clearunit;
using SETUNA.Main;
using SETUNA.Main.Clothesline;
using SETUNA.Main.KeyItems;
using SETUNA.Main.Option;
using SETUNA.Main.Style;

namespace SETUNA
{
    // Token: 0x02000037 RID: 55
    public sealed partial class Mainform : BaseForm, IScrapKeyPressEventListener, IScrapAddedListener, IScrapRemovedListener, IScrapStyleListener, IScrapMenuListener, ISingletonForm
    {
        public static Mainform Instance { private set; get; }

        // Token: 0x060001EE RID: 494 RVA: 0x0000A4C4 File Offset: 0x000086C4
        public Mainform()
        {
            Instance = this;

            _isstart = false;
            _iscapture = false;
            _isoption = false;
            InitializeComponent();
            scrapBook = new ScrapBook(this);
            scrapBook.addKeyPressListener(this);
            scrapBook.addScrapAddedListener(this);
            scrapBook.addScrapRemovedListener(this);
            optSetuna = new SetunaOption();
            dustbox = new Queue<ScrapBase>();
            scrapBook.DustBox = dustbox;
            scrapBook.DustBoxCapacity = 5;
            keyBook = optSetuna.GetKeyItemBook();
            _imgpool = new List<ScrapSource>();
            SetSubMenu();

            Text = $"SETUNA {Application.ProductVersion}";

            // 晾绳桥接订阅常驻（不依赖模式开关）：关闭期间新增的截图也要保持事件接线，
            // 否则重新开启模式统一收绳时其挂起状态变化无法同步到 Controller
            scrapBook.ScrapAdded += ScrapAdded_ClotheslineBridge;
            scrapBook.ScrapRemoved += ScrapRemoved_ClotheslineBridge;

            NetUtils.Init();
        }

        // Token: 0x17000055 RID: 85
        // (get) Token: 0x060001F0 RID: 496 RVA: 0x0000A59C File Offset: 0x0000879C
        // (set) Token: 0x060001EF RID: 495 RVA: 0x0000A577 File Offset: 0x00008777
        public bool IsStart
        {
            get => _isstart;
            set
            {
                _isstart = value;
                if (value && _imgpool.Count > 0)
                {
                    timPool.Start();
                }
            }
        }

        // Token: 0x17000056 RID: 86
        // (get) Token: 0x060001F2 RID: 498 RVA: 0x0000A5C9 File Offset: 0x000087C9
        // (set) Token: 0x060001F1 RID: 497 RVA: 0x0000A5A4 File Offset: 0x000087A4
        public bool IsCapture
        {
            get => _iscapture;
            set
            {
                _iscapture = value;
                if (!value && _imgpool.Count > 0)
                {
                    timPool.Start();
                }
            }
        }

        // Token: 0x17000057 RID: 87
        // (get) Token: 0x060001F4 RID: 500 RVA: 0x0000A5F6 File Offset: 0x000087F6
        // (set) Token: 0x060001F3 RID: 499 RVA: 0x0000A5D1 File Offset: 0x000087D1
        public bool IsOption
        {
            get => _isoption;
            set
            {
                _isoption = value;
                if (!value && _imgpool.Count > 0)
                {
                    timPool.Start();
                }
            }
        }

        // Token: 0x060001F5 RID: 501 RVA: 0x0000A600 File Offset: 0x00008800
        private void SetSubMenu()
        {
            setunaIconMenu.Scrap = scrapBook.GetDummyScrap();
            setunaIconMenu.Items.Clear();
            setunaIconMenu.Items.Add(new CScrapListStyle().GetToolStrip(scrapBook));
            setunaIconMenu.Items.Add(new CDustBoxStyle().GetToolStrip(scrapBook));
            setunaIconMenu.Items.Add(new CDustEraseStyle().GetToolStrip());
            setunaIconMenu.Items.Add(new CDustScrapStyle().GetToolStrip());
            setunaIconMenu.Items.Add(new ToolStripSeparator());
            setunaIconMenu.Items.Add(new CCaptureStyle().GetToolStrip());
            setunaIconMenu.Items.Add(new CPasteStyle().GetToolStrip());
            setunaIconMenu.Items.Add(new ToolStripSeparator());
            setunaIconMenu.Items.Add(new CShowVersionStyle().GetToolStrip());
            setunaIconMenu.Items.Add(new COptionStyle().GetToolStrip());
            setunaIconMenu.Items.Add(new ToolStripSeparator());
            setunaIconMenu.Items.Add(new CShutDownStyle().GetToolStrip());
        }

        // Token: 0x060001F6 RID: 502 RVA: 0x0000A740 File Offset: 0x00008940
        public void StartCapture()
        {
            if (IsCapture || Mainform.cap_form == null || IsOption)
            {
                return;
            }
            try
            {
                if (frmClickCapture != null)
                {
                    frmClickCapture.Stop();
                }
                IsCapture = true;
                // 捕获取样前暂停晾绳展示（ShowCapture 内部线程取样后才显示捕获窗，不能等其 VisibleChanged）
                clotheslineController?.SetCapturePaused(true);
                Console.WriteLine(string.Concat(new object[]
                {
                    "9 - ",
                    DateTime.Now.ToString(),
                    " ",
                    DateTime.Now.Millisecond
                }));
                Mainform.cap_form.OnCaptureClose = new CaptureForm.CaptureClosedDelegate(EndCapture);
                Mainform.cap_form.ShowCapture(optSetuna.Setuna);
                Console.WriteLine(string.Concat(new object[]
                {
                    "16 - ",
                    DateTime.Now.ToString(),
                    " ",
                    DateTime.Now.Millisecond
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Mainform StartCapture Exception:" + ex.Message);
                IsCapture = false;
                if (Mainform.cap_form != null)
                {
                    Mainform.cap_form.DialogResult = DialogResult.Cancel;
                }
                EndCapture(Mainform.cap_form);
            }
        }

        // Token: 0x060001F7 RID: 503 RVA: 0x0000A89C File Offset: 0x00008A9C
        private void EndCapture(CaptureForm cform)
        {
            try
            {
                Console.WriteLine("Mainform EndCapture Start---");
                if (cform.DialogResult == DialogResult.OK)
                {
                    using (var clipBitmap = cform.ClipBitmap)
                    {
                        if (clipBitmap != null)
                        {
                            scrapBook.AddScrap(clipBitmap, cform.ClipStart.X, cform.ClipStart.Y, cform.ClipSize.Width, cform.ClipSize.Height);
                        }
                    }
                }
                cform.Hide();
                Cursor.Clip = Rectangle.Empty;
                Console.WriteLine("Mainform EndCapture End---");
            }
            catch (Exception ex)
            {
                Console.WriteLine("MainForm EndCapture Exception:" + ex.Message);
            }
            finally
            {
                IsCapture = false;
                // 捕获结束（成功/取消/异常统一）恢复晾绳展示
                clotheslineController?.SetCapturePaused(false);
                if (frmClickCapture != null)
                {
                    frmClickCapture.Restart();
                }
            }
        }

        // Token: 0x060001F8 RID: 504 RVA: 0x0000A994 File Offset: 0x00008B94
        public void Option()
        {
            if (IsCapture)
            {
                return;
            }
            IsOption = true;
            var opt = (SetunaOption)optSetuna.Clone();
            var list = new List<ScrapBase>();
            try
            {
                foreach (var scrapBase in scrapBook)
                {
                    if (scrapBase.Visible && scrapBase.TopMost)
                    {
                        list.Add(scrapBase);
                    }
                }
                foreach (var scrapBase2 in list)
                {
                    scrapBase2.TopMost = false;
                }
                base.TopMost = false;
                if (frmClickCapture != null)
                {
                    frmClickCapture.Stop();
                }
                var optionForm = new OptionForm(opt)
                {
                    StartPosition = FormStartPosition.CenterScreen
                };
                optionForm.ShowDialog();
                if (optionForm.DialogResult == DialogResult.OK)
                {
                    optSetuna = optionForm.Option;
                    OptionApply();
                }
                if (!optSetuna.RegistHotKey(base.Handle, HotKeyID.Capture))
                {
                    optSetuna.ScrapHotKeyEnable = false;
                    new HotkeyMsg
                    {
                        HotKey = optSetuna.ScrapHotKeys[(int)HotKeyID.Capture]
                    }.ShowDialog();
                }
                if (!optSetuna.RegistHotKey(base.Handle, HotKeyID.Function1))
                {
                    optSetuna.ScrapHotKeyEnable = false;
                    new HotkeyMsg
                    {
                        HotKey = optSetuna.ScrapHotKeys[(int)HotKeyID.Function1]
                    }.ShowDialog();
                }
                if (optionForm.DialogResult == DialogResult.OK)
                {
                    SaveOption();
                }
            }
            finally
            {
                base.TopMost = true;
                foreach (var scrapBase3 in list)
                {
                    scrapBase3.TopMost = true;
                }
                IsOption = false;
            }
        }

        // Token: 0x060001F9 RID: 505 RVA: 0x0000AB90 File Offset: 0x00008D90
        private void OptionApply()
        {
            try
            {
                ApplyClotheslineOption();
                keyBook = optSetuna.GetKeyItemBook();
                if (optSetuna.Setuna.DustBoxEnable)
                {
                    scrapBook.DustBoxCapacity = (short)optSetuna.Setuna.DustBoxCapacity;
                }
                else
                {
                    scrapBook.DustBoxCapacity = 0;
                }
                if (!optSetuna.RegistHotKey(base.Handle, HotKeyID.Capture))
                {
                    optSetuna.ScrapHotKeyEnable = false;
                    new HotkeyMsg
                    {
                        HotKey = optSetuna.ScrapHotKeys[(int)HotKeyID.Capture]
                    }.ShowDialog();
                }
                if (!optSetuna.RegistHotKey(base.Handle, HotKeyID.Function1))
                {
                    optSetuna.ScrapHotKeyEnable = false;
                    new HotkeyMsg
                    {
                        HotKey = optSetuna.ScrapHotKeys[(int)HotKeyID.Function1]
                    }.ShowDialog();
                }
                if (optSetuna.Setuna.AppType == SetunaOption.SetunaOptionData.ApplicationType.ApplicationMode)
                {
                    base.ShowInTaskbar = true;
                    setunaIcon.Visible = false;
                    base.MinimizeBox = true;
                    base.Visible = true;
                }
                else
                {
                    setunaIcon.Visible = true;
                    base.ShowInTaskbar = false;
                    base.MinimizeBox = false;
                    base.WindowState = FormWindowState.Normal;
                    base.Visible = optSetuna.Setuna.ShowMainWindow;
                }
                subMenu.Items.Clear();
                foreach (var num in optSetuna.Scrap.SubMenuStyles)
                {
                    if (num >= 0)
                    {
                        using (var enumerator2 = optSetuna.Styles.GetEnumerator())
                        {
                            while (enumerator2.MoveNext())
                            {
                                var cstyle = enumerator2.Current;
                                if (cstyle.StyleID == num)
                                {
                                    subMenu.Items.Add(cstyle.GetToolStrip(scrapBook));
                                }
                            }
                            continue;
                        }
                    }
                    var preStyle = CPreStyles.GetPreStyle(num);
                    if (preStyle != null)
                    {
                        subMenu.Items.Add(preStyle.GetToolStrip(scrapBook));
                    }
                }
                if (optSetuna.Setuna.ClickCapture)
                {
                    if (frmClickCapture == null)
                    {
                        frmClickCapture = new ClickCapture(optSetuna.Setuna.ClickCaptureValue);
                        frmClickCapture.ClickCaptureEvent += frmClickCapture_ClickCaptureEvent;
                        frmClickCapture.Show();
                    }
                    else
                    {
                        frmClickCapture.ClickFlags = optSetuna.Setuna.ClickCaptureValue;
                        frmClickCapture.Restart();
                    }
                }
                else if (frmClickCapture != null)
                {
                    frmClickCapture.Close();
                    frmClickCapture.Dispose();
                    frmClickCapture = null;
                }

                windowTimer.Enabled = optSetuna.Setuna.TopMostEnabled;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Mainform OptionApply Exception:" + ex.Message);
            }
        }

        /// <summary>
        /// 晾绳模式应用：先配置持久化，再按状态变化更新模型。
        /// 关→开：钩子安装失败则提示并回滚关闭；开→关：统一取下+隐藏+卸钩；状态未变不动。
        /// </summary>
        private void ApplyClotheslineOption()
        {
            var enabled = optSetuna.ClotheslineEnabled;
            if (!IsStart)
            {
                // 启动期（LoadOption→OptionApply）不初始化晾绳：Controller 须在
                // LayerManager.Init 之后、缓存恢复前创建（Mainform_Load 的启动块负责）
                return;
            }
            switch (ClotheslineController.DecideModeAction(enabled, clotheslineModeActive))
            {
                case ClotheslineModeAction.None:
                    // 重复应用设置：不重新挂回已取下的照片（幂等），仅同步预览模式
                    if (enabled && clotheslineController != null)
                    {
                        clotheslineController.PreviewMode = optSetuna.ClotheslinePreviewLocation;
                    }
                    return;
                case ClotheslineModeAction.Enable:
                    EnableClotheslineMode();
                    return;
                case ClotheslineModeAction.Disable:
                    DisableClotheslineMode();
                    return;
            }
        }

        private void EnableClotheslineMode()
        {
            EnsureClotheslineController();
            clotheslineController.PreviewMode = optSetuna.ClotheslinePreviewLocation;
            if (!clotheslineHook.Install())
            {
                MessageBox.Show("晾绳模式键盘钩子安装失败，已保持照片原展示状态。", "SETUNA2", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                optSetuna.ClotheslineEnabled = false;
                return;
            }
            clotheslineModeActive = true;
            clotheslineController.Show();
            // 显式开启统一收绳（已挂起项不受影响）
            scrapBook.HangAll();
        }

        private void DisableClotheslineMode()
        {
            clotheslineModeActive = false;
            UninstallClotheslineHook();
            clotheslineController?.Hide();
            // 统一取下：按各截图记录的期望组合恢复展示
            if (allScrapActive)
            {
                scrapBook.UnhangAll();
            }
            else
            {
                // 全部隐藏中取下：恢复展示组合后立刻隐藏（不提前显示、不进 forms 恢复列表，
                // 隐藏前挂起窗口本就不可见，与全部隐藏语义一致）
                foreach (var scrap in scrapBook)
                {
                    if (scrap.IsClotheslineHanging)
                    {
                        scrap.SetClotheslineHanging(false);
                        scrap.Hide();
                    }
                }
            }
        }

        /// <summary>
        /// 卸钩：若本轮已消费 down 等待配对 keyup，延迟到轮次结束再卸（简报约束 9，不漏放被消费按键的后半段）；
        /// 进程退出路径（DisposeClothesline）直接清理。
        /// </summary>
        private void UninstallClotheslineHook()
        {
            if (clotheslineHook == null)
            {
                return;
            }
            if (clotheslineHook.IsConsumingRound)
            {
                // 消费中的轮次：状态机在 keyup 时自动复位（ClotheslineSpaceKeyStateMachine.OnKeyUp），
                // 排队一个 UI 轮询等待轮次结束后确定性卸钩。
                // 代际标记：期间若重新 Enable 装了新钩，旧钩的等待 tick 不得误卸新钩
                var hook = clotheslineHook;
                var generation = ++clotheslineHookGeneration;
                var waitTimer = new System.Windows.Forms.Timer { Interval = 50 };
                waitTimer.Tick += (s, args) =>
                {
                    var currentGeneration = hook == clotheslineHook ? clotheslineHookGeneration : -1;
                    if (!hook.IsConsumingRound || currentGeneration != generation)
                    {
                        waitTimer.Stop();
                        waitTimer.Dispose();
                        if (hook == clotheslineHook)
                        {
                            hook.Uninstall();
                        }
                    }
                };
                waitTimer.Start();
                return;
            }
            clotheslineHook.Uninstall();
        }

        /// <summary>创建 Controller 并接线（仅一次）：缓存桥接、事件、LayerManager 置顶回调。</summary>
        private void EnsureClotheslineController()
        {
            if (clotheslineController != null)
            {
                return;
            }

            clotheslineController = new ClotheslineController(
                () =>
                {
                    return optSetuna.ClotheslineRopePositionSaved
                        ? optSetuna.ClotheslineRopePosition
                        : new Point(Screen.PrimaryScreen.WorkingArea.X + 100, Screen.PrimaryScreen.WorkingArea.Top + 8);
                },
                position =>
                {
                    optSetuna.ClotheslineRopePosition = position;
                    optSetuna.ClotheslineRopePositionSaved = true;
                    SaveOption();
                });

            // 挂起/取下状态变化 → 同步绳子集合（构造函数已订阅 ScrapAdded/ScrapRemoved）

            clotheslineController.ThumbnailDoubleClicked += scrap =>
            {
                // 双击缩略图 = 取下（走任务 3 的恢复路径，不隐式 Show）
                scrap.SetClotheslineHanging(false);
            };
            clotheslineController.PreviewVisibilityChanged += visible =>
            {
                if (visible)
                {
                    SETUNA.Main.Layer.LayerManager.Instance.RefreshLayer();
                }
            };

            clotheslineHook = new ClotheslineSpaceKeyHook(IsSpaceKeyHit);
            clotheslineHook.ToggleRequested += () =>
            {
                // 钩子回调线程（安装线程消息循环）投递到 UI 线程
                BeginInvoke(new Action(ToggleSpacePreview));
            };

            SETUNA.Main.Layer.LayerManager.TopMostExtension = BringClotheslineToFront;
        }

        /// <summary>钩子命中检测（轻量、纯内存）：鼠标在绳窗缩略图照片矩形内，或预览已打开。</summary>
        private bool IsSpaceKeyHit()
        {
            if (!clotheslineModeActive || clotheslineController == null)
            {
                return false;
            }
            if (clotheslineController.IsPreviewOpen)
            {
                return true;
            }
            return clotheslineController.FindThumbnailAt(Cursor.Position) != null;
        }

        /// <summary>空格切换预览（UI 线程）：无预览则对命中的缩略图打开，有预览则关闭。</summary>
        private void ToggleSpacePreview()
        {
            if (!clotheslineModeActive || clotheslineController == null)
            {
                return;
            }
            if (clotheslineController.IsPreviewOpen)
            {
                clotheslineController.ClosePreview();
                return;
            }
            var scrap = clotheslineController.FindThumbnailAt(Cursor.Position);
            if (scrap != null)
            {
                clotheslineController.OpenPreview(scrap);
            }
        }

        private void BringClotheslineToFront()
        {
            clotheslineController?.BringRopeWindowsToFront();
        }

        private void ScrapAdded_ClotheslineBridge(object sender, ScrapEventArgs e)
        {
            var scrap = e.scrap;
            if (scrap == null)
            {
                return;
            }
            // 缓存恢复路径的挂起预设发生在本订阅触发之前，这里补挂到绳子；
            // 订阅常驻（模式关闭时也保持），Controller 为 null 时跳过同步
            if (scrap.IsClotheslineHanging)
            {
                clotheslineController?.AttachScrap(scrap);
            }
            scrap.ClotheslineHangingChanged += Scrap_HangingChanged;
            scrap.ScrapImageChangedEvent += Scrap_ImageChanged;
            // 原地旋转（RotateFlip 改像素不改 Image 引用）不触发 ScrapImageChanged，
            // 计划要求样式完成（含原地旋转）后更新缩略图和预览，走样式应用事件补齐
            scrap.ScrapStyleAppliedEvent += Scrap_ImageChanged;
        }

        private void ScrapRemoved_ClotheslineBridge(object sender, ScrapEventArgs e)
        {
            var scrap = e.scrap;
            if (scrap == null)
            {
                return;
            }
            scrap.ClotheslineHangingChanged -= Scrap_HangingChanged;
            scrap.ScrapImageChangedEvent -= Scrap_ImageChanged;
            scrap.ScrapStyleAppliedEvent -= Scrap_ImageChanged;
            clotheslineController?.RemoveScrap(scrap);
        }

        private void Scrap_ImageChanged(object sender, ScrapEventArgs e)
        {
            // 缩略图与预览随新图重建（缩放/替换图像后绳子上的小图保持同步）
            clotheslineController?.UpdateScrap(e.scrap);
        }

        private void Scrap_HangingChanged(object sender, ScrapEventArgs e)
        {
            var scrap = e.scrap;
            if (scrap == null)
            {
                return;
            }
            if (scrap.IsClotheslineHanging)
            {
                clotheslineController?.AttachScrap(scrap);
            }
            else
            {
                clotheslineController?.DetachScrap(scrap);
            }
        }

        // Token: 0x060001FA RID: 506 RVA: 0x0000AE78 File Offset: 0x00009078
        private void frmClickCapture_ClickCaptureEvent(object sender, EventArgs e)
        {
            StartCapture();
        }

        // Token: 0x060001FB RID: 507 RVA: 0x0000AE80 File Offset: 0x00009080
        private void CloseSetuna()
        {
            base.Close();
        }

        // Token: 0x060001FC RID: 508 RVA: 0x0000AE88 File Offset: 0x00009088
        public void ScrapKeyPress(object sender, ScrapKeyPressEventArgs e)
        {
            var keyItem = keyBook.FindKeyItem(e.key);
            if (keyItem != null)
            {
                var scrapBase = (ScrapBase)sender;
                keyItem.ParentStyle.Apply(ref scrapBase);
            }
        }

        // Token: 0x060001FD RID: 509 RVA: 0x0000AEBE File Offset: 0x000090BE
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x0312)
            {
                var id = (HotKeyID)m.WParam;
                switch (id)
                {
                    case HotKeyID.Capture:
                        StartCapture();
                        break;
                    case HotKeyID.Function1:
                        SetAllScrapsActive(!allScrapActive);
                        break;
                }
            }
        }

        // Token: 0x060001FE RID: 510 RVA: 0x0000AEE8 File Offset: 0x000090E8
        private void SaveOption()
        {
            var configFile = SetunaOption.ConfigFile;
            var allType = SetunaOption.GetAllType();
            try
            {
                var xmlSerializer = new XmlSerializer(optSetuna.GetType(), allType);
                var fileStream = new FileStream(configFile, FileMode.Create);
                xmlSerializer.Serialize(fileStream, optSetuna);
                fileStream.Close();
            }
            catch
            {
                MessageBox.Show("无法保存配置文件。", "SETUNA2", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
        }

        // Token: 0x060001FF RID: 511 RVA: 0x0000AF58 File Offset: 0x00009158
        private void LoadOption()
        {
            var configFile = SetunaOption.ConfigFile;
            try
            {
                if (!File.Exists(configFile))
                {
                    optSetuna = SetunaOption.GetDefaultOption();
                }
                else
                {
                    var allType = SetunaOption.GetAllType();
                    var xmlSerializer = new XmlSerializer(typeof(SetunaOption), allType);
                    var fileStream = new FileStream(configFile, FileMode.Open);
                    optSetuna = (SetunaOption)xmlSerializer.Deserialize(fileStream);
                    fileStream.Close();
                }
            }
            catch
            {
                optSetuna = SetunaOption.GetDefaultOption();
                MessageBox.Show("无法读取配置文件。\n使用默认设置。", "SETUNA2", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
            finally
            {
                OptionApply();
            }
        }

        // Token: 0x06000200 RID: 512 RVA: 0x0000B000 File Offset: 0x00009200
        public void RestoreScrap(object sender, EventArgs e)
        {
            var toolStripMenuItem = (ToolStripMenuItem)sender;
            var list = new List<ScrapBase>();
            while (dustbox.Count > 0)
            {
                var scrapBase = dustbox.Dequeue();
                if (!scrapBase.Equals(toolStripMenuItem.Tag))
                {
                    list.Add(scrapBase);
                }
                else
                {
                    scrapBook.AddScrapThenDo(scrapBase);
                }
            }
            dustbox.Clear();
            foreach (var item in list)
            {
                dustbox.Enqueue(item);
            }
            new ScrapEventArgs();
        }

        // Token: 0x06000201 RID: 513 RVA: 0x0000B0B8 File Offset: 0x000092B8
        private void miCapture_Click(object sender, EventArgs e)
        {
            StartCapture();
        }

        // Token: 0x06000202 RID: 514 RVA: 0x0000B0C0 File Offset: 0x000092C0
        private void miOption_Click(object sender, EventArgs e)
        {
            Option();
        }

        // Token: 0x06000203 RID: 515 RVA: 0x0000B0C8 File Offset: 0x000092C8
        private void miSetunaClose_Click(object sender, EventArgs e)
        {
            CloseSetuna();
        }

        // Token: 0x06000204 RID: 516 RVA: 0x0000B0D0 File Offset: 0x000092D0
        public void OnActiveScrapInList(object sender, EventArgs e)
        {
            var toolStripMenuItem = (ToolStripMenuItem)sender;
            if (toolStripMenuItem.Tag != null)
            {
                var scrapBase = (ScrapBase)toolStripMenuItem.Tag;
                if (scrapBase.IsClotheslineHanging)
                {
                    // 挂起项：滚动绳子定位其缩略图，不隐式 Show 原图
                    clotheslineController?.ScrollToScrap(scrapBase);
                }
                else if (scrapBase.Visible)
                {
                    scrapBase.Activate();
                }
                else if (scrapBase.StyleForm is Main.StyleItems.CompactScrap compactScrap)
                {
                    compactScrap.Close();
                }
            }
        }

        // Token: 0x06000205 RID: 517 RVA: 0x0000B0FE File Offset: 0x000092FE
        public void ScrapAdded(object sender, ScrapEventArgs e)
        {
        }

        // Token: 0x06000206 RID: 518 RVA: 0x0000B100 File Offset: 0x00009300
        public void ScrapRemoved(object sender, ScrapEventArgs e)
        {
        }

        // Token: 0x06000207 RID: 519 RVA: 0x0000B102 File Offset: 0x00009302
        private void button1_Click(object sender, EventArgs e)
        {
            StartCapture();
        }

        // Token: 0x06000208 RID: 520 RVA: 0x0000B10A File Offset: 0x0000930A
        private void button4_Click(object sender, EventArgs e)
        {
            Option();
        }

        // Token: 0x06000209 RID: 521 RVA: 0x0000B114 File Offset: 0x00009314
        public void ScrapCreated(object sender, ScrapEventArgs e)
        {
            var cstyle = optSetuna.FindStyle(optSetuna.Scrap.CreateStyleID);
            if (cstyle != null)
            {
                cstyle.Apply(ref e.scrap);
                return;
            }
        }

        // Token: 0x0600020A RID: 522 RVA: 0x0000B168 File Offset: 0x00009368
        public void ScrapInactived(object sender, ScrapEventArgs e)
        {
            if (optSetuna.Scrap.InactiveAlphaChange)
            {
                e.scrap.InactiveOpacity = 1.0 - optSetuna.Scrap.InactiveAlphaValue / 100.0;
            }
            else
            {
                e.scrap.InactiveOpacity = e.scrap.ActiveOpacity;
            }
            var inactiveLineChange = optSetuna.Scrap.InactiveLineChange;
        }

        // Token: 0x0600020B RID: 523 RVA: 0x0000B1E0 File Offset: 0x000093E0
        public void ScrapActivated(object sender, ScrapEventArgs e)
        {
            var inactiveAlphaChange = optSetuna.Scrap.InactiveAlphaChange;
            var inactiveLineChange = optSetuna.Scrap.InactiveLineChange;
        }

        // Token: 0x0600020C RID: 524 RVA: 0x0000B204 File Offset: 0x00009404
        public void ScrapInactiveMouseOver(object sender, ScrapEventArgs e)
        {
            if (optSetuna.Scrap.MouseOverAlphaChange)
            {
                e.scrap.RollOverOpacity = 1.0 - optSetuna.Scrap.MouseOverAlphaValue / 100.0;
            }
            else
            {
                e.scrap.RollOverOpacity = e.scrap.ActiveOpacity;
            }
            var mouseOverLineChange = optSetuna.Scrap.MouseOverLineChange;
        }

        // Token: 0x0600020D RID: 525 RVA: 0x0000B27C File Offset: 0x0000947C
        public void ScrapInactiveMouseOut(object sender, ScrapEventArgs e)
        {
            if (optSetuna.Scrap.InactiveAlphaChange)
            {
                e.scrap.InactiveOpacity = 1.0 - optSetuna.Scrap.InactiveAlphaValue / 100.0;
            }
            else
            {
                e.scrap.InactiveOpacity = e.scrap.ActiveOpacity;
            }
            var inactiveLineChange = optSetuna.Scrap.InactiveLineChange;
        }

        // Token: 0x0600020E RID: 526 RVA: 0x0000B2F4 File Offset: 0x000094F4
        private void button6_Click(object sender, EventArgs e)
        {
            optSetuna = SetunaOption.GetDefaultOption();
            var num = 0;
            num++;
        }

        // Token: 0x0600020F RID: 527 RVA: 0x0000B312 File Offset: 0x00009512
        private void button7_Click(object sender, EventArgs e)
        {
        }

        // Token: 0x06000210 RID: 528 RVA: 0x0000B314 File Offset: 0x00009514
        private void Mainform_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (HotKeyID item in Enum.GetValues(typeof(HotKeyID)))
            {
                optSetuna.UnregistHotKey(Handle, item);
            }

            // 晾绳清理：解绑、停计时、释放窗体/缩略图、确定性卸钩。
            // 不调用 CloseAllScrap()（避免进入用户删除/回收站路径）
            DisposeClothesline();
        }

        /// <summary>退出时的晾绳确定性清理（幂等）。</summary>
        private void DisposeClothesline()
        {
            scrapBook.ScrapAdded -= ScrapAdded_ClotheslineBridge;
            scrapBook.ScrapRemoved -= ScrapRemoved_ClotheslineBridge;
            SETUNA.Main.Layer.LayerManager.TopMostExtension = null;
            clotheslineHook?.Uninstall();
            clotheslineHook = null;
            clotheslineController?.Dispose();
            clotheslineController = null;
            miClotheslineHang = null;
            clotheslineModeActive = false;
        }

        // Token: 0x06000211 RID: 529 RVA: 0x0000B324 File Offset: 0x00009524
        private void Mainform_Load(object sender, EventArgs e)
        {
            base.Visible = false;
            LoadOption();
            OptionApply();
            SaveOption();
            if (optSetuna.Setuna.ShowSplashWindow)
            {
                frmSplash = new SplashForm();
                base.AddOwnedForm(frmSplash);
                frmSplash.Show(this);
                frmSplash.SplashTimer.Start();
            }
            timPool.Start();
            Mainform.cap_form = new CaptureForm(optSetuna.Setuna);
            IsStart = true;

            SETUNA.Main.Layer.LayerManager.Instance.Init();
            // 晾绳控制器：层级管理初始化后、缓存恢复前创建（缓存恢复链 AddScrapFromCache 会预设挂起）
            if (optSetuna.ClotheslineEnabled)
            {
                EnsureClotheslineController();
                if (!clotheslineHook.Install())
                {
                    MessageBox.Show("晾绳模式键盘钩子安装失败，已保持照片原展示状态。", "SETUNA2", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                    optSetuna.ClotheslineEnabled = false;
                    SaveOption();
                }
                else
                {
                    clotheslineModeActive = true;
                    clotheslineController.PreviewMode = optSetuna.ClotheslinePreviewLocation;
                    clotheslineController.Show();
                }
            }
            SETUNA.Main.Cache.CacheManager.Instance.Init();
            delayInitTimer.Start();
            
            // 程序启动时默认最小化到托盘
            StartupMinimizeToTray();
        }

        // Token: 0x06000212 RID: 0x0000B3B6 File Offset: 0x000095B6
        private void setunaIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 从系统托盘恢复窗口
                this.Show();
                this.ShowInTaskbar = true;
                this.WindowState = FormWindowState.Normal;
                base.Activate();
                SETUNA.Main.Layer.LayerManager.Instance.RefreshLayer();
            }
        }

        private void setunaIcon_MouseDoubleClick(object sender, EventArgs e)
        {
            Option();
        }

        // Token: 0x06000213 RID: 531 RVA: 0x0000B3BE File Offset: 0x000095BE
        public void ScrapMenuOpening(object sender, ScrapMenuArgs e)
        {
            subMenu.Scrap = e.scrap;
            UpdateClotheslineMenuItem(e.scrap);
            subMenu.Show(e.scrap, e.scrap.PointToClient(Cursor.Position));

            subMenu.MouseWheel -= SubMenu_MouseWheel;
            subMenu.MouseWheel += SubMenu_MouseWheel;
        }

        /// <summary>"挂回绳子"菜单项：仅模式开启且目标未挂起时可见；复用单实例不累积。</summary>
        private void UpdateClotheslineMenuItem(ScrapBase scrap)
        {
            var applicable = clotheslineModeActive && scrap != null && !scrap.IsClotheslineHanging;
            if (!applicable)
            {
                if (miClotheslineHang != null)
                {
                    subMenu.Items.Remove(miClotheslineHang);
                }
                return;
            }

            if (miClotheslineHang == null)
            {
                miClotheslineHang = new ToolStripMenuItem("挂回绳子");
                miClotheslineHang.Click += MiClotheslineHang_Click;
            }
            if (!subMenu.Items.Contains(miClotheslineHang))
            {
                subMenu.Items.Insert(0, miClotheslineHang);
            }
        }

        private void MiClotheslineHang_Click(object sender, EventArgs e)
        {
            var scrap = subMenu.Scrap;
            if (scrap != null && !scrap.IsClotheslineHanging)
            {
                scrap.SetClotheslineHanging(true);
            }
        }

        private void SubMenu_MouseWheel(object sender, MouseEventArgs e)
        {
            if (MouseWheelCallbackEvent != null)
            {
                MouseWheelCallbackEvent(sender, e);
            }
        }

        // Token: 0x06000214 RID: 532 RVA: 0x0000B3F2 File Offset: 0x000095F2
        private void button2_Click(object sender, EventArgs e)
        {
            GC.Collect();
        }

        // Token: 0x06000215 RID: 533 RVA: 0x0000B3FC File Offset: 0x000095FC
        private void timPool_Tick(object sender, EventArgs e)
        {
            if (_imgpool.Count == 0 || IsCapture || IsOption || !IsStart)
            {
                timPool.Stop();
                return;
            }

            for (var i = 0; i < _imgpool.Count; i++)
            {
                var scrap = _imgpool[i];
                if (scrap.IsDone)
                {
                    _imgpool.RemoveAt(i);

                    using (var scrapSource = scrap)
                    {
                        CreateScrapFromsource(scrapSource);
                    }

                    break;
                }
            }
        }

        // Token: 0x06000216 RID: 534 RVA: 0x0000B46C File Offset: 0x0000966C
        public void AddImageList(ScrapSource src)
        {
            _imgpool.Add(src);
            timPool.Start();
        }

        // Token: 0x06000217 RID: 535 RVA: 0x0000B488 File Offset: 0x00009688
        public void CreateScrapFromImage(Image image, Point location)
        {
            if (image == null)
            {
                return;
            }
            using (var bitmap = (Bitmap)image.Clone())
            {
                if (location == Point.Empty)
                {
                    location = Cursor.Position;
                }
                var x = location.X;
                var y = location.Y;
                scrapBook.AddScrap((Bitmap)bitmap.Clone(), x, y, bitmap.Width, bitmap.Height);
            }
        }

        // Token: 0x06000218 RID: 536 RVA: 0x0000B50C File Offset: 0x0000970C
        private void CreateScrapFromsource(ScrapSource src)
        {
            CreateScrapFromImage(src.GetImage(), src.GetPosition());
        }

        // Token: 0x06000219 RID: 537 RVA: 0x0000B520 File Offset: 0x00009720
        void ISingletonForm.DetectExternalStartup(string version, string[] args)
        {
            base.Invoke(new Mainform.ExternalStartupDelegate(ExternalStartup), new object[]
            {
                version,
                args
            });
        }

        // Token: 0x0600021A RID: 538 RVA: 0x0000B550 File Offset: 0x00009750
        private void ExternalStartup(string version, string[] args)
        {
            if (Application.ProductVersion != version)
            {
                MessageBox.Show("SETUNA已经运行在不同的版本。", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            if (args.Length > 0)
            {
                CommandRun(args);
                return;
            }
            if (optSetuna.Setuna.DupType == SetunaOption.SetunaOptionData.OpeningType.Capture)
            {
                StartCapture();
            }
        }

        // Token: 0x0600021B RID: 539 RVA: 0x0000B5A8 File Offset: 0x000097A8
        public void CommandRun(string[] args)
        {
            Console.WriteLine("-命令行参数--------------------");
            var num = 0;
            var rect = new Rectangle(0, 0, 0, 0);
            var fname = "";
            foreach (var text in args)
            {
                try
                {
                    var text2 = text;
                    var text3 = "";
                    if (text.Length > 3)
                    {
                        text3 = text2.Substring(0, 3);
                        if (text3.Substring(0, 1) == "/" && text3.Substring(2, 1) == ":")
                        {
                            text2 = text.Substring(text3.Length, text2.Length - text3.Length);
                        }
                        else
                        {
                            text3 = "";
                        }
                    }
                    if (text3.Length > 0)
                    {
                        if (text3 == "/R:")
                        {
                            var array = text2.Split(new char[]
                            {
                                ','
                            });
                            if (array.Length == 4)
                            {
                                rect = default(Rectangle);
                                rect.X = int.Parse(array[0]);
                                rect.Y = int.Parse(array[1]);
                                rect.Width = int.Parse(array[2]);
                                rect.Height = int.Parse(array[3]);
                                Console.WriteLine("[位置]" + rect.ToString());
                                goto IL_1C2;
                            }
                        }
                        if (text3 == "/P:")
                        {
                            fname = text2;
                        }
                        if (text3 == "/C:")
                        {
                            if (text2.ToUpper() == "OPTION")
                            {
                                num = 1;
                                goto IL_1C2;
                            }
                            if (text2.ToUpper() == "CAPTURE")
                            {
                                num = 2;
                                goto IL_1C2;
                            }
                            if (text2.ToUpper() == "SUBMENU")
                            {
                                num = 3;
                                goto IL_1C2;
                            }
                        }
                    }
                    AddImageList(new ScrapSourcePath(text2));
                    Console.WriteLine(text2);
                }
                catch
                {
                    Console.WriteLine("[Error]" + text);
                }
            IL_1C2:;
            }
            Console.WriteLine("---------------------------------------");
            if (rect.Width >= 10 && rect.Height >= 10)
            {
                CommandCutRect(rect, fname);
                return;
            }
            if (num != 0 && IsStart)
            {
                switch (num)
                {
                    case 1:
                        if (!IsOption)
                        {
                            Option();
                            return;
                        }
                        break;
                    case 2:
                        if (!IsCapture)
                        {
                            StartCapture();
                        }
                        break;
                    default:
                        return;
                }
            }
        }

        // Token: 0x0600021C RID: 540 RVA: 0x0000B80C File Offset: 0x00009A0C
        private void CommandCutRect(Rectangle rect, string fname)
        {
            // 直接抓屏路径同样暂停晾绳展示，抓取结果不含绳子/预览画面
            clotheslineController?.SetCapturePaused(true);
            try
            {
                using (var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb))
                {
                    var point = new Point(rect.X, rect.Y);
                    CaptureForm.CopyFromScreen(bitmap, point);
                    if (fname == "")
                    {
                        AddImageList(new ScrapSourceImage(bitmap, point));
                    }
                }
            }
            finally
            {
                clotheslineController?.SetCapturePaused(false);
            }
        }

        // Token: 0x0600021D RID: 541 RVA: 0x0000B888 File Offset: 0x00009A88
        private void Mainform_Shown(object sender, EventArgs e)
        {
        }

        // 程序启动时最小化到托盘
        private void StartupMinimizeToTray()
        {
            // 延迟执行，确保程序完全启动后再最小化
            System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
            {
                this.Invoke(new Action(() =>
                {
                    // 设置窗口状态为最小化
                    this.WindowState = FormWindowState.Minimized;
                    
                    // 隐藏窗口，显示托盘图标
                    this.Hide();
                    this.ShowInTaskbar = false;
                    setunaIcon.Visible = true;
                    
                    // 显示启动提示
                    setunaIcon.ShowBalloonTip(3000, "SETUNA2", "SETUNA2已启动并最小化到系统托盘", ToolTipIcon.Info);
                }));
            });
        }

        // 窗口大小变化事件处理
        private void Mainform_Resize(object sender, EventArgs e)
        {
            // 当窗口最小化时，隐藏到系统托盘
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
                setunaIcon.Visible = true;
                
                // 显示提示信息
                setunaIcon.ShowBalloonTip(2000, "SETUNA2", "程序已最小化到系统托盘", ToolTipIcon.Info);
                
                // 确保热键在窗口隐藏后仍然有效
                // 在窗口隐藏后重新注册热键，确保消息能正确传递
                if (optSetuna.ScrapHotKeyEnable)
                {
                    // 先注销所有热键
                    foreach (HotKeyID hotKeyID in Enum.GetValues(typeof(HotKeyID)))
                    {
                        if (hotKeyID != HotKeyID.__Count__)
                        {
                            optSetuna.UnregistHotKey(this.Handle, hotKeyID);
                        }
                    }
                    
                    // 延迟重新注册热键，确保窗口状态稳定
                    System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                    {
                        this.Invoke(new Action(() =>
                        {
                            foreach (HotKeyID hotKeyID in Enum.GetValues(typeof(HotKeyID)))
                            {
                                if (hotKeyID != HotKeyID.__Count__)
                                {
                                    optSetuna.RegistHotKey(this.Handle, hotKeyID);
                                }
                            }
                        }));
                    });
                }
            }
        }

        // Token: 0x0600021E RID: 542 RVA: 0x0000B88A File Offset: 0x00009A8A
        private void setunaIconMenu_Opening(object sender, CancelEventArgs e)
        {
            e.Cancel = false;
        }


        private void window_Tick(object sender, EventArgs e)
        {
            SETUNA.Main.WindowManager.Instance.Update();
        }

        private void SetAllScrapsActive(bool active)
        {
            if (allScrapActive == active)
            {
                return;
            }

            allScrapActive = active;

            // 全部隐藏与捕获暂停分别记录（Controller 内部互不清除）
            clotheslineController?.SetAllHidden(!active);

            if (active)
            {
                forms.ForEach(x => x.Visible = active);
                forms.Clear();

                Main.Layer.LayerManager.Instance.ResumeRefresh();
            }
            else
            {
                foreach (var item in scrapBook)
                {
                    if (item.Visible)
                    {
                        forms.Add(item);
                    }
                    else if ((item.StyleForm?.Visible ?? false) == true)
                    {
                        forms.Add(item.StyleForm);
                    }
                }

                forms.ForEach(x => x.Visible = active);

                Main.Layer.LayerManager.Instance.SuspendRefresh();
            }
        }

        private void delayInitTimer_Tick(object sender, EventArgs e)
        {
            if (SETUNA.Main.Cache.CacheManager.Instance.IsInit)
            {
                delayInitTimer.Enabled = false;
                SETUNA.Main.Layer.LayerManager.Instance.RefreshLayer();
                SETUNA.Main.Layer.LayerManager.Instance.DelayInit();
            }
        }



        // Token: 0x040000E1 RID: 225
        private SplashForm frmSplash;

        // Token: 0x040000E2 RID: 226
        public ScrapBook scrapBook;

        // Token: 0x040000E3 RID: 227
        public SetunaOption optSetuna;

        // Token: 0x040000E4 RID: 228
        public KeyItemBook keyBook;

        // Token: 0x040000E5 RID: 229
        public Queue<ScrapBase> dustbox;

        // Token: 0x040000E6 RID: 230
        private ClickCapture frmClickCapture;

        // Token: 0x040000E7 RID: 231
        private static CaptureForm cap_form;

        // Token: 0x040000E8 RID: 232
        private List<ScrapSource> _imgpool;

        // Token: 0x040000E9 RID: 233
        private bool _iscapture;

        // Token: 0x040000EA RID: 234
        private bool _isoption;

        // Token: 0x040000EB RID: 235
        private bool _isstart;

        // Token: 0x02000041 RID: 65
        // (Invoke) Token: 0x0600026A RID: 618
        private delegate void ExternalStartupDelegate(string version, string[] args);

        public delegate void MouseWheelCallback(object sender, MouseEventArgs e);
        public event MouseWheelCallback MouseWheelCallbackEvent;

        private List<Form> forms = new List<Form>();
        private bool allScrapActive = true;

        // —— 晾绳模式（任务 4 接线） ——
        private ClotheslineController clotheslineController;
        private ClotheslineSpaceKeyHook clotheslineHook;
        private ToolStripMenuItem miClotheslineHang;
        private bool clotheslineModeActive;
        private int clotheslineHookGeneration;
    }
}
