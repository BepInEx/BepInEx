// --------------------------------------------------
// UnityInjector - ConsoleMirror.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace UnityInjector.ConsoleUtil
{
    internal class ConsoleMirror : IDisposable
    {
        private readonly MirrorWriter _tWriter;

        public ConsoleMirror(string path)
        {
            try
            {
                var fileStream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                var fileWriter = new StreamWriter(fileStream)
                {
                    AutoFlush = true
                };
                _tWriter = new MirrorWriter(fileWriter, Console.Out);
            }
            catch (Exception e)
            {
                SafeConsole.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Couldn't open file to write: {0}", Path.GetFileName(path));
                Console.WriteLine(e.Message);
                SafeConsole.ForegroundColor = ConsoleColor.Gray;

                return;
            }
            Console.SetOut(_tWriter);

            Console.WriteLine();

            var processName = Process.GetCurrentProcess().ProcessName;
            var now = DateTime.Now;

            Console.WriteLine($" {processName} - {now:dd-MM-yyyy hh:mm:ss} ".PadCenter(79, '='));
            Console.WriteLine();
        }

        public void Dispose()
        {
            var cOld = _tWriter.Console;
            var fOld = _tWriter.File;
            Console.SetOut(cOld);
            if (fOld == null)
                return;
            fOld.Flush();
            fOld.Close();
        }

        private class MirrorWriter : TextWriter
        {
            public TextWriter Console { get; }
            public override Encoding Encoding => File.Encoding;
            public TextWriter File { get; }

            public MirrorWriter(TextWriter file, TextWriter console)
            {
                File = file;
                Console = console;
            }

            public override void Flush()
            {
                File.Flush();
                Console.Flush();
            }

            public override void Write(char value)
            {
                File.Write(value);
                Console.Write(value);
            }
        }
    }
}
