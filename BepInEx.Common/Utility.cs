using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BepInEx.Common
{
    /// <summary>
    /// Generic helper properties and methods.
    /// </summary>
    public static class Utility
    {
        /// <summary>
        /// The directory that the Koikatsu .exe is being run from.
        /// </summary>
        public static string ExecutingDirectory => Path.GetDirectoryName(Environment.CommandLine);

        /// <summary>
        /// The path that the plugins folder is located.
        /// </summary>
        public static string PluginsDirectory => Path.Combine(ExecutingDirectory, "BepInEx");

        /// <summary>
        /// Combines multiple paths together, as the specfic method is not availble in .NET 3.5.
        /// </summary>
        /// <param name="parts">The multiple paths to combine together.</param>
        /// <returns>A combined path.</returns>
        public static string CombinePaths(params string[] parts) => parts.Aggregate(Path.Combine);

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
    }
}
