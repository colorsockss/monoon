using System.ComponentModel;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record UiDefaults(string Locale="en",float TextScale=1,bool HighContrast=false);
public sealed class UiPreferences(UiDefaults defaults)
{
    private string locale=defaults.Locale;
    private float scale=defaults.TextScale;
    public string Locale { get=>locale;set=>locale=!string.IsNullOrWhiteSpace(value)&&value.Length<=64?value:throw new ArgumentException("Expected a language tag."); }
    public float TextScale { get=>scale;set=>scale=value is >=.75f and <=2?value:throw new ArgumentOutOfRangeException(nameof(value),"Use a text scale from 0.75 to 2."); }
    public bool HighContrast { get;set; }=defaults.HighContrast;
}
[DisplayName("UI Layout"),YamlObject]
public sealed partial record UiLayoutComponent : SceneComponent
{
    public UiLayoutComponent() {}
    [YamlConstructor] public UiLayoutComponent(string axis="Vertical",float gap=6,float padding=6,bool stretch=true)
        =>(Axis,Gap,Padding,Stretch)=(axis,gap,padding,stretch);
    public string Axis { get;set; }="Vertical";
    public float Gap { get;set; }=6;
    public float Padding { get;set; }=6;
    public bool Stretch { get;set; }=true;
}
[DisplayName("UI Scroll View"),YamlObject]
public sealed partial record UiScrollComponent : SceneComponent;
[DisplayName("UI Text Field"),YamlObject]
public sealed partial record UiTextFieldComponent : SceneComponent
{
    public UiTextFieldComponent() {}
    [YamlConstructor] public UiTextFieldComponent(string value="",int maxLength=128)=>(Value,MaxLength)=(value,maxLength);
    public string Value {get;set;}="";
    public int MaxLength {get;set;}=128;
}
