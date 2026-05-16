using Engine.Core;
using Modules.Core;

namespace Modules.Nova;

public static class NovaComponents
{
    public static readonly Component Name = Component.Define("nova.name",
        CoreMethods.GetName, CoreMethods.SetName);

    public static readonly Component Parent = Component.Define("nova.parent",
        CoreMethods.GetParent, CoreMethods.SetParent);

    public static readonly Component Pose = Component.Define("nova.pose",
        CoreMethods.GetPose, CoreMethods.SetPose);
}
