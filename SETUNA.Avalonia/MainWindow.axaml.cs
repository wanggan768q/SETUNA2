using System;
using Avalonia.Controls;

namespace SETUNA.Avalonia
{
    /// <summary>
    /// 主窗体：贴图工具没有主窗口概念，MainWindow 只做托盘宿主与生命周期锚点，
    /// 默认隐藏（对齐旧版 Mainform 隐藏主窗体的行为）。
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Title = "SETUNA";
            Width = 0;
            Height = 0;
            ShowInTaskbar = false;
            // 启动后隐藏，不留可见空窗
            Opened += (s, e) =>
            {
                WindowState = WindowState.Minimized;
                Hide();
            };
        }
    }
}
