using System;

namespace SETUNA.Main
{
    /// <summary>贴图事件委托（对齐原 ScrapBase.ScrapEventHandler / ScrapSubMenuHandler 签名）。</summary>
    public delegate void ScrapEventHandler(object sender, ScrapEventArgs e);

    public delegate void ScrapSubMenuHandler(object sender, ScrapMenuArgs e);
}
