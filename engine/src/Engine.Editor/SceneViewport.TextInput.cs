using Avalonia;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Engine.Core;

namespace Engine.Editor;

public sealed partial class SceneViewport
{
    private GameIme? ime;
    private Guid? editing;
    private void ConfigureIme()
    {
        ime=new(this);
        TextInputMethodClientRequested+=(_,e)=>{if(game?.InputUi?.Editing!=null){e.Client=ime;e.Handled=true;}};
        TextInput+=(_,e)=>{if(e.Text!=null && game?.InputUi?.CommitText(e.Text)==true){e.Handled=true;ime.Notify();RefreshScene();}};
    }
    private void SyncIme()
    {
        var next=game?.InputUi?.Editing;if(next==editing)return;
        editing=next;
        RaiseEvent(new TextInputMethodClientRequeryRequestedEventArgs {RoutedEvent=InputMethod.TextInputMethodClientRequeryRequestedEvent});
        ime?.Notify();
    }
    private sealed class GameIme(SceneViewport viewport):TextInputMethodClient
    {
        private ScreenUi? Ui=>viewport.game?.InputUi;
        public override Visual TextViewVisual=>viewport;
        public override bool SupportsPreedit=>true;
        public override bool SupportsSurroundingText=>true;
        public override string SurroundingText=>Ui?.Editing is Guid id?Ui.Value(id)??"":"";
        public override TextSelection Selection {get=>new(Ui?.Caret??0,Ui?.Caret??0);set=>Ui?.SetCaret(value.End);}
        public override Rect CursorRectangle
        {
            get
            {
                if(Ui?.Editing is not Guid id || Ui.Bounds(id) is not {} box)return default;
                var o=viewport.Origin;var z=viewport.Scale;
                return new(o.X+box.Left*z,o.Y+box.Top*z,box.Width*z,box.Height*z);
            }
        }
        public override void SetPreeditText(string? text){Ui?.Compose(text??"");viewport.RefreshScene();}
        internal void Notify(){RaiseCursorRectangleChanged();RaiseSelectionChanged();RaiseSurroundingTextChanged();}
    }
}
