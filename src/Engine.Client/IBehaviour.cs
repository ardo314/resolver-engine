namespace Engine.Client;

/// <summary>
/// Marker interface for behaviour contracts.
/// Behaviours define data and logic interfaces (e.g. <see cref="IPose"/>, <see cref="IParent"/>)
/// that component structs declare support for via <see cref="HasBehaviourAttribute{T}"/>.
/// </summary>
public interface IBehaviour { }
