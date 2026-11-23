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
    private float shake=1, flash=1;
    /// <summary>Scales camera shake and zoom punch: 0 turns screen motion off.</summary>
    public float ShakeScale { get=>shake;set=>shake=value is >=0 and <=1?value:throw new ArgumentOutOfRangeException(nameof(value),"Use a shake scale from 0 to 1."); }
    /// <summary>Scales the opacity of screen flashes: 0 turns them off.</summary>
    public float FlashScale { get=>flash;set=>flash=value is >=0 and <=1?value:throw new ArgumentOutOfRangeException(nameof(value),"Use a flash scale from 0 to 1."); }
    /// <summary>False ignores gamepad rumble requests.</summary>
    public bool Rumble { get;set; }=true;

    private const string Prefix="accessibility.";
    /// <summary>Writes these preferences into player settings (for example <see cref="PlayerStorage.SaveSettings"/>).</summary>
    public void SaveTo(GameData settings)
    {
        settings.Set(Prefix+"locale",Locale); settings.Set(Prefix+"textScale",TextScale); settings.Set(Prefix+"highContrast",HighContrast);
        settings.Set(Prefix+"shakeScale",ShakeScale); settings.Set(Prefix+"flashScale",FlashScale); settings.Set(Prefix+"rumble",Rumble);
    }
    /// <summary>Applies saved preferences. Missing or invalid entries keep their current value.</summary>
    public void LoadFrom(GameData settings)
    {
        static float Unit(double n,float fallback,double min,double max) => n>=min && n<=max ? (float)n : fallback;
        var locale=settings.GetText(Prefix+"locale",Locale);
        if(!string.IsNullOrWhiteSpace(locale)&&locale.Length<=64) Locale=locale;
        TextScale=Unit(settings.GetNumber(Prefix+"textScale",TextScale),TextScale,.75,2);
        HighContrast=settings.GetBool(Prefix+"highContrast",HighContrast);
        ShakeScale=Unit(settings.GetNumber(Prefix+"shakeScale",ShakeScale),ShakeScale,0,1);
        FlashScale=Unit(settings.GetNumber(Prefix+"flashScale",FlashScale),FlashScale,0,1);
        Rumble=settings.GetBool(Prefix+"rumble",Rumble);
    }
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
