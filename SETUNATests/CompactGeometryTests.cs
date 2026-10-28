using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SETUNA.Main.StyleItems;

namespace SETUNATests
{
    /// <summary>CompactGeometry 裁剪几何纯函数测试（对齐旧 CompactScrap 裁剪语义）。</summary>
    [TestClass]
    public class CompactGeometryTests
    {
        [TestMethod]
        public void ClickPointInside_CropsAroundIt()
        {
            // 200×200 图，点 (100,100)：源矩形 = (75,75,50,50)
            var rect = CompactGeometry.CalculateCropRect(new Size(200, 200), new Point(100, 100));
            Assert.AreEqual(new Rectangle(75, 75, 50, 50), rect);
        }

        [TestMethod]
        public void ClickPointNearEdge_GoesNegativeLikeOldVersion()
        {
            // 旧版 DrawImageUnscaled 不 clamp：点 (10,10) → 源矩形左上 (-15,-15)，GDI 越界自然透明
            var rect = CompactGeometry.CalculateCropRect(new Size(200, 200), new Point(10, 10));
            Assert.AreEqual(new Rectangle(-15, -15, 50, 50), rect);
        }

        [TestMethod]
        public void ClickPointEmpty_UsesImageCenter()
        {
            // Empty → 图中心（旧版构造分支）；200×140 中心 = (100,70) → (75,45,50,50)
            var rect = CompactGeometry.CalculateCropRect(new Size(200, 140), Point.Empty);
            Assert.AreEqual(new Rectangle(75, 45, 50, 50), rect);
        }

        [TestMethod]
        public void ImageSmallerThanWindow_StillFullWindowRect()
        {
            // 图小于窗（30×30）：中心 (15,15) → 矩形 (-10,-10,50,50)；越界由绘制透明呈现
            var rect = CompactGeometry.CalculateCropRect(new Size(30, 30), Point.Empty);
            Assert.AreEqual(new Rectangle(-10, -10, 50, 50), rect);
        }
    }
}
