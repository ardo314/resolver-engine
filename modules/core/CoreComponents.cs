using Engine.Core;

namespace Modules.Core;

// --- Core method definitions ---

public static class CoreMethods
{
    public static readonly Method GetPose = Method.Define<double[]>("getPose");
    public static readonly Method SetPose = Method.DefineWithInput<double[]>("setPose");

    public static readonly Method GetName = Method.Define<string>("getName");
    public static readonly Method SetName = Method.DefineWithInput<string>("setName");

    public static readonly Method GetParent = Method.Define<string>("getParent");
    public static readonly Method SetParent = Method.DefineWithInput<string>("setParent");
}

// --- Core component definitions ---

public static class CoreComponents
{
    public static readonly Component Pose = Component.Define("core.pose",
        CoreMethods.GetPose, CoreMethods.SetPose);

    public static readonly Component Name = Component.Define("core.name",
        CoreMethods.GetName, CoreMethods.SetName);

    public static readonly Component Parent = Component.Define("core.parent",
        CoreMethods.GetParent, CoreMethods.SetParent);
}
