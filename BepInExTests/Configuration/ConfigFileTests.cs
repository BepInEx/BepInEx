extern alias UEC;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UEC::UnityEngine;

namespace BepInEx.Configuration.Tests;

[TestClass]
public class ConfigFileTests
{
    private static ConcurrentBag<ConfigFile> _toRemove;

    [ClassInitialize]
    public static void Init(TestContext context) => _toRemove = new ConcurrentBag<ConfigFile>();

    [ClassCleanup]
    public static void Cleanup()
    {
        foreach (var configFile in _toRemove)
            File.Delete(configFile.ConfigFilePath);
    }

    private static ConfigFile MakeConfig()
    {
        var configPath = Path.GetTempFileName();
        if (configPath == null) throw new InvalidOperationException("Wtf");
        var config = new ConfigFile(configPath, true);
        _toRemove.Add(config);
        return config;
    }

    [TestMethod]
    public void SaveTest() => MakeConfig().Save();

    [TestMethod]
    public void SaveTestValueChange()
    {
        var c = MakeConfig();

        var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test"));
        var lines = File.ReadAllLines(c.ConfigFilePath);
        Assert.AreEqual(1, lines.Count(x => x.Equals("[Cat]")));
        Assert.AreEqual(1, lines.Count(x => x.Equals("## Test")));
        Assert.AreEqual(1, lines.Count(x => x.Equals("Key = 0")));

        c.Save();
        lines = File.ReadAllLines(c.ConfigFilePath);
        Assert.AreEqual(1, lines.Count(x => x.Equals("[Cat]")));
        Assert.AreEqual(1, lines.Count(x => x.Equals("## Test")));
        Assert.AreEqual(1, lines.Count(x => x.Equals("Key = 0")));

        w.Value = 69;
        lines = File.ReadAllLines(c.ConfigFilePath);
        Assert.AreEqual(1, lines.Count(x => x.Equals("[Cat]")));
        Assert.AreEqual(1, lines.Count(x => x.Equals("## Test")));
        Assert.AreEqual(1, lines.Count(x => x.Equals("Key = 69")));
    }

    [TestMethod]
    public void AutoSaveTest()
    {
        var c = MakeConfig();
        c.Bind("Cat", "Key", 0, new ConfigDescription("Test"));

        var eventFired = new AutoResetEvent(false);
        c.ConfigReloaded += (sender, args) => eventFired.Set();

        c.Save();

        Assert.IsFalse(eventFired.WaitOne(200));
    }

    [TestMethod]
    public void ReadTest()
    {
        var c = MakeConfig();
        File.WriteAllText(c.ConfigFilePath, "[Cat]\n# Test\nKey=1\n");
        c.Reload();
        var w = c.Bind("Cat", "Key", 0, "Test");
        Assert.AreEqual(w.Value, 1);
        var w2 = c.Bind("Cat", "Key2", 0, new ConfigDescription("Test"));
        Assert.AreEqual(w2.Value, 0);
    }

    [TestMethod]
    public void ReadTest2()
    {
        var c = MakeConfig();
        var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test"));
        Assert.AreEqual(w.Value, 0);

        File.WriteAllText(c.ConfigFilePath, "[Cat]\n# Test\nKey = 1 \n");

        c.Reload();
        Assert.AreEqual(w.Value, 1);
    }

    [TestMethod]
    public void FileWatchTestNoSelfReload()
    {
        var c = MakeConfig();

        var eventFired = new AutoResetEvent(false);
        c.ConfigReloaded += (sender, args) => eventFired.Set();

        c.Save();

        Assert.IsFalse(eventFired.WaitOne(200));
    }

    [TestMethod]
    public void EventTestWrapper()
    {
        var c = MakeConfig();
        var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test"));

        File.WriteAllText(c.ConfigFilePath, "[Cat]\n# Test\nKey=1\n");

        var eventFired = false;
        w.SettingChanged += (sender, args) => eventFired = true;

        c.Reload();

        Assert.IsTrue(eventFired);
    }

    [TestMethod]
    public void PersistHomeless()
    {
        var c = MakeConfig();

        File.WriteAllText(c.ConfigFilePath, "[Cat]\n# Test\nKey=1\nHomeless=0");
        c.Reload();

        var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test"));

        c.Save();

        Assert.IsTrue(File.ReadAllLines(c.ConfigFilePath)
                          .Single(x => x.StartsWith("Homeless") && x.EndsWith("0")) != null);
    }

    [TestMethod]
    public void EventTestReload()
    {
        var c = MakeConfig();
        var eventFired = false;

        var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test"));
        w.SettingChanged += (sender, args) => eventFired = true;

        Assert.IsFalse(eventFired);

        File.WriteAllText(c.ConfigFilePath, "[Cat]\n# Test\nKey=1\n");
        c.Reload();

        Assert.IsTrue(eventFired);
    }

    [TestMethod]
    public void ValueRangeTest()
    {
        var c = MakeConfig();
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
        var c = MakeConfig();
        c.Bind("Cat", "Key", 0, new ConfigDescription("Test", new AcceptableValueRange<float>(1, 2)));
        Assert.Fail();
    }

    [TestMethod]
    public void ValueRangeDefaultTest()
    {
        var c = MakeConfig();
        var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test", new AcceptableValueRange<int>(1, 2)));

        Assert.AreEqual(w.Value, 1);
    }

    [TestMethod]
    public void ValueRangeLoadTest()
    {
        var c = MakeConfig();

        File.WriteAllText(c.ConfigFilePath, "[Cat]\nKey = 1\n");
        c.Reload();

        var w = c.Bind("Cat", "Key", 0, new ConfigDescription("Test", new AcceptableValueRange<int>(0, 2)));

        Assert.AreEqual(w.Value, 1);

        File.WriteAllText(c.ConfigFilePath, "[Cat]\nKey = 5\n");
        c.Reload();

        Assert.AreEqual(w.Value, 2);
    }

    [TestMethod]
    public void ValueListTest()
    {
        var c = MakeConfig();
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

        var c = MakeConfig();
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

        var c = MakeConfig();

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

    [TestMethod]
    public void StringEscapeChars()
    {
        const string testVal = "new line\n test \t\0";

        var c = MakeConfig();
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
        var c = MakeConfig();
        var w = c.Bind("Cat", "Key", "", new ConfigDescription("Test"));

        var unescaped = @"D:\test\p ath";
        foreach (var testVal in new[] { unescaped, @"D:\\test\\p ath" })
        {
            File.WriteAllText(c.ConfigFilePath, $"[Cat]\n# Test\nKey={testVal}\n");
            c.Reload();

            Assert.AreEqual(unescaped, w.Value);

            w.SetSerializedValue(w.GetSerializedValue());
            Assert.AreEqual(unescaped, w.Value);

            c.Save();
            c.Reload();

            Assert.AreEqual(unescaped, w.Value);
        }
    }
}
