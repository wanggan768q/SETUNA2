using System;
using System.Drawing;
using System.Windows.Forms;
using SETUNA.Main;
using SETUNA.Main.Style;
using SETUNA.Main.StyleItems;

namespace SETUNA
{
    /// <summary>
    /// 旧 WinForms 宿主的 UI 工厂注册：把 Core 的 StyleUiFactory/ScrapUiFactory/HostCommands
    /// 全部接回原 WinForms 实现（期 3 Avalonia 宿主另写一套注册）。
    /// </summary>
    internal static class WinFormsUiFactoryRegistration
    {
        private static bool _registered;

        public static void Register()
        {
            if (_registered)
            {
                return;
            }
            _registered = true;

            // 贴图工厂
            SETUNA.Main.ScrapUiFactory.CreateScrap = () => new ScrapBase();
            SETUNA.Main.ScrapUiFactory.StyleListener = () => Mainform.Instance;
            SETUNA.Main.ScrapUiFactory.MenuListener = () => Mainform.Instance;

            // 宿主命令
            HostCommands.RequestCapture = () => Mainform.Instance.StartCapture();
            HostCommands.RequestOption = () => Mainform.Instance.Option();
            HostCommands.RequestShutdown = () => Mainform.Instance.Close();
            HostCommands.SuspendLayerRefresh = () => SETUNA.Main.Layer.LayerManager.Instance.SuspendRefresh();
            HostCommands.ResumeLayerRefresh = () => SETUNA.Main.Layer.LayerManager.Instance.ResumeRefresh();

            // 样式 UI
            StyleUiFactory.ShowSplash = () => new SplashForm().ShowDialog();
            StyleUiFactory.MenuItemFactory = style => new ToolStripStyleButton((CStyle)style);
            StyleUiFactory.MenuItemWithBookFactory = (style, book) => BuildBookMenuItem((CStyle)style, (ScrapBook)book);
            StyleUiFactory.PaintWindowFactory = scrap => new ScrapPaintWindow((ScrapBase)scrap).ShowDialog();
            StyleUiFactory.CompactFormFactory = (scrap, item, clickPoint) =>
                new CompactScrap((ScrapBase)scrap, (CCompactStyleItem)item, (Point)clickPoint).Show();
            StyleUiFactory.PicasaUploadBar = (scrap, item) => ShowPicasaBar((ScrapBase)scrap, (CPicasaUploaderStyleItem)item);

            StyleUiFactory.TrimDialogFactory = scrap => new TrimDialogHost(new TrimWindow((ScrapBase)scrap));
            StyleUiFactory.EditDialogFactory = item => new EditDialogHost(BuildEditPanel(item));

            StyleUiFactory.PreviewDialogFactory = (item, image) =>
            {
                var cimageStyleItem = (CImageJpegStyleItem)item;
                ImageJpegPreviewPanel panel;
                if (cimageStyleItem.HaveMargin)
                {
                    panel = new ImageJpegPreviewPanel(cimageStyleItem, image, cimageStyleItem.GetImageCodecInfoPublic());
                }
                else
                {
                    panel = new ImageJpegPreviewPanel(cimageStyleItem, (Bitmap)image, cimageStyleItem.GetImageCodecInfoPublic());
                }
                return new EditDialogHost(panel);
            };

            StyleUiFactory.ShowMessageBox = (text, caption, buttons) => (int)MessageBox.Show(text, caption, (MessageBoxButtons)buttons);
            StyleUiFactory.ShowSaveFileDialog = (folder, fileName, filter) =>
            {
                using var dialog = new SaveFileDialog
                {
                    InitialDirectory = folder,
                    FileName = fileName,
                    Filter = filter,
                    RestoreDirectory = true,
                };
                SETUNA.Main.Layer.LayerManager.Instance.SuspendRefresh();
                try
                {
                    return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
                }
                finally
                {
                    SETUNA.Main.Layer.LayerManager.Instance.ResumeRefresh();
                }
            };
        }

        /// <summary>Core PanelKey → 原 WinForms 编辑面板。</summary>
        private static Form BuildEditPanel(object styleItem)
        {
            switch (styleItem)
            {
                case CCompactStyleItem i: return new CompactStyleItemPanel(i);
                case CCopyStyleItem i: return new CopyStyleItemPanel(i);
                case CImageBmpStyleItem i: return new ImageBmpStyleItemPanel(i);
                case CImageJpegStyleItem i: return new ImageJpegStyleItemPanel(i);
                case CImagePngStyleItem i: return new ImagePngStyleItemPanel(i);
                case CMarginStyleItem i: return new MarginStyleItemPanel(i);
                case CMoveStyleItem i: return new MoveStyleItemPanel(i);
                case COpacityStyleItem i: return new OpacityStyleItemPanel(i);
                case CPicasaUploaderStyleItem i: return new PicasaStyleItemPanel(i);
                case CRotateStyleItem i: return new RotateStyleItemPanel(i);
                case CScaleStyleItem i: return new ScaleStyleItemPanel(i);
                case CTimerStyleItem i: return new TimerStyleItemPanel(i);
                case CWindowStyleItem i: return new WindowStyleItemPanel(i);
                default: return new NothingStyleItemPanel((CStyleItem)styleItem);
            }
        }

        private static ToolStripItem BuildBookMenuItem(CStyle style, ScrapBook book)
        {
            if (style is CScrapListStyle)
            {
                return new ToolStripScrapList(style.StyleName, book);
            }
            if (style is CDustBoxStyle)
            {
                return new ToolStripDustboxList(style.StyleName, book);
            }
            return new ToolStripStyleButton(style);
        }

        private static void ShowPicasaBar(ScrapBase scrap, CPicasaUploaderStyleItem item)
        {
            var picasaBar = new PicasaBar
            {
                SendImage = scrap.Image,
                ID = item.ID,
                Pass = item.Pass,
                AlbumName = item.AlbumName,
                AlbumComment = item.AlbumComment,
                AlbumPublic = item.IsPublic,
                StartPosition = FormStartPosition.Manual,
                TopMost = true,
            };
            picasaBar.Left = scrap.Left + scrap.Width / 2 - picasaBar.Width / 2;
            picasaBar.Top = scrap.Top + scrap.Height / 2 - picasaBar.Height / 2;
            picasaBar.Show();
        }

        /// <summary>TrimWindow 的 ITrimDialog 适配。</summary>
        private sealed class TrimDialogHost : StyleUiFactory.ITrimDialog
        {
            private readonly TrimWindow _window;

            public TrimDialogHost(TrimWindow window)
            {
                _window = window;
            }

            public System.Drawing.Rectangle TrimRectangle => _window.TrimRectangle;

            public int TrimLeft => _window.TrimLeft;

            public int TrimTop => _window.TrimTop;

            public int ShowTrimDialog() => (int)_window.ShowDialog();

            public void Dispose() => _window.Dispose();
        }

        /// <summary>ToolBoxForm 家族（含 PreviewPanel）的 IStyleItemEditDialog 适配。</summary>
        private sealed class EditDialogHost : StyleUiFactory.IStyleItemEditDialog
        {
            private readonly Form _form;
            private readonly ToolBoxForm _toolBox;

            public EditDialogHost(Form form)
            {
                _form = form;
                _toolBox = form as ToolBoxForm;
            }

            public object EditedStyleItem => _toolBox?.StyleItem;

            public int ShowEditDialog() => (int)_form.ShowDialog();

            public void Dispose() => _form.Dispose();
        }
    }
}
