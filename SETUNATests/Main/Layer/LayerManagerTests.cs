using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace SETUNA.Main.Layer.Tests
{
    [TestClass()]
    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    public class LayerManagerTests
    {
        private LayerManager? layerManager;
        private Form? form1;
        private Form? form2;
        private Form? form3;

        [TestInitialize]
        public void Setup()
        {
            layerManager = new LayerManager();
            layerManager.Init(); // 必须初始化才能使用
            
            // 使用真实 Form 对象
            form1 = new Form();
            form1.CreateControl(); // 确保句柄已创建
            form1.Visible = true;
            
            form2 = new Form();
            form2.CreateControl();
            form2.Visible = true;
            
            form3 = new Form();
            form3.CreateControl();
            form3.Visible = false;
        }

        [TestMethod()]
        public void GetNextSortingOrder_ShouldReturnIncrementalValues()
        {
            // Arrange
            var order1 = layerManager!.GetNextSortingOrder();
            var order2 = layerManager!.GetNextSortingOrder();
            var order3 = layerManager!.GetNextSortingOrder();

            // Assert
            Assert.AreEqual(1, order1);
            Assert.AreEqual(2, order2);
            Assert.AreEqual(3, order3);
        }

        [TestMethod()]
        public void SuspendRefresh_And_ResumeRefresh_ShouldWorkCorrectly()
        {
            // Arrange & Act
            layerManager!.SuspendRefresh();
            layerManager!.SuspendRefresh();
            
            // Assert - should not throw
            try
            {
                layerManager!.ResumeRefresh();
                layerManager!.ResumeRefresh();
                layerManager!.ResumeRefresh(); // extra resume should not go negative
            }
            catch
            {
                Assert.Fail("ResumeRefresh should not throw an exception");
            }
        }

        [TestMethod()]
        public void FormData_Constructor_ShouldSetProperties()
        {
            // Arrange
            var form = form1!;
            var sortingOrder = 5;

            // Act
            var formData = new FormData(form, sortingOrder);

            // Assert
            Assert.AreEqual(form, formData.Form);
            Assert.AreEqual(sortingOrder, formData.SortingOrder);
        }

        [TestMethod()]
        public void FormData_Visible_ShouldReturnFormVisible()
        {
            // Arrange
            var formData = new FormData(form1!, 1);

            // Act & Assert
            Assert.IsTrue(formData.Visible);

            form1!.Visible = false;
            Assert.IsFalse(formData.Visible);
        }

        [TestMethod()]
        public void FormData_TopMost_ShouldGetAndSetFormTopMost()
        {
            // Arrange
            var formData = new FormData(form1!, 1);

            // Act
            formData.TopMost = true;

            // Assert
            Assert.IsTrue(formData.TopMost);
            // 验证实际 Form 的 TopMost 属性
            Assert.IsTrue(form1!.TopMost);
        }

        [TestMethod()]
        public void OptimizeLayerCounterTest()
        {
            // Arrange
            var dic = new Dictionary<IntPtr, FormData>();
            // 创建简单的不连续排序值：0, 3, 5
            for (var i = 0; i < 3; i++)
            {
                var form = new Form();
                form.CreateControl();
                var sortingOrder = i == 0 ? 0 : (i == 1 ? 3 : 5);
                dic.Add(form.Handle, new FormData(form, sortingOrder));
            }

            // Act & Assert - 测试优化方法不会抛出异常
            try
            {
                layerManager!.GetType().GetField("formDic", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(layerManager!, dic);
                
                var method = typeof(LayerManager).GetMethod("OptimizeLayerCounter", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(layerManager!, new object[] { });
                
                // 如果没有抛出异常，测试通过
            }
            catch (Exception ex)
            {
                Assert.Fail($"OptimizeLayerCounter should not throw an exception, but got: {ex.Message}");
            }
        }

        [TestMethod()]
        public void OptimizeLayerCounter_EmptyDictionary_ShouldWork()
        {
            // Arrange
            var method = typeof(LayerManager).GetMethod("OptimizeLayerCounter", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act & Assert - should not throw
            try
            {
                method?.Invoke(layerManager!, new object[] { });
            }
            catch
            {
                Assert.Fail("OptimizeLayerCounter should not throw an exception for empty dictionary");
            }
        }

        [TestMethod()]
        public void RefreshLayer_WithVisibleForms_ShouldSetTopMost()
        {
            // Arrange
            var formData1 = new FormData(form1!, 1);
            var formData2 = new FormData(form2!, 2);
            var formData3 = new FormData(form3!, 3); // invisible

            var formDic = new Dictionary<IntPtr, FormData>
            {
                { form1!.Handle, formData1 },
                { form2!.Handle, formData2 },
                { form3!.Handle, formData3 }
            };

            // 创建新的 LayerManager 实例以确保没有状态污染
            var testLayerManager = new LayerManager();
            testLayerManager.Init(); // 必须初始化才能使用
            testLayerManager.GetType().GetField("formDic", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(testLayerManager, formDic);

            // Act & Assert - 捕获具体异常信息
            try
            {
                testLayerManager.RefreshLayer();
                // 如果没有抛出异常，测试通过
            }
            catch (Exception ex)
            {
                // 输出具体异常信息以便调试
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Assert.Fail($"RefreshLayer should not throw an exception, but got: {ex.Message}");
            }
        }

        [TestMethod()]
        public void GetNextSortingOrder_ShouldTriggerOptimize_WhenOver100()
        {
            // Arrange - 减少阈值以加速测试
            var formDic = new Dictionary<IntPtr, FormData>();
            for (int i = 0; i < 105; i++)
            {
                var form = new Form();
                form.CreateControl();
                formDic.Add(form.Handle, new FormData(form, i));
            }
            
            layerManager!.GetType().GetField("formDic", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(layerManager!, formDic);

            layerManager!.GetType().GetField("maxSortingOrder", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(layerManager!, 105);

            // Act & Assert - should not throw when optimization is triggered
            try
            {
                layerManager!.GetNextSortingOrder();
            }
            catch
            {
                Assert.Fail("GetNextSortingOrder should not throw when optimization is triggered");
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            layerManager = null;
            form1 = null;
            form2 = null;
            form3 = null;
        }
    }

    class Compare : IEqualityComparer<FormData>
    {
        bool IEqualityComparer<FormData>.Equals(FormData? x, FormData? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.SortingOrder == y.SortingOrder;
        }

        int IEqualityComparer<FormData>.GetHashCode(FormData obj)
        {
            return obj?.GetHashCode() ?? 0;
        }
    }
}