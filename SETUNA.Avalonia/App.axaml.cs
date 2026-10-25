using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Avalonia.Markup.Xaml;

namespace SETUNA.Avalonia
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();

                // 宿主装配：配置/贴图集合/热键/托盘/缓存恢复（MainWindow.Hide 后仍为生命周期锚点）
                var host = new AvaloniaHost();
                if (!host.Initialize(desktop.MainWindow))
                {
                    Log("host initialize failed");
                    desktop.Shutdown();
                }
                else
                {
                    // 退出时保存配置 + 停平台服务（对齐旧 Mainform OnClosing → SaveOption）
                    desktop.ShutdownRequested += (s, e) =>
                    {
                        try
                        {
                            host.SaveOption();
                        }
                        catch (Exception ex)
                        {
                            Log($"save on shutdown: {ex.Message}");
                        }
                    };
                    desktop.ShutdownMode = global::Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        internal static void Log(string message)
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "setuna-avalonia-debug.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} [App] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }
    }
}
