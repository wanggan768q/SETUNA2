using System;

namespace SETUNA.Main.Style
{
    /// <summary>
    /// 宿主命令回调：托盘/右键菜单中的宿主级动作（截图/设置/退出）。
    /// WinForms 宿主启动时注册到 Mainform；Avalonia 宿主注册到对应控制器。
    /// </summary>
    public static class HostCommands
    {
        /// <summary>开始截屏（原 Mainform.StartCapture）。</summary>
        public static Action RequestCapture;

        /// <summary>打开设置（原 Mainform.Option）。</summary>
        public static Action RequestOption;

        /// <summary>退出应用（原 Mainform.Close）。</summary>
        public static Action RequestShutdown;

        /// <summary>挂起层级刷新（原 LayerManager.SuspendRefresh）。</summary>
        public static Action SuspendLayerRefresh;

        /// <summary>恢复层级刷新（原 LayerManager.ResumeRefresh）。</summary>
        public static Action ResumeLayerRefresh;
    }
}
