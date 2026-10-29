using System;
using System.Windows.Forms;
using Avalonia;
using com.clearunit;

namespace SETUNA.Avalonia
{
    internal static class Program
    {
        // 单实例互斥体（进程生命周期持有）
        private static Mutex _singleInstanceMutex;

        [STAThread]
        public static void Main(string[] args)
        {
            // 平台服务装配：必须在任何 UI / 业务代码触达平台能力之前
            using var platform = new SETUNA.Platform.Windows.WindowsPlatformService();
            SETUNA.Core.Platform.PlatformServices.Register(platform);

            // 单实例（对齐旧 Program：第二实例转发给已运行实例后退出）
            _singleInstanceMutex = new Mutex(true, $"Global\\{System.Windows.Forms.Application.ProductName}", out var firstInstance);
            if (!firstInstance)
            {
                SingletonIpcChannel.SendStartup(System.Windows.Forms.Application.ProductVersion, args);
                return;
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
