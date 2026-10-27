using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SETUNA.Main.Clothesline;

namespace SETUNATests
{
    /// <summary>RopeSwingMath 摆动数学纯函数测试（无 UI、无 STA）。</summary>
    [TestClass]
    public class RopeSwingMathTests
    {
        [TestMethod]
        public void SwingAngle_AtZeroElapsed_Index0_IsZero()
        {
            // sin(0 + 0·相位) = 0
            Assert.AreEqual(0.0, RopeSwingMath.SwingAngle(1.2, 0.0, 0), 0.0001);
        }

        [TestMethod]
        public void SwingAngle_NeverExceedsAmplitude()
        {
            for (var t = 0.0; t < 10.0; t += 0.05)
            {
                var angle = RopeSwingMath.SwingAngle(4.0, t, 2);
                Assert.IsTrue(Math.Abs(angle) <= 4.0 + 0.0001, $"t={t} angle={angle}");
            }
        }

        [TestMethod]
        public void SwingAngle_DifferentIndices_HaveDifferentPhase()
        {
            // 同一时刻不同条目相位错开，角度不同（选定采样点避开共同零点）
            var a = RopeSwingMath.SwingAngle(4.0, 0.8, 0);
            var b = RopeSwingMath.SwingAngle(4.0, 0.8, 1);
            Assert.AreNotEqual(a, b, 0.0001);
        }

        [TestMethod]
        public void SwingAngle_PeriodRecurs_AfterOnePeriod()
        {
            var a = RopeSwingMath.SwingAngle(4.0, 1.0, 0);
            var b = RopeSwingMath.SwingAngle(4.0, 1.0 + RopeSwingMath.SwingPeriodSeconds, 0);
            Assert.AreEqual(a, b, 0.0001);
        }

        [TestMethod]
        public void ApproachAmplitude_ConvergesToTarget()
        {
            // 多次逼近后应恰好到达目标
            var amp = 6.0;
            for (var i = 0; i < 1000; i++)
            {
                amp = RopeSwingMath.ApproachAmplitude(amp, 1.2, RopeSwingMath.ApproachStepDeg);
            }
            Assert.AreEqual(1.2, amp, 0.0001);
        }

        [TestMethod]
        public void ApproachAmplitude_MoveDirection_IsCorrect()
        {
            // 低于目标应上升，高于目标应下降
            Assert.IsTrue(RopeSwingMath.ApproachAmplitude(1.0, 4.0, 0.08) > 1.0);
            Assert.IsTrue(RopeSwingMath.ApproachAmplitude(5.0, 1.2, 0.08) < 5.0);
        }

        [TestMethod]
        public void ApproachAmplitude_AlreadyAtTarget_Stays()
        {
            Assert.AreEqual(4.0, RopeSwingMath.ApproachAmplitude(4.0, 4.0, 0.08), 0.0001);
        }
    }
}
