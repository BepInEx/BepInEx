// --------------------------------------------------
// UnityInjector - SafeConsole.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.Reflection;

namespace UnityInjector.ConsoleUtil
{
	public static class SafeConsole
	{
		private static GetColorDelegate _getBackgroundColor;
		private static GetColorDelegate _getForegroundColor;
		private static SetColorDelegate _setBackgroundColor;
		private static SetColorDelegate _setForegroundColor;

		public static ConsoleColor BackgroundColor
		{
			get { return _getBackgroundColor(); }
			set { _setBackgroundColor(value); }
		}

		public static ConsoleColor ForegroundColor
		{
			get { return _getForegroundColor(); }
			set { _setForegroundColor(value); }
		}

		static SafeConsole()
		{
			var tConsole = typeof(Console);
			InitColors(tConsole);
		}

		private static void InitColors(Type tConsole)
		{
			const BindingFlags BINDING_FLAGS = BindingFlags.Public | BindingFlags.Static;

			var sfc = tConsole.GetMethod("set_ForegroundColor", BINDING_FLAGS);
			var sbc = tConsole.GetMethod("set_BackgroundColor", BINDING_FLAGS);
			var gfc = tConsole.GetMethod("get_ForegroundColor", BINDING_FLAGS);
			var gbc = tConsole.GetMethod("get_BackgroundColor", BINDING_FLAGS);

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
		}

		private delegate ConsoleColor GetColorDelegate();

		private delegate void SetColorDelegate(ConsoleColor value);
	}
}