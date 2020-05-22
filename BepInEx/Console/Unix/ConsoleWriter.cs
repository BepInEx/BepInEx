using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace BepInEx.Unix
{
	public static class ConsoleWriter
	{
		private static ConstructorInfo cStreamWriterConstructor = AccessTools.Constructor(AccessTools.TypeByName("System.IO.CStreamWriter"), new []{ typeof(Stream), typeof(Encoding), typeof(bool) });
		public static TextWriter CreateConsoleStreamWriter(Stream stream, Encoding encoding, bool leaveOpen)
		{
			var writer = (StreamWriter)cStreamWriterConstructor.Invoke(new object[] { stream, encoding, leaveOpen, });
			
			writer.AutoFlush = true;

			return writer;
		}
	}
}