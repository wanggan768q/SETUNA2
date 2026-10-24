using System;

namespace SETUNA.Main
{
    // Token: 0x0200002E RID: 46
    public class ScrapMenuArgs : EventArgs
    {
        // Token: 0x060001AF RID: 431 RVA: 0x000099F3 File Offset: 0x00007BF3
        public ScrapMenuArgs()
        {
        }

        // Token: 0x060001B0 RID: 432 RVA: 0x000099FB File Offset: 0x00007BFB
        public ScrapMenuArgs(IScrap targetscrap, object targetmenu)
        {
            scrap = targetscrap;
            menu = targetmenu;
        }

        // Token: 0x040000C8 RID: 200
        public IScrap scrap;

        // Token: 0x040000C9 RID: 201
        public object menu;
    }
}
