namespace Engine.Core;

/// <summary>Reads an Inspector field previously saved under another name. List the most recent former name first.</summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
public sealed class FormerlySerializedAsAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
