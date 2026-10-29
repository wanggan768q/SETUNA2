using System;
using SETUNA.Core.Platform;

namespace SETUNA.Platform.Windows
{
    /// <summary>Windows 平台服务聚合。在入口按需装配到 PlatformServices.Register()。</summary>
    public sealed class WindowsPlatformService : IPlatformService, IDisposable
    {
        public IHotkeyService Hotkey { get; }
        public IScreenCaptureService ScreenCapture { get; }
        public IWindowService Window { get; }
        public ITrayService Tray { get; }
        public IAutoStartService AutoStart { get; }
        public IClipboardService Clipboard { get; }

        private readonly WindowsHotkeyService _hotkeyImpl;

        public WindowsPlatformService()
        {
            _hotkeyImpl = new WindowsHotkeyService();
            Hotkey = _hotkeyImpl;
            ScreenCapture = new WindowsScreenCaptureService();
            Window = new WindowsWindowService();
            Tray = new WindowsTrayService();
            AutoStart = new WindowsAutoStartService();
            Clipboard = new WindowsClipboardService();
        }

        public void Dispose()
        {
            _hotkeyImpl.Dispose();
            (Tray as IDisposable)?.Dispose();
        }
    }
}
