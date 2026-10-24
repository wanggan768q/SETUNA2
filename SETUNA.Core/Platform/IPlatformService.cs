using System;
using System.Collections.Generic;
using System.Drawing;

namespace SETUNA.Core.Platform
{
    /// <summary>全局热键 + 低级键盘过滤（原 RegisterHotKey / ClotheslineSpaceKeyHook 的抽象）。</summary>
    public interface IHotkeyService
    {
        /// <summary>注册全局热键。同一 id 重复注册会先注销再注册（对齐原 RegistHotKey 行为）。失败返回 false。</summary>
        bool Register(HotkeyId id, HotkeySpec spec, Action callback);

        void Unregister(HotkeyId id);

        void UnregisterAll();

        /// <summary>
        /// 低级键盘过滤器（晾绳空格钩子）。返回 true 表示消费该键。
        /// 传 null 清除过滤。修饰键状态由调用方通过 GetModifierState 查询。
        /// </summary>
        void SetKeyFilter(Func<int, bool> virtualKeyFilter);

        /// <summary>查询当前修饰键按下状态（返回 MOD_* 位掩码：1=Alt 2=Ctrl 4=Shift 8=Win）。</summary>
        int GetModifierState();
    }

    /// <summary>屏幕捕获与显示器信息（原 BitBlt/CopyFromScreen/Screen.AllScreens 的抽象）。</summary>
    public interface IScreenCaptureService
    {
        /// <summary>抓取屏幕指定区域（物理像素坐标），返回 24bpp GDI Bitmap（对齐原 CaptureForm.ImgSnap 格式）。</summary>
        Bitmap CaptureScreen(Rectangle bounds);

        /// <summary>全部显示器信息。</summary>
        IReadOnlyList<MonitorInfo> GetMonitors();

        /// <summary>虚拟桌面整体边界（原 SystemInformation.VirtualScreen）。</summary>
        Rectangle GetVirtualScreenBounds();

        /// <summary>包含指定点的显示器。</summary>
        MonitorInfo GetMonitorFromPoint(Point point);

        /// <summary>鼠标光标当前全局位置。</summary>
        Point GetCursorPosition();

        /// <summary>把鼠标光标图像画到给定图像上（原 WindowsAPI.DrawCursorImageToScreenImage）。</summary>
        void DrawCursorToImage(Bitmap target, Point targetLocation);
    }

    /// <summary>窗口行为：置顶/穿透/透明/Z序/枚举（原 user32 P/Invoke 散点的抽象）。</summary>
    public interface IWindowService
    {
        void SetTopmost(IntPtr hwnd, bool topmost);

        /// <summary>设置/取消鼠标点击穿透（WS_EX_TRANSPARENT|WS_EX_LAYERED）。</summary>
        void SetClickThrough(IntPtr hwnd, bool through);

        /// <summary>设置 WS_EX_LAYERED 分层窗口的整体不透明度（0-255）。</summary>
        void SetLayeredAlpha(IntPtr hwnd, byte alpha);

        /// <summary>移动/缩放窗口（物理像素）。</summary>
        void SetWindowBounds(IntPtr hwnd, Rectangle bounds);

        /// <summary>查询窗口矩形（物理像素）。</summary>
        Rectangle GetWindowBounds(IntPtr hwnd);

        /// <summary>前台窗口信息；无前台窗口返回 null。</summary>
        WindowInfo GetForegroundWindow();

        /// <summary>按过滤器枚举顶层窗口。</summary>
        IReadOnlyList<WindowInfo> EnumTopLevelWindows(Func<WindowInfo, bool> filter);

        /// <summary>窗口所在可执行文件路径（可能为空串）。</summary>
        string GetWindowModuleFileName(IntPtr hwnd);
    }

    /// <summary>系统托盘（原 NotifyIcon 的抽象）。</summary>
    public interface ITrayService : IDisposable
    {
        void Initialize(TrayIconSpec spec);

        void ShowBalloon(string title, string message);

        void SetToolTip(string text);
    }

    /// <summary>开机自启（原 AutoStartup 注册表逻辑的抽象）。</summary>
    public interface IAutoStartService
    {
        void SetEnabled(bool enabled);

        bool IsEnabled { get; }
    }

    /// <summary>剪贴板（原 WinForms Clipboard 图像/文本的抽象，GDI Bitmap 进出）。</summary>
    public interface IClipboardService
    {
        void SetImage(Bitmap image);

        Bitmap GetImage();

        void SetText(string text);

        string GetText();

        bool ContainsImage { get; }

        bool ContainsText { get; }
    }

    /// <summary>平台服务聚合门面。由入口程序在 UI 框架启动前按 OS 装配。</summary>
    public interface IPlatformService
    {
        IHotkeyService Hotkey { get; }

        IScreenCaptureService ScreenCapture { get; }

        IWindowService Window { get; }

        ITrayService Tray { get; }

        IAutoStartService AutoStart { get; }

        IClipboardService Clipboard { get; }
    }
}
