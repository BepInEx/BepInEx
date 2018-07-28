﻿using System;
using System.Collections.Generic;
 using System.Reflection;

namespace BepInEx.Common
{
    /// <summary>
    /// Generic helper properties and methods.
    /// </summary>
    [Obsolete("This class has moved, please use BepInEx.Utility instead of BepInEx.Common.Utility", true)]
    public static class Utility
    {
	    /// <summary>
	    /// The directory that the game .exe is being run from.
	    /// </summary>
	    [Obsolete("This property has been moved, please use Paths.GameRootPath instead", true)]
	    public static string ExecutingDirectory
		    => Paths.GameRootPath;

	    /// <summary>
	    /// The path that the plugins folder is located.
	    /// </summary>
	    [Obsolete("This property has been moved, please use Paths.PluginPath instead", true)]
	    public static string PluginsDirectory
			=> Paths.PluginPath;

	    /// <summary>
	    /// Combines multiple paths together, as the specfic method is not availble in .NET 3.5.
	    /// </summary>
	    /// <param name="parts">The multiple paths to combine together.</param>
	    /// <returns>A combined path.</returns>
	    [Obsolete("This method has been moved, please use BepInEx.Utility instead of BepInEx.Common.Utility", true)]
	    public static string CombinePaths(params string[] parts) => 
		    BepInEx.Utility.CombinePaths(parts);

	    /// <summary>
	    /// Tries to parse a bool, with a default value if unable to parse.
	    /// </summary>
	    /// <param name="input">The string to parse</param>
	    /// <param name="defaultValue">The value to return if parsing is unsuccessful.</param>
	    /// <returns>Boolean value of input if able to be parsed, otherwise default value.</returns>
	    [Obsolete("This method has been moved, please use BepInEx.Utility instead of BepInEx.Common.Utility", true)]
	    public static bool SafeParseBool(string input, bool defaultValue = false) =>
		    BepInEx.Utility.SafeParseBool(input, defaultValue);

	    /// <summary>
	    /// Converts a file path into a UnityEngine.WWW format.
	    /// </summary>
	    /// <param name="path">The file path to convert.</param>
	    /// <returns>A converted file path.</returns>
	    [Obsolete("This method has been moved, please use BepInEx.Utility instead of BepInEx.Common.Utility", true)]
	    public static string ConvertToWWWFormat(string path)
		    => BepInEx.Utility.ConvertToWWWFormat(path);

        /// <summary>
        /// Indicates whether a specified string is null, empty, or consists only of white-space characters.
        /// </summary>
        /// <param name="self">The string to test.</param>
        /// <returns>True if the value parameter is null or empty, or if value consists exclusively of white-space characters.</returns>
        [Obsolete("This method has been moved, please use BepInEx.Utility instead of BepInEx.Common.Utility", true)]
        public static bool IsNullOrWhiteSpace(this string self)
	        => BepInEx.Utility.IsNullOrWhiteSpace(self);

	    [Obsolete("This method has been moved, please use BepInEx.Utility instead of BepInEx.Common.Utility", true)]
	    public static IEnumerable<TNode> TopologicalSort<TNode>(IEnumerable<TNode> nodes, Func<TNode, IEnumerable<TNode>> dependencySelector)
		    => BepInEx.Utility.TopologicalSort(nodes, dependencySelector);

	    /// <summary>
	    /// Try to resolve and load the given assembly DLL.
	    /// </summary>
	    /// <param name="assemblyName">Name of the assembly, of the type <see cref="AssemblyName" />.</param>
	    /// <param name="directory">Directory to search the assembly from.</param>
	    /// <param name="assembly">The loaded assembly.</param>
	    /// <returns>True, if the assembly was found and loaded. Otherwise, false.</returns>
	    [Obsolete("This method has been moved, please use BepInEx.Utility instead of BepInEx.Common.Utility", true)]
	    public static bool TryResolveDllAssembly(AssemblyName assemblyName, string directory, out Assembly assembly)
		    => BepInEx.Utility.TryResolveDllAssembly(assemblyName, directory, out assembly);
    }
}