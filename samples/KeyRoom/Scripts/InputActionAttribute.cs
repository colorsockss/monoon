using System;
using System.Diagnostics;

/// <summary>The string field names an input action; Editor/KeyRoomTools draws it as a list of the project's actions.</summary>
// [Conditional]: the editor reads it from the script source, and no field carries it in a game build.
[Conditional("MONOON_EDITOR"), AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class InputActionAttribute : Attribute;
