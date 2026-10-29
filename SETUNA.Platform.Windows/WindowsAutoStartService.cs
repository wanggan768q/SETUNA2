using System;
using Microsoft.Win32;
using SETUNA.Core.Platform;

namespace SETUNA.Platform.Windows
{
    /// <summary>开机自启：原 AutoStartup.cs 的注册表逻辑原样搬入（HKCU\...\Run）。</summary>
    public sealed class WindowsAutoStartService : IAutoStartService
    {
        private const string ValueName = "SETUNA_AutoStartup";

        public void SetEnabled(bool enabled)
        {
            using var registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (registryKey == null)
            {
                throw new InvalidOperationException("无法打开自启动注册表项");
            }

            if (enabled)
            {
                registryKey.SetValue(ValueName, Environment.ProcessPath ?? AppContext.BaseDirectory);
            }
            else
            {
                var value = registryKey.GetValue(ValueName, null);
                if (value != null)
                {
                    registryKey.DeleteValue(ValueName);
                }
            }
        }

        public bool IsEnabled
        {
            get
            {
                try
                {
                    using var registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                    if (registryKey == null)
                    {
                        return false;
                    }

                    foreach (var name in registryKey.GetValueNames())
                    {
                        if (name.Equals(ValueName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return false;
                }
            }
        }
    }
}
