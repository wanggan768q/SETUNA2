using System;
using System.Collections.Generic;

namespace SETUNA.Core.Platform
{
    /// <summary>全局热键的描述（替代原 SetunaOption 中散落的修饰键+虚拟键组合逻辑）。</summary>
    public sealed class HotkeySpec
    {
        /// <summary>修饰键位掩码：1=Alt, 2=Ctrl, 4=Shift（与 Win32 MOD_* 常量对齐）。</summary>
        public int Modifiers { get; }

        /// <summary>虚拟键码（Win32 VK_*）。</summary>
        public int VirtualKey { get; }

        public HotkeySpec(int modifiers, int virtualKey)
        {
            Modifiers = modifiers;
            VirtualKey = virtualKey;
        }
    }

    /// <summary>显示器信息（跨平台抽象，对应 WinForms Screen）。</summary>
    public sealed class MonitorInfo
    {
        /// <summary>整个显示器的边界（物理像素）。</summary>
        public Rectangle Bounds { get; set; }

        /// <summary>去掉任务栏等后的可用区域（物理像素）。</summary>
        public Rectangle WorkingArea { get; set; }

        /// <summary>DPI 缩放因子（1.0 = 96 DPI）。</summary>
        public double Scaling { get; set; }

        /// <summary>是否主显示器。</summary>
        public bool IsPrimary { get; set; }

        /// <summary>系统报告的显示器名称（可能为 null）。</summary>
        public string DisplayName { get; set; }
    }

    /// <summary>枚举到的顶层窗口信息（对应 WindowManager/WindowsFilter 的需求）。</summary>
    public sealed class WindowInfo
    {
        public IntPtr Handle { get; set; }

        /// <summary>窗口标题（GetWindowText）。</summary>
        public string Title { get; set; }

        /// <summary>窗口类名（GetClassName）。</summary>
        public string ClassName { get; set; }

        /// <summary>窗口所在可执行文件完整路径（可能为空）。</summary>
        public string ModuleFileName { get; set; }

        /// <summary>窗口矩形（物理像素，GetWindowRect）。</summary>
        public Rectangle Bounds { get; set; }

        public bool IsVisible { get; set; }
    }

    /// <summary>托盘图标初始化参数。</summary>
    public sealed class TrayIconSpec
    {
        /// <summary>托盘图标（ico 文件路径或流来源由实现决定；Avalonia TrayIcon 需要 WindowIcon）。</summary>
        public byte[] IconData { get; set; }

        public string ToolTipText { get; set; }

        /// <summary>托盘菜单项模型（树状，实现负责映射为 NativeMenu）。</summary>
        public IReadOnlyList<TrayMenuItem> MenuItems { get; set; } = Array.Empty<TrayMenuItem>();

        /// <summary>左键单击托盘图标（可能为 null 表示无动作）。</summary>
        public Action ClickAction { get; set; }
    }

    /// <summary>托盘菜单项（扁平描述 + 子级，实现负责递归映射）。</summary>
    public sealed class TrayMenuItem
    {
        public string Header { get; set; }

        public Action ClickAction { get; set; }

        public bool IsSeparator { get; set; }

        public bool IsEnabled { get; set; } = true;

        public IReadOnlyList<TrayMenuItem> Items { get; set; } = Array.Empty<TrayMenuItem>();
    }
}
