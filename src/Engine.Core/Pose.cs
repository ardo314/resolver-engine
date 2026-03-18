using System.Numerics;

namespace Engine.Core;

public struct Pose
{
    public Vector3 Position { get; init; }
    public Quaternion Rotation { get; init; }
}

public interface IPose : IDataBehaviour<Pose> { }
