﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

namespace BepInEx;

/// <summary>
///     Generic helper properties and methods.
/// </summary>
public static class Utility
{
    private const string TRUSTED_PLATFORM_ASSEMBLIES = "TRUSTED_PLATFORM_ASSEMBLIES";
    private static bool? sreEnabled;

    /// <summary>
    ///     Whether current Common Language Runtime supports dynamic method generation using
    ///     <see cref="System.Reflection.Emit" /> namespace.
    /// </summary>
    public static bool CLRSupportsDynamicAssemblies => CheckSRE();

    /// <summary>
    ///     An encoding for UTF-8 which does not emit a byte order mark (BOM).
    /// </summary>
    public static Encoding UTF8NoBom { get; } = new UTF8Encoding(false);

    private static bool CheckSRE()
    {
        try
        {
            if (sreEnabled.HasValue)
                return sreEnabled.Value;

            // ReSharper disable once AssignNullToNotNullAttribute
            _ = new CustomAttributeBuilder(null, new object[0]);
        }
        catch (PlatformNotSupportedException)
        {
            sreEnabled = false;
            return sreEnabled.Value;
        }
        catch (ArgumentNullException)
        {
            // Suppress ArgumentNullException
        }

        sreEnabled = true;
        return sreEnabled.Value;
    }

    /// <summary>
    ///     Try to perform an action.
    /// </summary>
    /// <param name="action">Action to perform.</param>
    /// <param name="exception">Possible exception that gets returned.</param>
    /// <returns>True, if action succeeded, false if an exception occured.</returns>
    public static bool TryDo(Action action, out Exception exception)
    {
        exception = null;
        try
        {
            action();
            return true;
        }
        catch (Exception e)
        {
            exception = e;
            return false;
        }
    }

    /// <summary>
    ///     Combines multiple paths together, as the specific method is not available in .NET 3.5.
    /// </summary>
    /// <param name="parts">The multiple paths to combine together.</param>
    /// <returns>A combined path.</returns>
    public static string CombinePaths(params string[] parts) => parts.Aggregate(Path.Combine);

    /// <summary>
    ///     Returns the parent directory of a path, optionally specifying the amount of levels.
    /// </summary>
    /// <param name="path">The path to get the parent directory of.</param>
    /// <param name="levels">The amount of levels to traverse. Defaults to 1</param>
    /// <returns>The parent directory.</returns>
    public static string ParentDirectory(string path, int levels = 1)
    {
        for (var i = 0; i < levels; i++)
            path = Path.GetDirectoryName(path);

        return path;
    }

    /// <summary>
    ///     Tries to parse a bool, with a default value if unable to parse.
    /// </summary>
    /// <param name="input">The string to parse</param>
    /// <param name="defaultValue">The value to return if parsing is unsuccessful.</param>
    /// <returns>Boolean value of input if able to be parsed, otherwise default value.</returns>
    public static bool SafeParseBool(string input, bool defaultValue = false) =>
        bool.TryParse(input, out var result) ? result : defaultValue;

    /// <summary>
    ///     Converts a file path into a UnityEngine.WWW format.
    /// </summary>
    /// <param name="path">The file path to convert.</param>
    /// <returns>A converted file path.</returns>
    public static string ConvertToWWWFormat(string path) => $"file://{path.Replace('\\', '/')}";

    /// <summary>
    ///     Indicates whether a specified string is null, empty, or consists only of white-space characters.
    /// </summary>
    /// <param name="self">The string to test.</param>
    /// <returns>True if the value parameter is null or empty, or if value consists exclusively of white-space characters.</returns>
    public static bool IsNullOrWhiteSpace(this string self) => self == null || self.All(char.IsWhiteSpace);

    /// <summary>
    ///     Sorts a given dependency graph using a direct toposort, reporting possible cyclic dependencies.
    /// </summary>
    /// <param name="nodes">Nodes to sort</param>
    /// <param name="dependencySelector">Function that maps a node to a collection of its dependencies.</param>
    /// <typeparam name="TNode">Type of the node in a dependency graph.</typeparam>
    /// <returns>Collection of nodes sorted in the order of least dependencies to the most.</returns>
    /// <exception cref="Exception">Thrown when a cyclic dependency occurs.</exception>
    public static IEnumerable<TNode> TopologicalSort<TNode>(IEnumerable<TNode> nodes,
                                                            Func<TNode, IEnumerable<TNode>> dependencySelector)
    {
        var sorted_list = new List<TNode>();

        var visited = new HashSet<TNode>();
        var sorted = new HashSet<TNode>();

        foreach (var input in nodes)
        {
            var currentStack = new Stack<TNode>();
            if (!Visit(input, currentStack))
                throw new Exception("Cyclic Dependency:\r\n" + currentStack.Select(x => $" - {x}") //append dashes
                                                                           .Aggregate((a, b) =>
                                                                               $"{a}\r\n{b}")); //add new lines inbetween
        }


        return sorted_list;

        bool Visit(TNode node, Stack<TNode> stack)
        {
            if (visited.Contains(node))
            {
                if (!sorted.Contains(node)) return false;
            }
            else
            {
                visited.Add(node);
                stack.Push(node);
                if (dependencySelector(node).Any(dep => !Visit(dep, stack)))
                    return false;

                sorted.Add(node);
                sorted_list.Add(node);

                stack.Pop();
            }

            return true;
        }
    }

