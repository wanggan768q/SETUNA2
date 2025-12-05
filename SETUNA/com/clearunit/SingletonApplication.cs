using System;
using System.Threading;
using System.Windows.Forms;

namespace com.clearunit
{
    // Token: 0x02000087 RID: 135
    public class SingletonApplication
    {
        private static Mutex mutex = null;

        // Token: 0x0600046C RID: 1132 RVA: 0x0001CABC File Offset: 0x0001ACBC
        private SingletonApplication()
        {
        }

        // Token: 0x0600046D RID: 1133 RVA: 0x0001CAC4 File Offset: 0x0001ACC4
        public static SingletonApplication GetInstance(string version, string[] args)
        {
            if (SingletonApplication._instance == null)
            {
                lock (SingletonApplication.lockObj)
                {
                    if (SingletonApplication._instance == null)
                    {
                        SingletonApplication._instance = new SingletonApplication();
                    }
                }
            }
            SingletonApplication._instance._args = args;
            SingletonApplication._instance._version = version;
            return SingletonApplication._instance;
        }

        // Token: 0x0600046E RID: 1134 RVA: 0x0001CB38 File Offset: 0x0001AD38
        public void AddSingletonFormListener(ISingletonForm implement)
        {
            // Note: In .NET Core, we use a simpler approach
            SingletonAppRemoteObject.Event = (SingletonAppRemoteObject.StartupDelegate)Delegate.Combine(SingletonAppRemoteObject.Event, new SingletonAppRemoteObject.StartupDelegate(implement.DetectExternalStartup));
        }

        // Token: 0x0600046F RID: 1135 RVA: 0x0001CB5B File Offset: 0x0001AD5B
        public bool Register()
        {
            string mutexName = $"Global\\{Application.ProductName}";
            mutex = new Mutex(true, mutexName, out bool createdNew);
            
            if (createdNew)
            {
                return true; // This is the first instance
            }
            else
            {
                // Another instance is already running, send signal to it
                CreateClient();
                return false;
            }
        }

        // Token: 0x06000471 RID: 1137 RVA: 0x0001CBC8 File Offset: 0x0001ADC8
        private void CreateClient()
        {
            try
            {
                // Signal the existing instance to activate
                if (SingletonAppRemoteObject.Event != null)
                {
                    SingletonAppRemoteObject.Event(_version, _args);
                }
            }
            catch
            {
                Console.WriteLine("CreateClient Error");
            }
        }

        // Token: 0x040002AF RID: 687
        private static volatile SingletonApplication _instance = null;

        // Token: 0x040002B0 RID: 688
        private static object lockObj = new object();

        // Token: 0x040002B1 RID: 689
        private string[] _args;

        // Token: 0x040002B2 RID: 690
        private string _version;
    }
}
