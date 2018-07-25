using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BepInEx.Common
{
    /// <summary>
    /// Generic helper properties and methods.
    /// </summary>
    public static class Utility
    {
        /// <summary>
        /// Combines multiple paths together, as the specfic method is not availble in .NET 3.5.
        /// </summary>
        /// <param name="parts">The multiple paths to combine together.</param>
        /// <returns>A combined path.</returns>
        public static string CombinePaths(params string[] parts) => parts.Aggregate(Path.Combine);

		/// <summary>
		/// Tries to parse a bool, with a default value if unable to parse.
		/// </summary>
		/// <param name="input">The string to parse</param>
		/// <param name="defaultValue">The value to return if parsing is unsuccessful.</param>
		/// <returns>Boolean value of input if able to be parsed, otherwise default value.</returns>
	    public static bool SafeParseBool(string input, bool defaultValue = false)
	    {
		    return bool.TryParse(input, out bool result) ? result : defaultValue;
	    }

        /// <summary>
        /// Converts a file path into a UnityEngine.WWW format.
        /// </summary>
        /// <param name="path">The file path to convert.</param>
        /// <returns>A converted file path.</returns>
        public static string ConvertToWWWFormat(string path)
        {
            return $"file://{path.Replace('\\', '/')}";
        }

        /// <summary>
        /// Indicates whether a specified string is null, empty, or consists only of white-space characters.
        /// </summary>
        /// <param name="self">The string to test.</param>
        /// <returns>True if the value parameter is null or empty, or if value consists exclusively of white-space characters.</returns>
        public static bool IsNullOrWhiteSpace(this string self)
        {
            return self == null || self.Trim().Length == 0;
        }

        public static IEnumerable<TNode> TopologicalSort<TNode>(IEnumerable<TNode> nodes, Func<TNode, IEnumerable<TNode>> dependencySelector)
        {
            List<TNode> sorted_list = new List<TNode>();

            HashSet<TNode> visited = new HashSet<TNode>();
            HashSet<TNode> sorted = new HashSet<TNode>();

            foreach (TNode input in nodes)
                Visit(input);

            return sorted_list;

            void Visit(TNode node)
            {
                if (visited.Contains(node))
                {
                    if (!sorted.Contains(node))
                        throw new Exception("Cyclic Dependency");
                }
                else
                {
                    visited.Add(node);

                    foreach (var dep in dependencySelector(node))
                        Visit(dep);

                    sorted.Add(node);
                    sorted_list.Add(node);
                }
            }
        }

        /// <summary>
        /// Try to resolve and load the given assembly DLL.
        /// </summary>
        /// <param name="assemblyName">Name of the assembly, of the type <see cref="AssemblyName" />.</param>
        /// <param name="directory">Directory to search the assembly from.</param>
        /// <param name="assembly">The loaded assembly.</param>
        /// <returns>True, if the assembly was found and loaded. Otherwise, false.</returns>
        public static bool TryResolveDllAssembly(AssemblyName assemblyName, string directory, out Assembly assembly)
        {
            assembly = null;
            string path = Path.Combine(directory, $"{assemblyName.Name}.dll");

            if (!File.Exists(path))
                return false;

            try
            {
                assembly = Assembly.LoadFile(path);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }
    }
}