    /// <summary>
    ///     Try to resolve and load the given assembly DLL.
    /// </summary>
    /// <param name="assemblyName">Name of the assembly, of the type <see cref="AssemblyName" />.</param>
    /// <param name="directory">Directory to search the assembly from.</param>
    /// <param name="assembly">The loaded assembly.</param>
    /// <returns>True, if the assembly was found and loaded. Otherwise, false.</returns>
    public static bool TryResolveDllAssembly<T>(AssemblyName assemblyName,
                                                string directory,
                                                Func<string, T> loader,
                                                out T assembly) where T : class
    {
        assembly = null;

        var potentialDirectories = new List<string> { directory };

        if (!Directory.Exists(directory))
            return false;

        potentialDirectories.AddRange(Directory.GetDirectories(directory, "*", SearchOption.AllDirectories));

        foreach (var subDirectory in potentialDirectories)
        {
            var potentialPaths = new[]
            {
                $"{assemblyName.Name}.dll",
                $"{assemblyName.Name}.exe"
            };

            foreach (var potentialPath in potentialPaths)
            {
                var path = Path.Combine(subDirectory, potentialPath);

                if (!File.Exists(path))
                    continue;

                try
                {
                    assembly = loader(path);
                }
                catch (Exception)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Checks whether a given cecil type definition is a subtype of a provided type.
    /// </summary>
    /// <param name="self">Cecil type definition</param>
    /// <param name="td">Type to check against</param>
    /// <returns>Whether the given cecil type is a subtype of the type.</returns>
    public static bool IsSubtypeOf(this TypeDefinition self, Type td)
    {
        if (self.FullName == td.FullName)
            return true;
        return self.FullName != "System.Object" && (self.BaseType?.Resolve()?.IsSubtypeOf(td) ?? false);
    }

    /// <summary>
    ///     Try to resolve and load the given assembly DLL.
    /// </summary>
    /// <param name="assemblyName">Name of the assembly, of the type <see cref="AssemblyName" />.</param>
    /// <param name="directory">Directory to search the assembly from.</param>
    /// <param name="assembly">The loaded assembly.</param>
    /// <returns>True, if the assembly was found and loaded. Otherwise, false.</returns>
    public static bool TryResolveDllAssembly(AssemblyName assemblyName, string directory, out Assembly assembly) =>
        TryResolveDllAssembly(assemblyName, directory, Assembly.LoadFrom, out assembly);

    /// <summary>
    ///     Try to resolve and load the given assembly DLL.
    /// </summary>
    /// <param name="assemblyName">Name of the assembly, of the type <see cref="AssemblyName" />.</param>
    /// <param name="directory">Directory to search the assembly from.</param>
    /// <param name="readerParameters">Reader parameters that contain possible custom assembly resolver.</param>
    /// <param name="assembly">The loaded assembly.</param>
    /// <returns>True, if the assembly was found and loaded. Otherwise, false.</returns>
    public static bool TryResolveDllAssembly(AssemblyName assemblyName,
                                             string directory,
                                             ReaderParameters readerParameters,
                                             out AssemblyDefinition assembly) =>
        TryResolveDllAssembly(assemblyName, directory,
                              s => AssemblyDefinition.ReadAssembly(s, readerParameters), out assembly);

    /// <summary>
    ///     Tries to create a file with the given name
    /// </summary>
    /// <param name="path">Path of the file to create</param>
    /// <param name="mode">File open mode</param>
    /// <param name="fileStream">Resulting filestream</param>
    /// <param name="access">File access options</param>
    /// <param name="share">File share options</param>
    /// <returns></returns>
    public static bool TryOpenFileStream(string path,
                                         FileMode mode,
                                         out FileStream fileStream,
                                         FileAccess access = FileAccess.ReadWrite,
                                         FileShare share = FileShare.Read)
    {
        try
        {
            fileStream = new FileStream(path, mode, access, share);

            return true;
        }
        catch (IOException)
        {
            fileStream = null;

            return false;
        }
    }

    public static IEnumerable<MethodDefinition> EnumerateAllMethods(this TypeDefinition type)
    {
        var currentType = type;

        while (currentType != null)
        {
            foreach (var method in currentType.Methods)
                yield return method;

            currentType = currentType.BaseType?.Resolve();
        }
    }

    /// <summary>
    ///     Compute a MD5 hash of the given stream.
    /// </summary>
    /// <param name="stream">Stream to hash</param>
    /// <returns>MD5 hash as a hex string</returns>
    public static string HashStream(Stream stream)
    {
        using var md5 = MD5.Create();

        var buf = new byte[4096];
        int read;
        while ((read = stream.Read(buf, 0, buf.Length)) > 0)
            md5.TransformBlock(buf, 0, read, buf, 0);

        md5.TransformFinalBlock(new byte[0], 0, 0);

        return ByteArrayToString(md5.Hash);
    }

    /// <summary>
    /// Hash a list of strings using MD5
    /// </summary>
    /// <param name="strings">Strings to hash</param>
    /// <returns>MD5 of the strings</returns>
    public static string HashStrings(params string[] strings)
    {
        using var md5 = MD5.Create();

        foreach (var str in strings)
            md5.TransformBlock(Encoding.UTF8.GetBytes(str), 0, str.Length, null, 0);

        md5.TransformFinalBlock(new byte[0], 0, 0);

        return ByteArrayToString(md5.Hash);
    }

    /// <summary>
    ///     Convert the given array to a hex string.
    /// </summary>
    /// <param name="data">Bytes to convert.</param>
    /// <returns>Bytes reinterpreted as a hex number.</returns>
    public static string ByteArrayToString(byte[] data)
    {
        var builder = new StringBuilder(data.Length * 2);

        foreach (var b in data)
            builder.AppendFormat("{0:x2}", b);

        return builder.ToString();
    }

    /// <summary>
    /// Get a value of a command line argument
    /// </summary>
    /// <param name="arg">Argument name</param>
    /// <returns>Next argument after the given argument name. If not found, returns null.</returns>
    public static string GetCommandLineArgValue(string arg)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 1; i < args.Length; i++)
            if (args[i] == arg && i + 1 < args.Length)
                return args[i + 1];
        return null;
    }

    /// <summary>
    ///     Try to parse given string as an assembly name
    /// </summary>
    /// <param name="fullName">Fully qualified assembly name</param>
    /// <param name="assemblyName">Resulting <see cref="AssemblyName" /> instance</param>
    /// <returns><c>true</c>, if parsing was successful, otherwise <c>false</c></returns>
    /// <remarks>
    ///     On some versions of mono, using <see cref="Assembly.GetName()" /> fails because it runs on unmanaged side
    ///     which has problems with encoding.
    ///     Using <see cref="AssemblyName" /> solves this by doing parsing on managed side instead.
    /// </remarks>
    public static bool TryParseAssemblyName(string fullName, out AssemblyName assemblyName)
    {
        try
        {
            assemblyName = new AssemblyName(fullName);
            return true;
        }
        catch (Exception)
        {
            assemblyName = null;
            return false;
        }
    }

    internal static void AddCecilPlatformAssemblies(this AppDomain appDomain, string assemblyDir)
    {
        if (!Directory.Exists(assemblyDir))
            return;
        // Cecil 0.11 requires one to manually set up list of trusted assemblies for assembly resolving
        var curTrusted = appDomain.GetData(TRUSTED_PLATFORM_ASSEMBLIES) as string;
        var addTrusted = string.Join(Path.PathSeparator.ToString(),
                                     Directory.GetFiles(assemblyDir, "*.dll",
                                                        SearchOption.TopDirectoryOnly));
        var newTrusted = curTrusted == null ? addTrusted : $"{curTrusted}{Path.PathSeparator}{addTrusted}";
        appDomain.SetData(TRUSTED_PLATFORM_ASSEMBLIES, newTrusted);
    }

    /// <summary>
    ///     Gets unique files in all given directories. If the file with the same name exists in multiple directories,
    ///     only the first occurrence is returned.
    /// </summary>
    /// <param name="directories">Directories to search from.</param>
    /// <param name="pattern">File pattern to search.</param>
    /// <returns>Collection of all files in the directories.</returns>
    public static IEnumerable<string> GetUniqueFilesInDirectories(IEnumerable<string> directories,
                                                                  string pattern = "*")
    {
        var result = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var directory in directories)
        foreach (var file in Directory.GetFiles(directory, pattern))
        {
            var fileName = Path.GetFileName(file);
            if (!result.ContainsKey(fileName))
                result[fileName] = file;
        }

        return result.Values;
    }
}
