using System;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SETUNA.Main.Cache;
using SETUNA.Main.Option;

namespace SETUNATests
{
    [TestClass]
    [SupportedOSPlatform("windows")]
    public class ClotheslineTests
    {
        private string _originalCachePath;
        private string _tempCacheRoot;

        [TestInitialize]
        public void Setup()
        {
            // 缓存指向临时目录，不触碰真实用户截图
            _originalCachePath = CacheManager.Path;
            _tempCacheRoot = Path.Combine(Path.GetTempPath(), "SETUNATests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempCacheRoot);
            CacheManager.Path = _tempCacheRoot;
        }

        [TestCleanup]
        public void Cleanup()
        {
            CacheManager.Path = _originalCachePath;
            if (Directory.Exists(_tempCacheRoot))
            {
                Directory.Delete(_tempCacheRoot, true);
            }
        }

        /// 按主程序 Mainform.LoadOption/SaveOption 的实际方式序列化/反序列化
        private static SetunaOption RoundTripViaXml(SetunaOption option)
        {
            var allType = SetunaOption.GetAllType();
            using (var stream = new MemoryStream())
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(option.GetType(), allType);
                serializer.Serialize(stream, option);
                stream.Position = 0;
                return (SetunaOption)new System.Xml.Serialization.XmlSerializer(typeof(SetunaOption), allType).Deserialize(stream);
            }
        }

        /// CacheItem.FolderPath 的目录名格式（DateTimeExtensions internal，此处用相同格式）
        private static string FolderName(DateTime createTime)
        {
            return createTime.ToString("yyyy-MM-dd HH-mm-ss-ff");
        }

        [TestMethod]
        public void Deserialize_OldXmlWithoutClotheslineFields_UsesDefaults()
        {
            var oldXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<SetunaOption xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <Difficult>false</Difficult>
  <ScrapHotKeyEnable>false</ScrapHotKeyEnable>
</SetunaOption>";

            var allType = SetunaOption.GetAllType();
            var option = (SetunaOption)new System.Xml.Serialization.XmlSerializer(typeof(SetunaOption), allType)
                .Deserialize(new StringReader(oldXml));

            Assert.IsFalse(option.ClotheslineEnabled);
            Assert.AreEqual(ClotheslinePreviewLocation.MonitorCenter, option.ClotheslinePreviewLocation);
            Assert.IsFalse(option.ClotheslineRopePositionSaved);
        }

        [TestMethod]
        public void RoundTrip_NonDefaultClotheslineFields_ValuesPreserved()
        {
            var option = new SetunaOption
            {
                ClotheslineEnabled = true,
                ClotheslinePreviewLocation = ClotheslinePreviewLocation.NearThumbnail,
                ClotheslineRopePosition = new Point(123, 456),
                ClotheslineRopePositionSaved = true
            };

            var loaded = RoundTripViaXml(option);

            Assert.IsTrue(loaded.ClotheslineEnabled);
            Assert.AreEqual(ClotheslinePreviewLocation.NearThumbnail, loaded.ClotheslinePreviewLocation);
            Assert.AreEqual(new Point(123, 456), loaded.ClotheslineRopePosition);
            Assert.IsTrue(loaded.ClotheslineRopePositionSaved);
        }

        [TestMethod]
        public void Clone_ModifyOriginalNewFields_CloneIsolated()
        {
            var option = new SetunaOption
            {
                ClotheslineEnabled = true,
                ClotheslinePreviewLocation = ClotheslinePreviewLocation.OriginalLocation,
                ClotheslineRopePosition = new Point(10, 20),
                ClotheslineRopePositionSaved = true
            };

            var clone = (SetunaOption)option.Clone();

            // 初始值一致
            Assert.IsTrue(clone.ClotheslineEnabled);
            Assert.AreEqual(ClotheslinePreviewLocation.OriginalLocation, clone.ClotheslinePreviewLocation);
            Assert.AreEqual(new Point(10, 20), clone.ClotheslineRopePosition);
            Assert.IsTrue(clone.ClotheslineRopePositionSaved);

            // 修改原对象不影响克隆
            option.ClotheslineEnabled = false;
            option.ClotheslinePreviewLocation = ClotheslinePreviewLocation.NearThumbnail;
            option.ClotheslineRopePosition = new Point(99, 99);
            option.ClotheslineRopePositionSaved = false;
            Assert.IsTrue(clone.ClotheslineEnabled);
            Assert.AreEqual(ClotheslinePreviewLocation.OriginalLocation, clone.ClotheslinePreviewLocation);
            Assert.AreEqual(new Point(10, 20), clone.ClotheslineRopePosition);
            Assert.IsTrue(clone.ClotheslineRopePositionSaved);

            // 反向：修改克隆不影响原对象
            clone.ClotheslineEnabled = true;
            clone.ClotheslineRopePosition = new Point(77, 88);
            Assert.IsFalse(option.ClotheslineEnabled);
            Assert.AreEqual(new Point(99, 99), option.ClotheslineRopePosition);
        }

        [TestMethod]
        public void CacheItem_SaveInfoRead_WithTrueFalseNull_Preserved()
        {
            var createTime = new DateTime(2026, 1, 1, 12, 0, 0);
            var item = new CacheItemAccessForTest
            {
                CreateTime = createTime,
                Position = new Point(5, 6),
                ClotheslineHanging = true
            };
            item.SaveInfo();

            var loaded = CacheItem.Read(Path.Combine(CacheManager.Path, FolderName(createTime)));
            Assert.IsTrue(loaded.ClotheslineHanging.Value);

            // false 保持 false
            var itemFalse = new CacheItemAccessForTest
            {
                CreateTime = createTime,
                Position = new Point(5, 6),
                ClotheslineHanging = false
            };
            itemFalse.SaveInfo();
            var loadedFalse = CacheItem.Read(Path.Combine(CacheManager.Path, FolderName(createTime)));
            Assert.IsFalse(loadedFalse.ClotheslineHanging.Value);

            // null 保持 null
            var itemNull = new CacheItemAccessForTest
            {
                CreateTime = createTime,
                Position = new Point(5, 6),
                ClotheslineHanging = null
            };
            itemNull.SaveInfo();
            var loadedNull = CacheItem.Read(Path.Combine(CacheManager.Path, FolderName(createTime)));
            Assert.IsNull(loadedNull.ClotheslineHanging);
        }

        [TestMethod]
        public void CacheItem_ReadOldJsonWithoutClotheslineHanging_IsNull()
        {
            var createTime = new DateTime(2026, 2, 2, 8, 30, 0);
            var folder = Path.Combine(CacheManager.Path, FolderName(createTime));
            Directory.CreateDirectory(folder);
            var oldJson = @"{
  ""CreateTime"": """ + createTime.ToString("yyyy-MM-ddTHH:mm:ss") + @""",
  ""Position"": ""1, 2"",
  ""SortingOrder"": 0
}";
            File.WriteAllText(Path.Combine(folder, "Info.json"), oldJson);

            var loaded = CacheItem.Read(folder);
            Assert.IsNotNull(loaded);
            Assert.IsNull(loaded.ClotheslineHanging);
        }

        /// 暴露 protected 构造函数用于测试
        private class CacheItemAccessForTest : CacheItem
        {
        }
    }
}
