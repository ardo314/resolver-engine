using System.Reflection;
using Engine.Core;

namespace Engine.ModuleRuntime;

/// <summary>
/// Scans a directory for assemblies containing <see cref="IExtension"/> implementations.
/// </summary>
public static class ExtensionLoader
{
    /// <summary>
    /// Loads all assemblies from <paramref name="directory"/> and returns
    /// an instance of every <see cref="IExtension"/> found.
    /// </summary>
    public static IReadOnlyList<IExtension> LoadFrom(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var extensions = new List<IExtension>();

        foreach (var dll in Directory.EnumerateFiles(directory, "*.dll"))
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(dll);
            }
            catch
            {
                // Skip assemblies that can't be loaded (e.g., native libs)
                continue;
            }

            foreach (var type in GetExtensionTypes(assembly))
            {
                if (Activator.CreateInstance(type) is IExtension extension)
                {
                    extensions.Add(extension);
                }
            }
        }

        return extensions;
    }

    private static IEnumerable<Type> GetExtensionTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        return types.Where(t =>
            t is { IsClass: true, IsAbstract: false } && typeof(IExtension).IsAssignableFrom(t)
        );
    }
}
