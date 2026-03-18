namespace Engine.Client;

/// <summary>
/// Marker interface for all generated client-side proxies.
/// Both behaviour proxies (e.g. <c>PoseProxy</c>) and component proxies
/// (e.g. <c>InMemoryPoseProxy</c>) implement this interface, enabling a
/// unified <c>Entity.GetComponentProxy&lt;T&gt;()</c> API.
/// </summary>
public interface IProxy { }
