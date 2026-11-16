namespace Engine.Core;

public sealed partial class ScreenUi
{
    private readonly record struct InputMode(Guid Root, Guid? PreviousFocus);
    private readonly List<InputMode> inputModes = [];
    // Only objects belonging to a mode need entries. Include non-widget script descendants.
    private readonly Dictionary<Guid, Guid?> modeOwners = [];
    public Guid? ActiveInputMode => inputModes.Count == 0 ? null : inputModes[^1].Root;
    internal int InputModeRevision { get; private set; }
    internal event Action? InputModeChanged;

    internal bool ReceivesInput(Guid id) => modeOwners.GetValueOrDefault(id) == ActiveInputMode;
    private bool ModeClosed(Node node) => modeOwners.GetValueOrDefault(node.Object.Id) == node.Object.Id &&
        LayerOf(node.Object.Id) == 0;

    public bool PushInputMode(Guid root)
    {
        ValidateInputModes();
        if (modeOwners.GetValueOrDefault(root) != root || !byId.TryGetValue(root, out var node) ||
            LayerOf(root) > 0) return false;
        inputModes.Add(new(root, focus));
        if (!Visible(node))
        {
            inputModes.RemoveAt(inputModes.Count - 1);
            return false;
        }
        SwitchMode(null, true);
        return true;
    }

    public bool PopInputMode(Guid root)
    {
        if (ActiveInputMode != root) return false;
        RemoveModesFrom(inputModes.Count - 1);
        return true;
    }

    internal void ValidateInputModes()
    {
        for (int i = 0; i < inputModes.Count; i++)
        {
            var root = inputModes[i].Root;
            if (modeOwners.GetValueOrDefault(root) == root && byId.TryGetValue(root, out var n) && Visible(n)) continue;
            RemoveModesFrom(i);
            break;
        }
    }

    private void RemoveModesFrom(int first)
    {
        var previous = inputModes[first].PreviousFocus;
        inputModes.RemoveRange(first, inputModes.Count - first);
        SwitchMode(previous, ActiveInputMode.HasValue);
    }

    private void SwitchMode(Guid? previous, bool focusFirst)
    {
        InputModeRevision++;
        InputModeChanged?.Invoke();
        clicks.Clear();
        CancelPointer();
        SetFocus(null);
        OrderModeLayers();
        if (previous is Guid id && byId.TryGetValue(id, out var restored) && Enabled(restored) && Visible(restored))
            SetFocus(id);
        else if (focusFirst)
            SetFocus(nodes.FirstOrDefault(n => Enabled(n) && Visible(n))?.Object.Id);
    }

    private int LayerOf(Guid root)
    {
        for (int i = 0; i < inputModes.Count; i++)
            if (inputModes[i].Root == root) return i + 1;
        return 0;
    }

    // In Scene edit mode the selected widget determines which closed panels to preview.
    internal void PreviewModeFor(Guid? selected)
    {
        var owner = selected.HasValue ? modeOwners.GetValueOrDefault(selected.Value) : null;
        if (owner == ActiveInputMode) return;
        if (inputModes.Count > 0) RemoveModesFrom(0);
        if (owner is not Guid id) return;
        var ancestors = new Stack<Guid>();
        for (var n = byId[id]; n != null; n = n.Parent)
            if (modeOwners.GetValueOrDefault(n.Object.Id) == n.Object.Id) ancestors.Push(n.Object.Id);
        foreach (var root in ancestors) PushInputMode(root);
    }

    private int PaintLayer(Node node) => modeOwners.GetValueOrDefault(node.Object.Id) is Guid owner ? LayerOf(owner) : 0;

    private void OrderModeLayers()
    {
        if (modeOwners.Count == 0) return;
        nodes = treeOrder.OrderBy(PaintLayer).ThenBy(node => node.Order).ToArray();
    }
}
