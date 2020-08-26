using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace BepInEx.Unix
{
	internal static class ConsoleWriter
	{
		private static Func<Stream, Encoding, bool, StreamWriter> cStreamWriterConstructor;
		
		private static Func<Stream, Encoding, bool, StreamWriter> CStreamWriterConstructor
		{
			get
			{
				if (cStreamWriterConstructor != null)
					return cStreamWriterConstructor;

				var cStreamWriter = AccessTools.TypeByName("System.IO.CStreamWriter");
				Func<Stream, Encoding, bool, StreamWriter> GetCtor(int[] perm)
				{
					var parameters = new[] { typeof(Stream), typeof(Encoding), typeof(bool) };
					var ctor = AccessTools.Constructor(cStreamWriter, perm.Select(i => parameters[i]).ToArray());
					if (ctor != null)
					{
						return (stream, encoding, l) =>
						{
							var vals = new object[] { stream, encoding, l };
							return (StreamWriter)ctor.Invoke(perm.Select(i => vals[i]).ToArray());
						};
					}
					return null;
				}

				var ctorParams = new []
				{
					new[] { 0, 1, 2 }, // Unity 5.x and up
					new[] { 0, 1 }     // Unity 4.7 and older
				};
				
				cStreamWriterConstructor = ctorParams.Select(GetCtor).FirstOrDefault(f => f != null);
				if (cStreamWriterConstructor == null)
					throw new AmbiguousMatchException("Failed to find suitable constructor for CStreamWriter");
				return cStreamWriterConstructor;
			}
		}

		public static TextWriter CreateConsoleStreamWriter(Stream stream, Encoding encoding, bool leaveOpen)
		{
			var writer = CStreamWriterConstructor(stream, encoding, leaveOpen);
			writer.AutoFlush = true;
			return writer;
		}
	}
}