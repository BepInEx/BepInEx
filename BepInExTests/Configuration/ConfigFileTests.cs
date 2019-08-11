using System;
using System.Collections.Concurrent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Threading;

namespace BepInEx.Configuration.Tests
{
	[TestClass]
	public class ConfigFileTests
	{
		private static ConcurrentBag<ConfigFile> _toRemove;

		[ClassInitialize]
		public static void Init(TestContext context)
		{
			_toRemove = new ConcurrentBag<ConfigFile>();
		}

		[ClassCleanup]
		public static void Cleanup()
		{
			foreach (var configFile in _toRemove)
			{
				configFile.StopWatching();
				File.Delete(configFile.ConfigFilePath);
			}
		}

		private static ConfigFile MakeConfig()
		{
			string configPath = Path.GetTempFileName();
			if (configPath == null) throw new InvalidOperationException("Wtf");
			var config = new ConfigFile(configPath, true);
			_toRemove.Add(config);
			return config;
		}

		[TestMethod]
		public void SaveTest()
		{
			MakeConfig().Save();
		}

		[TestMethod]
		public void SaveTestValueChange()
		{
			var c = MakeConfig();

			var w = c.Wrap("Cat", "Key", 0, new ConfigDescription("Test"));
			var lines = File.ReadAllLines(c.ConfigFilePath);
			Assert.AreEqual(0, lines.Count(x => x.Equals("[Cat]")));
			Assert.AreEqual(0, lines.Count(x => x.Equals("# Test")));
			Assert.AreEqual(0, lines.Count(x => x.Equals("Key = 0")));

			c.Save();
			lines = File.ReadAllLines(c.ConfigFilePath);
			Assert.AreEqual(1, lines.Count(x => x.Equals("[Cat]")));
			Assert.AreEqual(1, lines.Count(x => x.Equals("# Test")));
			Assert.AreEqual(1, lines.Count(x => x.Equals("Key = 0")));

			w.Value = 69;
			lines = File.ReadAllLines(c.ConfigFilePath);
			Assert.AreEqual(1, lines.Count(x => x.Equals("[Cat]")));
			Assert.AreEqual(1, lines.Count(x => x.Equals("# Test")));
			Assert.AreEqual(1, lines.Count(x => x.Equals("Key = 69")));
		}

		[TestMethod]
		public void AutoSaveTest()
		{
			var c = MakeConfig();
			c.Wrap("Cat", "Key", 0, new ConfigDescription("Test"));

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
			var w = c.Wrap("Cat", "Key", 0, new ConfigDescription("Test"));
			Assert.AreEqual(w.Value, 1);
			var w2 = c.Wrap("Cat", "Key2", 0, new ConfigDescription("Test"));
			Assert.AreEqual(w2.Value, 0);
		}

		[TestMethod]
		public void ReadTest2()
		{
			var c = MakeConfig();
			var w = c.Wrap("Cat", "Key", 0, new ConfigDescription("Test"));
			Assert.AreEqual(w.Value, 0);

			File.WriteAllText(c.ConfigFilePath, "[Cat]\n# Test\nKey = 1 \n");

			c.Reload();
			Assert.AreEqual(w.Value, 1);
		}

		[TestMethod]
		public void FileWatchTest()
		{
			var c = MakeConfig();
			var w = c.Wrap("Cat", "Key", 0, new ConfigDescription("Test"));
			Assert.AreEqual(w.Value, 0);

			var eventFired = new AutoResetEvent(false);
			w.SettingChanged += (sender, args) => eventFired.Set();

			File.WriteAllText(c.ConfigFilePath, "[Cat]\n# Test\nKey = 1 \n");

			Assert.IsTrue(eventFired.WaitOne(500));

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
			var w = c.Wrap("Cat", "Key", 0, new ConfigDescription("Test"));

			File.WriteAllText(c.ConfigFilePath, "[Cat]\n# Test\nKey=1\n");

			var eventFired = false;
			w.SettingChanged += (sender, args) => eventFired = true;

			c.Reload();

			Assert.IsTrue(eventFired);
		}

		[TestMethod]
		public void EventTestReload()
		{
			var c = MakeConfig();
			var eventFired = false;

			var w = c.Wrap("Cat", "Key", 0, new ConfigDescription("Test"));
			w.SettingChanged += (sender, args) => eventFired = true;

			Assert.IsFalse(eventFired);

			File.WriteAllText(c.ConfigFilePath, "[Cat]\n# Test\nKey=1\n");
			c.Reload();

			Assert.IsTrue(eventFired);
		}
	}
}