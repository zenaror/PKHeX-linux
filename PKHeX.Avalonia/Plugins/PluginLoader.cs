using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace PKHeX.Avalonia.Plugins;

/// <summary>
/// Loads plugin assemblies from a folder (port of the WinForms <c>PluginLoader</c>).
/// </summary>
/// <remarks>
/// Plugins implement <see cref="PKHeX.Core.IPlugin"/> and are handed the save editor, the entity editor,
/// the Tools menu and the program version through <see cref="PKHeX.Core.IPlugin.Initialize"/>.
/// </remarks>
public static class PluginLoader
{
    /// <summary>
    /// Loads every plugin of type <typeparamref name="T"/> found in <paramref name="pluginPath"/>.
    /// </summary>
    /// <returns>Load contexts, which must be kept alive so plugins can resolve their own dependencies.</returns>
    [RequiresUnreferencedCode("Plugin loading depends on runtime-discovered assemblies and types.")]
    [RequiresAssemblyFiles("Plugin loading reads assemblies from disk.")]
    public static PluginLoadResult LoadPlugins<T>(string pluginPath, List<T> list) where T : class
    {
        var result = new PluginLoadResult();
        if (Directory.Exists(pluginPath))
        {
            foreach (var file in Directory.EnumerateFiles(pluginPath, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    result.Load(file);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unable to load plugin from file: {file}");
                    Debug.WriteLine(ex.Message);
                }
            }
        }

        foreach (var type in GetPluginTypes<T>(result.GetAssemblies()))
        {
            T? plugin;
            try
            {
                plugin = (T?)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to load plugin [{type.Name}]: {type.FullName}");
                Debug.WriteLine(ex.Message);
                continue;
            }
            if (plugin is not null)
                list.Add(plugin);
        }
        return result;
    }

    [RequiresUnreferencedCode("Plugin discovery depends on runtime-discovered types.")]
    private static IEnumerable<Type> GetPluginTypes<T>(IEnumerable<Assembly> assemblies)
    {
        var pluginType = typeof(T);
        foreach (var assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // A plugin built against a different API surface; use whatever loaded.
                types = [.. ex.Types.OfType<Type>()];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to read types from {assembly.FullName}: {ex.Message}");
                continue;
            }

            foreach (var type in types)
            {
                if (!type.IsAbstract && !type.IsInterface && pluginType.IsAssignableFrom(type))
                    yield return type;
            }
        }
    }
}

/// <summary>
/// Keeps the load contexts of the loaded plugin assemblies alive.
/// </summary>
public sealed class PluginLoadResult
{
    private readonly List<PluginLoadContext> Contexts = [];
    private readonly List<Assembly> Assemblies = [];

    /// <summary>Loads one assembly into its own context.</summary>
    [RequiresAssemblyFiles("Plugin loading reads assemblies from disk.")]
    public void Load(string path)
    {
        var context = new PluginLoadContext(path);
        var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(path));
        Contexts.Add(context);
        Assemblies.Add(assembly);
    }

    /// <summary>Assemblies loaded so far.</summary>
    public IReadOnlyList<Assembly> GetAssemblies() => Assemblies;
}

/// <summary>
/// Resolves a plugin's private dependencies next to the plugin file, sharing the host's assemblies.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver Resolver;

    [RequiresAssemblyFiles("Plugin loading reads assemblies from disk.")]
    public PluginLoadContext(string pluginPath) : base(isCollectible: false)
        => Resolver = new AssemblyDependencyResolver(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Types shared with the host (PKHeX.Core, Avalonia, the app itself) must come from the host.
        var loaded = Default.Assemblies.FirstOrDefault(z => z.GetName().Name == assemblyName.Name);
        if (loaded is not null)
            return loaded;

        var path = Resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = Resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
