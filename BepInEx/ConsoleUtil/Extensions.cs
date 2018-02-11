// --------------------------------------------------
// UnityInjector - Extensions.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityInjector
{
    internal static class Extensions
    {
        public static string PluginsPath { get; } = Path.Combine(Environment.CurrentDirectory, "UnityInjector");
        public static string UserDataPath { get; } = Path.Combine(PluginsPath, "Config");
        public static string Asciify(this string s) => Regex.Replace(s, @"[^0-9A-Za-z]", "_");
        public static string CombinePaths(params string[] parts) => parts.Aggregate(Path.Combine);

        public static void ForEach<T>(this IEnumerable<T> tees, Action<T> action)
        {
            foreach (var tee in tees)
                action(tee);
        }

        public static string PadCenter(this string str, int totalWidth, char paddingChar = ' ')
        {
            int spaces = totalWidth - str.Length;
            int padLeft = spaces / 2 + str.Length;
            return str.PadLeft(padLeft, paddingChar).PadRight(totalWidth, paddingChar);
        }
    }
}
