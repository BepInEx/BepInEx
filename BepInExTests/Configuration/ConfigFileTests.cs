extern alias UEC;
using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UEC::UnityEngine;

namespace BepInEx.Configuration.Tests
{
    [TestClass]
    public class ConfigFileTests
    {
        [TestMethod]
        public void AutoSaveTest()
        {
            var (c, _) = LegacyTestConfigProvider.MakeConfig();
            c.Bind("Cat", "Key", 0, new ConfigDescription("Test"));

            var eventFired = new AutoResetEvent(false);
            c.ConfigReloaded += (sender, args) => eventFired.Set();

            c.Save();

            Assert.IsFalse(eventFired.WaitOne(200));
        }

        [TestMethod]
        public void FileWatchTestNoSelfReload()
        {
            var (c, _) = LegacyTestConfigProvider.MakeConfig();

            var eventFired = new AutoResetEvent(false);
            c.ConfigReloaded += (sender, args) => eventFired.Set();

            c.Save();

            Assert.IsFalse(eventFired.WaitOne(200));
        }

        [TestMethod]
        public void ValueRangeTest()
        {
            var (c, _) = LegacyTestConfigProvider.MakeConfig();
            var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test", new AcceptableValueRange<int>(0, 2)));

            Assert.AreEqual(0, w.Value);
            w.Value = 2;
            Assert.AreEqual(2, w.Value);
            w.Value = -2;
            Assert.AreEqual(0, w.Value);
            w.Value = 4;
            Assert.AreEqual(2, w.Value);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ValueRangeBadTypeTest()
        {
            var (c, _) = LegacyTestConfigProvider.MakeConfig();
            c.Bind("Cat", "Key", 0, new ConfigDescription("Test", new AcceptableValueRange<float>(1, 2)));
            Assert.Fail();
        }

        [TestMethod]
        public void ValueRangeDefaultTest()
        {
            var (c, _) = LegacyTestConfigProvider.MakeConfig();
            var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test", new AcceptableValueRange<int>(1, 2)));

            Assert.AreEqual(w.Value, 1);
        }

        [TestMethod]
        public void ValueListTest()
        {
            var (c, _) = LegacyTestConfigProvider.MakeConfig();
            var w = c.Bind("Cat", "Key", "kek",
                           new ConfigDescription("Test", new AcceptableValueList<string>("lel", "kek", "wew", "why")));

            Assert.AreEqual("kek", w.Value);
            w.Value = "wew";
            Assert.AreEqual("wew", w.Value);
            w.Value = "no";
            Assert.AreEqual("lel", w.Value);
            w.Value = null;
            Assert.AreEqual("lel", w.Value);
        }

        [TestMethod]
        public void KeyboardShortcutTest()
        {
            var shortcut = new KeyboardShortcut(KeyCode.H, KeyCode.O, KeyCode.R, KeyCode.S, KeyCode.E, KeyCode.Y);
            var s = shortcut.Serialize();
            var d = KeyboardShortcut.Deserialize(s);
            Assert.AreEqual(shortcut, d);

            var (c, _) = LegacyTestConfigProvider.MakeConfig();
            var w = c.Bind("Cat", "Key", new KeyboardShortcut(KeyCode.A, KeyCode.LeftShift));
            Assert.AreEqual(new KeyboardShortcut(KeyCode.A, KeyCode.LeftShift), w.Value);

            w.Value = shortcut;
            c.Reload();
            Assert.AreEqual(shortcut, w.Value);
        }

        [TestMethod]
        public void KeyboardShortcutTest2()
        {
            Assert.AreEqual(KeyboardShortcut.Empty, new KeyboardShortcut());

            var (c, _) = LegacyTestConfigProvider.MakeConfig();

            var w = c.Bind("Cat", "Key", KeyboardShortcut.Empty, new ConfigDescription("Test"));

            Assert.AreEqual("", w.GetSerializedValue());

            w.SetSerializedValue(w.GetSerializedValue());
            Assert.AreEqual(KeyboardShortcut.Empty, w.Value);

            var testShortcut = new KeyboardShortcut(KeyCode.A, KeyCode.B, KeyCode.C);
            w.Value = testShortcut;

            w.SetSerializedValue(w.GetSerializedValue());
            Assert.AreEqual(testShortcut, w.Value);

            c.Save();
            c.Reload();

            Assert.AreEqual(testShortcut, w.Value);
        }
    }
}
