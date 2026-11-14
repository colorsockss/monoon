using System;
using Engine.Core;

public sealed class PlayerOptions : GameBehaviour
{
    public int Action;
    public Guid NameField, Form, Receipt;

    public override void UpdateUi(UiTick tick)
    {
        if (Action==4 && tick.Ui.Preedit.Length!=0) tick.Session.Set("sawComposition");
    }

    public override void OnUiClick(UiClick click)
    {
        var ui=click.Ui;
        var options=ui.Preferences;
        switch (Action)
        {
            case 1:
                options.Locale=options.Locale switch { "ko-KR"=>"ja", "ja"=>"en", _=>"ko-KR" };
                break;
            case 2:
                options.TextScale=options.TextScale switch { 1=>1.5f, 1.5f=>2, _=>1 };
                break;
            case 3:
                options.HighContrast=!options.HighContrast;
                break;
            case 4:
                var data=new GameData();
                string name=ui.Value(NameField)??"";
                data.Set("name",name);data.Set("locale",options.Locale);data.Set("textScale",options.TextScale);
                data.Set("highContrast",options.HighContrast);data.Set("scroll",ui.ScrollOffset(Form));
                data.Set("saveFocused",ui.Focused==click.ObjectId);data.Set("sawComposition",click.Session.Has("sawComposition"));
                if (click.Session.Storage.IsAvailable) click.Session.Storage.SaveSlot(1,data);
                var saved=options.Locale switch { "ja"=>"保存しました", "en"=>"Saved", _=>"저장 완료" };
                ui.SetText(Receipt,$"{saved}   ·   {name}   ·   {options.TextScale:P0}");
                break;
        }
    }
}
