using System;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SETUNA.Main.Clothesline;

namespace SETUNATests
{
    /// <summary>边缘呼出决策纯函数测试（spec 测试清单全覆盖）。</summary>
    [TestClass]
    public class ClotheslineEdgeRevealTests
    {
        private static readonly TimeSpan Away300 = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan Away100 = TimeSpan.FromMilliseconds(100);

        [TestMethod]
        public void Hidden_AtEdgeZone_NoFullscreen_Reveals()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                ropeVisible: false, capturePaused: false, edgeZoneHit: true,
                pointerNearRope: false, fullscreenCovers: false, awayDuration: Away300, withinManualGrace: false);
            Assert.AreEqual(EdgeRevealAction.Reveal, action);
        }

        [TestMethod]
        public void Hidden_AtEdgeZone_FullscreenCovers_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                false, false, true, false, true, Away300, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Hidden_NotAtEdgeZone_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                false, false, false, false, false, Away300, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Visible_WithinManualGrace_None()
        {
            // 手动呼出 3 秒宽限内，即使光标早已离开也不收起
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, false, false, false, Away300, true);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Visible_AtEdgeZone_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, true, false, false, Away300, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Visible_NearRope_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, false, true, false, Away300, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Visible_FullscreenCovers_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, false, false, true, Away300, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void Visible_AwayPastDelay_Retracts()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, false, false, false, Away300, false);
            Assert.AreEqual(EdgeRevealAction.Retract, action);
        }

        [TestMethod]
        public void Visible_AwayBelowDelay_None()
        {
            var action = ClotheslineEdgeReveal.DecideEdgeReveal(
                true, false, false, false, false, Away100, false);
            Assert.AreEqual(EdgeRevealAction.None, action);
        }

        [TestMethod]
        public void CapturePaused_AlwaysNone()
        {
            Assert.AreEqual(EdgeRevealAction.None, ClotheslineEdgeReveal.DecideEdgeReveal(
                false, true, true, false, false, Away300, false));
            Assert.AreEqual(EdgeRevealAction.None, ClotheslineEdgeReveal.DecideEdgeReveal(
                true, true, false, false, false, Away300, false));
        }

        [TestMethod]
        public void EdgeZone_Horizontal_Top4px_WithinWorkX()
        {
            var work = new Rectangle(0, 0, 1920, 1040);
            Assert.IsTrue(ClotheslineEdgeGeometry.IsEdgeZoneHit(new Point(500, 2), work, vertical: false));
            Assert.IsFalse(ClotheslineEdgeGeometry.IsEdgeZoneHit(new Point(500, 10), work, vertical: false));
            // 工作区 X 范围外不算
            Assert.IsFalse(ClotheslineEdgeGeometry.IsEdgeZoneHit(new Point(-5, 2), work, vertical: false));
        }

        [TestMethod]
        public void EdgeZone_Vertical_Right4px_WithinWorkY()
        {
            var work = new Rectangle(0, 0, 1920, 1040);
            Assert.IsTrue(ClotheslineEdgeGeometry.IsEdgeZoneHit(new Point(1918, 500), work, vertical: true));
            Assert.IsFalse(ClotheslineEdgeGeometry.IsEdgeZoneHit(new Point(1900, 500), work, vertical: true));
        }

        [TestMethod]
        public void CoversWorkArea_ContainsOrNot()
        {
            var work = new Rectangle(0, 0, 1920, 1040);
            Assert.IsTrue(ClotheslineEdgeGeometry.CoversWorkArea(new Rectangle(0, 0, 1920, 1080), work));
            Assert.IsFalse(ClotheslineEdgeGeometry.CoversWorkArea(new Rectangle(100, 100, 800, 600), work));
            Assert.IsFalse(ClotheslineEdgeGeometry.CoversWorkArea(Rectangle.Empty, work));
        }
    }
}
