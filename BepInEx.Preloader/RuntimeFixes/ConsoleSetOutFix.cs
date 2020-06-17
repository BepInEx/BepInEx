using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;

namespace BepInEx.Preloader.RuntimeFixes
{
	internal static class ConsoleSetOutFix
	{
		private static AggregatedTextWriter aggregatedTextWriter;

		public static void Apply()
		{
			aggregatedTextWriter = new AggregatedTextWriter(Console.Out);
			Console.SetOut(aggregatedTextWriter);
			HarmonyLib.Harmony.CreateAndPatchAll(typeof(ConsoleSetOutFix));
		}

		[HarmonyPatch(typeof(Console), nameof(Console.SetOut))]
		[HarmonyPrefix]
		private static bool OnSetOut(TextWriter newOut)
		{
			aggregatedTextWriter.Add(newOut);
			return false;
		}
	}

	internal class AggregatedTextWriter : TextWriter
	{
		public override Encoding Encoding { get; } = Encoding.UTF8;

		private List<TextWriter> writers = new List<TextWriter>();

		public AggregatedTextWriter(params TextWriter[] initialWriters)
		{
			writers.AddRange(initialWriters.Where(w => w != null));
		}

		public void Add(TextWriter tw)
		{
			if (writers.Any(t => t == tw))
				return;
			writers.Add(tw);
		}

		public override void Flush() => writers.ForEach(w => w.Flush());

		public override void Write(object value) => writers.ForEach(w => w.Write(value));
		public override void Write(string value) => writers.ForEach(w => w.Write(value));
		public override void Write(char value) => writers.ForEach(w => w.Write(value));

		public override void WriteLine(object value) => writers.ForEach(w => w.WriteLine(value));
		public override void WriteLine(string value) => writers.ForEach(w => w.WriteLine(value));
		public override void WriteLine(char value) => writers.ForEach(w => w.WriteLine(value));
	}
}