using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BepInEx.Configuration.Tests
{
    [TestClass]
    public class LegacyConfigTests
    {
        [TestMethod]
        public void ReadTest()
        {
            var (c, _) = LegacyTestConfigProvider.MakeConfig(@"
[Cat]
# Test
Key=1
");
            c.Reload();
            var w = c.Bind("Cat", "Key", 0, "Test");
            Assert.AreEqual(w.Value, 1);
            var w2 = c.Bind("Cat", "Key2", 0, new ConfigDescription("Test"));
            Assert.AreEqual(w2.Value, 0);
        }

        [TestMethod]
        public void ReadTest2()
        {
            var (c, provider) = LegacyTestConfigProvider.MakeConfig();
            var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test"));
            Assert.AreEqual(w.Value, 0);

            provider.FileContents = @"
[Cat]
# Test
Key=1
";
            
            c.Reload();
            Assert.AreEqual(w.Value, 1);
        }

        [TestMethod]
        public void EventTestWrapper()
        {
            var (c, p) = LegacyTestConfigProvider.MakeConfig();
            var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test"));

            p.FileContents = @"
[Cat]
# Test
Key=1
";

            var eventFired = false;
            w.SettingChanged += (sender, args) => eventFired = true;

            c.Reload();

            Assert.IsTrue(eventFired);
        }

        [TestMethod]
        public void EventTestReload()
        {
            var (c, p) = LegacyTestConfigProvider.MakeConfig();
            var eventFired = false;

            var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test"));
            w.SettingChanged += (sender, args) => eventFired = true;

            Assert.IsFalse(eventFired);

            p.FileContents = @"
[Cat]
# Test
Key=1
";
            c.Reload();

            Assert.IsTrue(eventFired);
        }

        [TestMethod]
        public void StringEscapeChars()
        {
            const string testVal = "new line\n test \t\0";

            var (c, _) = LegacyTestConfigProvider.MakeConfig();
            var w = c.Bind("Cat", "Key", testVal, new ConfigDescription("Test"));

            Assert.AreEqual(testVal, w.Value);
            Assert.IsFalse(w.GetSerializedValue().Any(x => x == '\n'));

            w.SetSerializedValue(w.GetSerializedValue());
            Assert.AreEqual(testVal, w.Value);

            c.Save();
            c.Reload();

            Assert.AreEqual(testVal, w.Value);
        }

        [TestMethod]
        public void UnescapedPathString()
        {
            var (c, p) = LegacyTestConfigProvider.MakeConfig();
            var w = c.Bind("Cat", "Key", "", new ConfigDescription("Test"));

            var unescaped = @"D:\test\p ath";
            foreach (var testVal in new[] { unescaped, @"D:\\test\\p ath" })
            {
                p.FileContents = $@"
[Cat]
# Test
Key={testVal}
";
                c.Reload();

                Assert.AreEqual(unescaped, w.Value);

                w.SetSerializedValue(w.GetSerializedValue());
                Assert.AreEqual(unescaped, w.Value);

                c.Save();
                c.Reload();

                Assert.AreEqual(unescaped, w.Value);
            }
        }
        
        [TestMethod]
        public void ValueRangeLoadTest()
        {
            var (c, p) = LegacyTestConfigProvider.MakeConfig();

            p.FileContents = @"
[Cat]
Key = 1
";
            c.Reload();

            var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test", new AcceptableValueRange<int>(0, 2)));

            Assert.AreEqual(w.Value, 1);

            p.FileContents = @"
[Cat]
Key = 5
";
            c.Reload();

            Assert.AreEqual(w.Value, 2);
        }
    }
}
