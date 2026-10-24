using System;

namespace SETUNA.Main.Style
{
    /// <summary>宿主通用 UI 能力：消息框 / 文件对话框 / 剪贴板之外的零散弹窗。</summary>
    public static class StyleUiFactory
    {
        /// <summary>ToolBoxForm 工厂：(styleItem) → 编辑对话框宿主抽象。</summary>
        public static Func<object, IStyleItemEditDialog> EditDialogFactory;

        /// <summary>收缩窗工厂：(scrap, styleItem, clickPoint) → 收缩贴图窗体。</summary>
        public static Action<object, object, object> CompactFormFactory;

        /// <summary>绘图窗工厂：(scrap) → 绘图编辑窗体（ShowDialog 语义）。</summary>
        public static Action<object> PaintWindowFactory;

        /// <summary>裁剪窗工厂：(scrap) → 裁剪对话框宿主抽象。</summary>
        public static Func<object, ITrimDialog> TrimDialogFactory;

        /// <summary>版本信息弹窗。</summary>
        public static Action ShowSplash;

        /// <summary>JPEG 质量预览对话框工厂：(styleItem, previewImage) → 编辑对话框。</summary>
        public static Func<object, Image, IStyleItemEditDialog> PreviewDialogFactory;

        /// <summary>Picasa 上传栏工厂：(scrap, styleItem)。未注册时跳过（服务已停）。</summary>
        public static Action<object, object> PicasaUploadBar;

        /// <summary>托盘/贴图菜单项工厂：(style) → 宿主菜单项对象。</summary>
        public static Func<object, object> MenuItemFactory;

        /// <summary>带书签菜单项工厂：(style, scrapbook) → 宿主菜单项对象。</summary>
        public static Func<object, object, object> MenuItemWithBookFactory;

        /// <summary>消息框：返回值对齐 WinForms DialogResult（Yes=6, No=7, Cancel=2, OK=1）。</summary>
        public static Func<string, string, int, int> ShowMessageBox;

        /// <summary>另存为对话框：返回选定路径；取消返回 null。</summary>
        public static Func<string, string, string, string> ShowSaveFileDialog;

        /// <summary>编辑对话框宿主抽象。</summary>
        public interface IStyleItemEditDialog : IDisposable
        {
            /// <summary>ShowDialog 返回的 DialogResult（OK=1）。</summary>
            int ShowEditDialog();

            /// <summary>用户调整后的新 StyleItem。</summary>
            object EditedStyleItem { get; }
        }

        /// <summary>裁剪对话框宿主抽象。</summary>
        public interface ITrimDialog : IDisposable
        {
            /// <summary>ShowDialog 返回的 DialogResult（OK=1）。</summary>
            int ShowTrimDialog();

            /// <summary>裁剪区域（物理像素，相对贴图）。</summary>
            System.Drawing.Rectangle TrimRectangle { get; }

            int TrimLeft { get; }

            int TrimTop { get; }
        }

        // DialogResult 常量（对齐 System.Windows.Forms.DialogResult）
        public const int DialogResultOK = 1;
        public const int DialogResultCancel = 2;
        public const int DialogResultYes = 6;
        public const int DialogResultNo = 7;

        /// <summary>消息框便捷方法；工厂未注册时返回 Cancel。</summary>
        public static int MessageBox(string text, string caption, int buttons)
        {
            return ShowMessageBox?.Invoke(text, caption, buttons) ?? DialogResultCancel;
        }

        /// <summary>另存为便捷方法；工厂未注册时返回 null。</summary>
        public static string SaveFileDialog(string initialFolder, string fileName, string filter)
        {
            return ShowSaveFileDialog?.Invoke(initialFolder, fileName, filter);
        }
    }
}
