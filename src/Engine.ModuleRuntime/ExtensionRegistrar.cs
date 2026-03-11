using Engine.Core;

namespace Engine.ModuleRuntime;

/// <summary>
/// Collects component and behaviour registrations from extensions.
/// </summary>
public sealed class ExtensionRegistrar : IExtensionRegistrar
{
    private readonly List<Type> _componentTypes = [];
    private readonly List<(Type Contract, Type Implementation)> _behaviourTypes = [];
    private readonly List<Type> _pluginTypes = [];

    public IReadOnlyList<Type> ComponentTypes => _componentTypes;
    public IReadOnlyList<(Type Contract, Type Implementation)> BehaviourTypes => _behaviourTypes;
    public IReadOnlyList<Type> PluginTypes => _pluginTypes;

    public void AddComponent<T>()
        where T : IComponent
    {
        _componentTypes.Add(typeof(T));
    }

    public void AddBehaviour<TContract, TImplementation>()
        where TContract : IBehaviour
        where TImplementation : TContract
    {
        _behaviourTypes.Add((typeof(TContract), typeof(TImplementation)));
    }

    public void AddPlugin<T>()
        where T : Plugin, new()
    {
        _pluginTypes.Add(typeof(T));
    }
}
