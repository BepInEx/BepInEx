using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace BepInEx
{
	/// <summary>
	/// A helper class to handle persistent data.
	/// </summary>
	public static class Config
	{
		private static readonly Dictionary<string, Dictionary<string, string>> cache = new Dictionary<string, Dictionary<string, string>>();

		private static string ConfigPath => Path.Combine(Paths.BepInExRootPath, "config.ini");

		private static readonly Regex sanitizeKeyRegex = new Regex(@"[^a-zA-Z0-9\-\.]+");

		private static void RaiseConfigReloaded()
		{
			ConfigReloaded?.Invoke();
		}

		/// <summary>
		/// An event that is fired every time the config is reloaded.
		/// </summary>
		public static event Action ConfigReloaded;

		/// <summary>
		/// If enabled, writes the config to disk every time a value is set.
		/// </summary>
		public static bool SaveOnConfigSet { get; set; } = true;

		static Config()
		{
			if (File.Exists(ConfigPath))
			{
				ReloadConfig();
			}
			else
			{
				SaveConfig();
			}
		}

		/// <summary>
		/// Returns the value of the key if found, otherwise returns the default value.
		/// </summary>
		/// <param name="key">The key to search for.</param>
		/// <param name="defaultValue">The default value to return if the key is not found.</param>
		/// <param name="section">The section of the config to search the key for.</param>
		/// <returns>The value of the key.</returns>
		public static string GetEntry(string key, string defaultValue = "", string section = "")
		{
			try
			{
				key = Sanitize(key);
				section = section.IsNullOrWhiteSpace() ? "Global" : Sanitize(section);

				if (!cache.TryGetValue(section, out Dictionary<string, string> subdict))
				{
					SetEntry(key, defaultValue, section);
					return defaultValue;
				}

				if (subdict.TryGetValue(key, out string value))
					return value;

				SetEntry(key, defaultValue, section);
				return defaultValue;
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error | LogLevel.Message, "Unable to read config entry!");
				Logger.LogError(ex);
				return defaultValue;
			}
		}

		/// <summary>
		/// Reloads the config from disk. Unwritten changes are lost.
		/// </summary>
		public static void ReloadConfig()
		{
			cache.Clear();

			string currentSection = "";

			foreach (string rawLine in File.ReadAllLines(ConfigPath))
			{
				string line = rawLine.Trim();

				bool commentIndex = line.StartsWith(";") || line.StartsWith("#");

				if (commentIndex) //trim comment
					continue;

				if (line.StartsWith("[") && line.EndsWith("]")) //section
				{
					currentSection = line.Substring(1, line.Length - 2);
					continue;
				}

				string[] split = line.Split('='); //actual config line
				if (split.Length != 2)
					continue; //empty/invalid line

				if (!cache.ContainsKey(currentSection))
					cache[currentSection] = new Dictionary<string, string>();

				cache[currentSection][split[0]] = split[1];
			}

			RaiseConfigReloaded();
		}

		/// <summary>
		/// Writes the config to disk.
		/// </summary>
		public static void SaveConfig()
		{
			using (StreamWriter writer = new StreamWriter(File.Create(ConfigPath), System.Text.Encoding.UTF8))
				foreach (var sectionKv in cache)
				{
					writer.WriteLine($"[{sectionKv.Key}]");

					foreach (var entryKv in sectionKv.Value)
						writer.WriteLine($"{entryKv.Key}={entryKv.Value}");

					writer.WriteLine();
				}
		}

		/// <summary>
		/// Sets the value of the key in the config.
		/// </summary>
		/// <param name="key">The key to set the value to.</param>
		/// <param name="value">The value to set.</param>
		public static void SetEntry(string key, string value, string section = "")
		{
			try
			{
				key = Sanitize(key);
				section = section.IsNullOrWhiteSpace() ? "Global" : Sanitize(section);

				if (!cache.TryGetValue(section, out Dictionary<string, string> subdict))
				{
					subdict = new Dictionary<string, string>();
					cache[section] = subdict;
				}

				subdict[key] = value;

				if (SaveOnConfigSet)
					SaveConfig();
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error | LogLevel.Message, "Unable to save config entry!");
				Logger.LogError(ex);
			}
		}

		/// <summary>
		/// Returns whether a value is currently set.
		/// </summary>
		/// <param name="key">The key to check against</param>
		/// <param name="section">The section to check in</param>
		/// <returns>True if the key is present</returns>
		public static bool HasEntry(string key, string section = "")
		{
			key = Sanitize(key);
			section = section.IsNullOrWhiteSpace() ? "Global" : Sanitize(section);

			return cache.ContainsKey(section) && cache[section].ContainsKey(key);
		}


		/// <summary>
		/// Removes a value from the config.
		/// </summary>
		/// <param name="key">The key to remove</param>
		/// <param name="section">The section to remove from</param>
		/// <returns>True if the key was removed</returns>
		public static bool UnsetEntry(string key, string section = "")
		{
			key = Sanitize(key);
			section = section.IsNullOrWhiteSpace() ? "Global" : Sanitize(section);

			if (!HasEntry(key, section))
				return false;

			cache[section].Remove(key);
			return true;
		}

		/// <summary>
		/// Replaces any potentially breaking input with underscores.
		/// </summary>
		/// <param name="text">The text to sanitize.</param>
		/// <returns>Sanitized text.</returns>
		public static string Sanitize(string text)
		{
			return sanitizeKeyRegex.Replace(text, "_");
		}

		#region Extensions

		public static string GetEntry(this BaseUnityPlugin plugin, string key, string defaultValue = "")
		{
			return GetEntry(key, defaultValue, MetadataHelper.GetMetadata(plugin).GUID);
		}

		public static void SetEntry(this BaseUnityPlugin plugin, string key, string value)
		{
			SetEntry(key, value, MetadataHelper.GetMetadata(plugin).GUID);
		}

		public static bool HasEntry(this BaseUnityPlugin plugin, string key)
		{
			return HasEntry(key, MetadataHelper.GetMetadata(plugin).GUID);
		}

		#endregion Extensions
	}
}