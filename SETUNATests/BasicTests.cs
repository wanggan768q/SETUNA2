using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SETUNATests
{
    [TestClass]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class BasicTests
    {
        [TestMethod]
        public void BasicTest_ShouldPass()
        {
            // 简单的基础测试，确保测试框架正常工作
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void VersionTest_ShouldHaveValidVersion()
        {
            // 测试程序集版本信息
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            
            Assert.IsNotNull(version);
            Assert.IsTrue(version.Major >= 1);
        }

        [TestMethod]
        public void SETUNAAssembly_ShouldExist()
        {
            // 测试主项目程序集是否可以被加载
            try
            {
                var setunaAssembly = System.Reflection.Assembly.LoadFrom("SETUNA.dll");
                Assert.IsNotNull(setunaAssembly);
                Assert.AreEqual("SETUNA", setunaAssembly.GetName().Name);
            }
            catch (Exception ex)
            {
                Assert.Fail($"无法加载 SETUNA 程序集: {ex.Message}");
            }
        }
    }
}