using System;

namespace com.clearunit
{
    // Token: 0x02000088 RID: 136
    internal class SingletonAppRemoteObject
    {
        // Token: 0x06000473 RID: 1139 RVA: 0x0001CC98 File Offset: 0x0001AE98
        public void Startup(string version, string[] args)
        {
            if (Event != null)
            {
                Event(version, args);
            }
        }

        // Token: 0x040002B3 RID: 691
        public static StartupDelegate Event;

        // Token: 0x02000089 RID: 137
        // (Invoke) Token: 0x06000476 RID: 1142
        public delegate void StartupDelegate(string version, string[] args);
    }
}
