// --------------------------------------------------
// UnityInjector - SafeConsole.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.Reflection;

namespace UnityInjector.ConsoleUtil
{
	/// <summary>
	/// Console class with safe handlers for Unity 4.x, which does not have a proper Console implementation
	/// </summary>
	internal static class SafeConsole
	{
		public static bool BackgroundColorExists { get; private set; }

		private static GetColorDelegate _getBackgroundColor;
		private static SetColorDelegate _setBackgroundColor;

		public static ConsoleColor BackgroundColor
		{
			get => _getBackgroundColor();
			set => _setBackgroundColor(value);
		}

		public static bool ForegroundColorExists { get; private set; }

		private static GetColorDelegate _getForegroundColor;
		private static SetColorDelegate _setForegroundColor;

		public static ConsoleColor ForegroundColor
		{
			get => _getForegroundColor();
			set => _setForegroundColor(value);
		}

		public static bool TitleExists { get; private set; }

		private static GetStringDelegate _getTitle;
		private static SetStringDelegate _setTitle;

		public static string Title
		{
			get => _getTitle();
			set => _setTitle(value);
		}

		static SafeConsole()
		{
			var tConsole = typeof(Console);
			InitColors(tConsole);
		}

		private static void InitColors(Type tConsole)
		{
			const BindingFlags BINDING_FLAGS = BindingFlags.Public | BindingFlags.Static;

			var gfc = tConsole.GetMethod("get_ForegroundColor", BINDING_FLAGS);
			var sfc = tConsole.GetMethod("set_ForegroundColor", BINDING_FLAGS);

			var gbc = tConsole.GetMethod("get_BackgroundColor", BINDING_FLAGS);
			var sbc = tConsole.GetMethod("set_BackgroundColor", BINDING_FLAGS);
			
			var gtt = tConsole.GetMethod("get_Title", BINDING_FLAGS);
			var stt = tConsole.GetMethod("set_Title", BINDING_FLAGS);

			_setForegroundColor = sfc != null
				? (SetColorDelegate)Delegate.CreateDelegate(typeof(SetColorDelegate), sfc)
				: (value => { });

			_setBackgroundColor = sbc != null
				? (SetColorDelegate)Delegate.CreateDelegate(typeof(SetColorDelegate), sbc)
				: (value => { });

			_getForegroundColor = gfc != null
				? (GetColorDelegate)Delegate.CreateDelegate(typeof(GetColorDelegate), gfc)
				: (() => ConsoleColor.Gray);

			_getBackgroundColor = gbc != null
				? (GetColorDelegate)Delegate.CreateDelegate(typeof(GetColorDelegate), gbc)
				: (() => ConsoleColor.Black);

			_getTitle = gtt != null
				? (GetStringDelegate)Delegate.CreateDelegate(typeof(GetStringDelegate), gtt)
				: (() => string.Empty);

			_setTitle = stt != null
				? (SetStringDelegate)Delegate.CreateDelegate(typeof(SetStringDelegate), stt)
				: (value => { });

			BackgroundColorExists = _setBackgroundColor != null && _getBackgroundColor != null;
			ForegroundColorExists = _setForegroundColor != null && _getForegroundColor != null;
			TitleExists = _setTitle != null && _getTitle != null;
		}

		private delegate ConsoleColor GetColorDelegate();
		private delegate void SetColorDelegate(ConsoleColor value);

		private delegate string GetStringDelegate();
		private delegate void SetStringDelegate(string value);
	}
}