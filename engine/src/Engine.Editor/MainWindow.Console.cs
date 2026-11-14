using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private sealed record ConsoleRow(DateTime At,GameLogLevel Level,string Message,string? File,int Line);
    private const int ConsoleLimit=1000;
    private readonly Queue<ConsoleRow> consoleRows=new();
    private long consoleRevision;
    private Window? consoleWindow;
    private void WriteLog(GameLogEntry entry)
    {
        // A script may emit from a worker. The queue is bounded even when the window is closed.
        string? path=null;
        if(project is {} root && entry.Source is {Length:>0} source && !Path.IsPathRooted(source) &&
            source.EndsWith(".cs",StringComparison.Ordinal) && ProjectScripts.ValidReference(source[..^3])){
            var candidate=Path.Combine(root,"Scripts",source);
            if(File.Exists(candidate))path=candidate;
        }
        var msg=entry.Message??"";
        lock(consoleRows){
            if(consoleRows.Count==ConsoleLimit)consoleRows.Dequeue();
            consoleRows.Enqueue(new(DateTime.Now,entry.Level,msg.Length<=4096?msg:msg[..4095]+"…",path,entry.Line));consoleRevision++;
        }
    }
    private void LogError(string message)
    {
        foreach(var line in message.Split('\n',StringSplitOptions.RemoveEmptyEntries)){
            var match=Regex.Match(line,@"(?:^|: )(?<path>[\w ./-]+\.cs)\((?<line>\d+),\d+\)");
            WriteLog(new(GameLogLevel.Error,line.Trim(),match.Success?match.Groups["path"].Value.Trim():null,
                match.Success&&int.TryParse(match.Groups["line"].Value,out int n)?n:0));
        }
    }
    private void ShowConsole()
    {
        if(consoleWindow!=null){consoleWindow.Activate();return;}
        var dialog=consoleWindow=new Window{Title="Console",Width=660,Height=330,MinWidth=345,MinHeight=210,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var root=new Grid{Margin=new Thickness(10),RowDefinitions=new("Auto,*,Auto"),RowSpacing=6};
        var bar=new Grid{ColumnDefinitions=new("Auto,*,Auto"),ColumnSpacing=5};
        var level=new ComboBox{ItemsSource=new[]{"All levels","Info","Warning","Error"},SelectedIndex=0};
        var search=new TextBox{Watermark="Filter messages…"};AutomationProperties.SetName(search,"Filter messages");
        var clear=new Button{Content="Clear"};bar.Children.Add(level);Grid.SetColumn(search,1);bar.Children.Add(search);Grid.SetColumn(clear,2);bar.Children.Add(clear);root.Children.Add(bar);
        var list=new ListBox();Grid.SetRow(list,1);root.Children.Add(list);
        var foot=new TextBlock{Classes={"label"},TextTrimming=TextTrimming.CharacterEllipsis};Grid.SetRow(foot,2);root.Children.Add(foot);
        list.ItemTemplate=new FuncDataTemplate<ConsoleRow>((row,_)=>{
            if(row==null)return null; // Virtualized rows are detached when a filter changes.
            var text=new TextBlock{Text=$"{row.At:HH:mm:ss}   {row.Level}   {row.Message}",TextWrapping=TextWrapping.Wrap};
            if(row.Level!=GameLogLevel.Info) text.Foreground=row.Level==GameLogLevel.Error?Brushes.LightSalmon:Brushes.Gold;
            if(row.File!=null)ToolTip.SetTip(text,$"{row.File}:{row.Line} · Double-click to open");
            return text;
        });
        long shown=-1;
        void Draw(bool force=false){
            ConsoleRow[] all;lock(consoleRows){if(!force&&shown==consoleRevision)return;shown=consoleRevision;all=consoleRows.ToArray();}
            var previous=list.SelectedItem;var query=search.Text??"";
            var rows=all.Where(r=>(level.SelectedIndex<=0 || r.Level.ToString()==level.SelectedItem as string)&&r.Message.Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray();
            list.ItemsSource=rows;
            if(previous is ConsoleRow r && rows.Contains(r))list.SelectedItem=r;
            else if(rows.Length>0)list.ScrollIntoView(rows.Length-1);
            foot.Text=$"{rows.Length} shown · {all.Length} retained / {ConsoleLimit}   ·   Double-click a source message to open";
        }
        clear.Click+=(_,_)=>{lock(consoleRows){consoleRows.Clear();consoleRevision++;}Draw();};
        level.SelectionChanged+=(_,_)=>Draw(true);search.TextChanged+=(_,_)=>Draw(true);
        list.DoubleTapped+=async (_,_)=>{if(list.SelectedItem is ConsoleRow {File:{} file} row)
            try{await ExternalScriptEditor.Open(file,row.Line);}catch(Exception ex){LogError(ex.Message);}};
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=(_,_)=>Draw();
        dialog.Closed+=(_,_)=>{timer.Stop();consoleWindow=null;};
        dialog.Content=root;Draw();dialog.Show(this);timer.Start();
    }
}
