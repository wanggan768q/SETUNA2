using System.Drawing;
using SETUNA.Core.Platform;

namespace SETUNA.Main.StyleItems
{
    /// <summary>
    /// 平台服务未装配时的 WinForms Clipboard 兜底（旧 WinForms 宿主直跑场景）。
    /// Avalonia 宿主注册 IClipboardService 后不走此路径。
    /// </summary>
    internal sealed class LegacyFormsClipboard : IClipboardService
    {
        public void SetImage(Bitmap image)
        {
            System.Windows.Forms.Clipboard.SetImage(image);
        }

        public Bitmap GetImage()
        {
            return System.Windows.Forms.Clipboard.ContainsImage()
                ? (Bitmap)System.Windows.Forms.Clipboard.GetImage()
                : null;
        }

        public void SetText(string text)
        {
            System.Windows.Forms.Clipboard.SetText(text ?? string.Empty);
        }

        public string GetText()
        {
            return System.Windows.Forms.Clipboard.ContainsText()
                ? System.Windows.Forms.Clipboard.GetText()
                : null;
        }

        public bool ContainsImage => System.Windows.Forms.Clipboard.ContainsImage();

        public bool ContainsText => System.Windows.Forms.Clipboard.ContainsText();
    }
}
