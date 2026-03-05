using System;

namespace Engine.Core;

/// <summary>
/// Marker attribute that the Engine source generator will look for.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GenerateAttribute : Attribute { }
