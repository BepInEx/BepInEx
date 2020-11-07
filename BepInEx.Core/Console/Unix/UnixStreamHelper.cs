using System;
using System.Collections.Generic;
using System.IO;
using MonoMod.Utils;

namespace BepInEx.Unix
{
	internal static class UnixStreamHelper
	{
		public delegate int dupDelegate(int fd);
		[DynDllImport("libc")]
		public static dupDelegate dup;

		public delegate IntPtr fdopenDelegate(int fd, string mode);
		[DynDllImport("libc")]
		public static fdopenDelegate fdopen;

		public delegate IntPtr freadDelegate(IntPtr ptr, IntPtr size, IntPtr nmemb, IntPtr stream);
		[DynDllImport("libc")]
		public static freadDelegate fread;

		public delegate int fwriteDelegate(IntPtr ptr, IntPtr size, IntPtr nmemb, IntPtr stream);
		[DynDllImport("libc")]
		public static fwriteDelegate fwrite;

		public delegate int fcloseDelegate(IntPtr stream);
		[DynDllImport("libc")]
		public static fcloseDelegate fclose;

		public delegate int fflushDelegate(IntPtr stream);
		[DynDllImport("libc")]
		public static fflushDelegate fflush;

		public delegate int isattyDelegate(int fd);
		[DynDllImport("libc")]
		public static isattyDelegate isatty;

		static UnixStreamHelper()
		{
			var libcMapping = new Dictionary<string, List<DynDllMapping>>
			{
				["libc"] = new List<DynDllMapping>
				{
					"libc.so.6", // Ubuntu glibc
					"libc", // Linux glibc
					"/usr/lib/libSystem.dylib", // OSX POSIX
				}
			};

			typeof(UnixStreamHelper).ResolveDynDllImports(libcMapping);
		}

		public static Stream CreateDuplicateStream(int fileDescriptor)
		{
			int newFd = dup(fileDescriptor);

			return new UnixStream(newFd, FileAccess.Write);
		}
	}
}