using System.Numerics;
using Engine.Core;

namespace Engine.Math;

public struct Pose
{
    public Vector3 Position { get; init; }
    public Quaternion Rotation { get; init; }
}

public interface IPose : IComponentData<Pose> { }
