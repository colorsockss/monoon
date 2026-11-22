namespace Engine.Core;

public sealed partial class ScreenUi
{
    public string? TextOf(Guid id) => byId.GetValueOrDefault(id) is not { } n ? null : n.Field != null ? Value(id) : n.Label != null ? Label(n) : null;
}
