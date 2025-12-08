using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SETUNA.Main.Layer;

namespace SETUNA.Main.Layer.Tests
{
    [TestClass()]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class LayerManagerTests
    {
        private LayerManager? layerManager;

        [TestInitialize]
        public void Setup()
        {
            // 创建独立的 LayerManager 实例用于测试
            layerManager = new LayerManager();
        }

        [TestMethod()]
        public void LayerManager_Instance_ShouldNotBeNull()
        {
            // 测试静态实例是否可用
            var instance = LayerManager.Instance;
            Assert.IsNotNull(instance);
        }

        [TestMethod()]
        public void FormData_Constructor_ShouldSetProperties()
        {
            // Arrange - 使用简单的 Form 对象进行基础测试
            var form = new Form();
            var sortingOrder = 5;

            // Act
            var formData = new FormData(form, sortingOrder);

            // Assert
            Assert.AreEqual(form, formData.Form);
            Assert.AreEqual(sortingOrder, formData.SortingOrder);
        }

        [TestMethod()]
        public void FormData_TopMostProperty_ShouldWork()
        {
            // Arrange
            var form = new Form();
            var formData = new FormData(form, 1);

            // Act & Assert
            formData.TopMost = true;
            Assert.IsTrue(formData.TopMost);
            
            formData.TopMost = false;
            Assert.IsFalse(formData.TopMost);
        }

        [TestMethod()]
        public void LayerManager_ShouldCreateInstance()
        {
            // 测试能够成功创建 LayerManager 实例
            Assert.IsNotNull(layerManager);
        }

        [TestMethod()]
        public void FormData_VisibleProperty_ShouldReflectFormVisibility()
        {
            // Arrange
            var form = new Form();
            var formData = new FormData(form, 1);

            // Act
            form.Visible = true;

            // Assert
            Assert.IsTrue(formData.Visible);

            // Act
            form.Visible = false;

            // Assert
            Assert.IsFalse(formData.Visible);
        }

        [TestCleanup]
        public void Cleanup()
        {
            layerManager = null;
        }
    }
}