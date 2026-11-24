using System.ComponentModel;
using VYaml.Annotations;

namespace Engine.Core;

[EngineFeature(EngineFeature.Text)]
public abstract record UiControlComponent : SceneComponent
{
    internal override string Kind => nameof(UiButtonComponent);
    public bool Interactable { get; set; } = true;
    public string? Style { get; set; }
    internal virtual float DefaultValue => 0;
    internal virtual float Normalize(float value) => value;
    internal virtual void Validate(string name) { }
}

[DisplayName("UI Slider"), YamlObject]
public sealed partial record UiSliderComponent : UiControlComponent
{
    public UiSliderComponent() { }
    [YamlConstructor] public UiSliderComponent(float minimum = 0, float maximum = 1, float step = .1f, float initial = 0, bool interactable = true)
        => (Minimum, Maximum, Step, Initial, Interactable) = (minimum, maximum, step, initial, interactable);
    public float Minimum { get; set; }
    public float Maximum { get; set; } = 1;
    public float Step { get; set; } = .1f;
    public float Initial { get; set; }
    internal override float DefaultValue => Initial;
    internal override float Normalize(float value)
    {
        if (value <= Minimum || !(Maximum > Minimum && Step > 0)) return Minimum;
        if (value >= Maximum) return Maximum;
        double index = Math.Round(((double)value - Minimum) / Step, MidpointRounding.AwayFromZero);
        float snapped = Math.Clamp((float)(Minimum + index * Step), Minimum, Maximum);
        return Maximum - value < Math.Abs(snapped - value) ? Maximum : snapped;
    }
    internal float Advance(float value, int direction)
    {
        if (direction < 0 && value == Maximum)
        {
            double steps = Math.Ceiling(((double)Maximum - Minimum) / Step - 1e-6) - 1;
            return Math.Max(Minimum, (float)(Minimum + steps * Step));
        }
        return Normalize(value + direction * Step);
    }
    internal override void Validate(string name)
    {
        if (!float.IsFinite(Minimum) || !float.IsFinite(Maximum) || Maximum <= Minimum ||
            !float.IsFinite(Step) || Step <= 0 || !float.IsFinite(Initial) || Initial < Minimum || Initial > Maximum)
            throw new InvalidDataException($"{name}: slider needs a finite range, positive step and initial value within the range.");
    }
}

[DisplayName("UI Toggle"), YamlObject]
public sealed partial record UiToggleComponent : UiControlComponent
{
    public UiToggleComponent() { }
    [YamlConstructor] public UiToggleComponent(bool initial = false, bool interactable = true)
        => (Initial, Interactable) = (initial, interactable);
    public bool Initial { get; set; }
    internal override float DefaultValue => Initial ? 1 : 0;
}

[DisplayName("UI Dropdown"), YamlObject]
public sealed partial record UiDropdownComponent : UiControlComponent
{
    public UiDropdownComponent() { }
    [YamlConstructor] public UiDropdownComponent(bool interactable = true, string[]? options = null) =>
        (Interactable, Options) = (interactable, options ?? Options);
    public string[] Options { get; set; } = ["Option 1", "Option 2"];
    public int Initial { get; set; }
    internal override float DefaultValue => Initial;
    internal override float Normalize(float value) => Math.Clamp((int)value, 0, Math.Max(0, Options.Length - 1));
    internal override void Validate(string name)
    {
        if (Options is not { Length: > 0 } || Options.Any(string.IsNullOrWhiteSpace) || Initial < 0 || Initial >= Options.Length)
            throw new InvalidDataException($"{name}: dropdown needs labels and a valid initial index.");
    }
}
