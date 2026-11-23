using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    private void WriteLog(GameLogEntry entry)
    {
        // A script may emit from a worker. The queue is bounded even when the window is closed.
        string? path=null;
        if(project is {} root && entry.Source is {Length:>0} source && source.EndsWith(".cs",StringComparison.Ordinal)){
            if (Path.IsPathFullyQualified(source)) source = "./" + Path.GetRelativePath(root,source).Replace(Path.DirectorySeparatorChar,'/');
            bool projectRelative = source.StartsWith("./", StringComparison.Ordinal);
            var relative = projectRelative ? source[2..] : source;
            if (relative.Split('/').All(ProjectImport.ValidName))
            {
                var candidate = Path.Combine(projectRelative ? root : Path.Combine(root,"Scripts"), relative);
                if (File.Exists(candidate)) path = candidate;
            }
        }
        var msg=entry.Message??"";
        lock(consoleRows){
            if(consoleRows.Count==ConsoleLimit)consoleRows.Dequeue();
            consoleRows.Enqueue(new(DateTime.Now,entry.Level,msg.Length<=4096?msg:msg[..4095]+"…",path,entry.Line));consoleRevision++;
        }
    }
    private void LogError(string message)
    {
        // Scene Play and Editor/ compile with absolute paths; show them project-relative so the file name stays visible.
        if (project != null) message = message.Replace(Path.GetFullPath(project) + Path.DirectorySeparatorChar, "./");
        foreach(var line in message.Split('\n',StringSplitOptions.RemoveEmptyEntries)){
            var match=Regex.Match(line,@"(?:^|: )(?<path>[\w ./-]+\.cs)\((?<line>\d+),\d+\)");
            WriteLog(new(GameLogLevel.Error,line.Trim(),match.Success?match.Groups["path"].Value.Trim():null,
                match.Success&&int.TryParse(match.Groups["line"].Value,out int n)?n:0));
        }
    }
    private void ShowConsole()=>OpenPanel("Console");
    // One list row; Count > 1 only when identical messages are collapsed.
    private sealed record ConsoleItem(ConsoleRow Row,int Count);
    private static (GameLogLevel,string,string?,int) ConsoleKey(ConsoleRow row)=>(row.Level,row.Message,row.File,row.Line);
    // Built once; the panel keeps collecting while it is closed or behind another tab.
    private void InitializeConsole()
    {
        var root=new Grid{RowDefinitions=new("Auto,*,Auto,Auto")};
        // Toolbar: Clear, Collapse, search, then one filter toggle per severity with its retained count.
        var bar=new Grid{ColumnDefinitions=new("Auto,Auto,*,Auto,Auto,Auto"),ColumnSpacing=EditorTokens.Gap};
        var clear=new Button{Content="Clear",Height=22,MinHeight=0,Padding=new(8,0),VerticalAlignment=VerticalAlignment.Center};
        ToolTip.SetTip(clear,"Remove all retained messages");
        var collapse=new ToggleButton{Content="Collapse",Classes={"severity"}};
        ToolTip.SetTip(collapse,"Show identical messages once, with a count");AutomationProperties.SetName(collapse,"Collapse identical messages");
        var search=new TextBox{Watermark="Search messages…",Classes={"toolbarSearch"},
            InnerLeftContent=new PathIcon{Theme=(Avalonia.Styling.ControlTheme)this.FindResource("EditorIcon")!,Margin=new(6,0,0,0),Foreground=EditorTokens.Brush("EditorMuted"),Data=EditorTokens.Geometry("IconSearch")}};
        AutomationProperties.SetName(search,"Search messages");ToolTip.SetTip(search,"Show messages containing this text. Esc clears.");
        search.KeyDown+=(_,e)=>{if(e.Key==Avalonia.Input.Key.Escape){search.Text="";e.Handled=true;}};
        var levels=new[]{GameLogLevel.Info,GameLogLevel.Warning,GameLogLevel.Error};
        var counts=new Dictionary<GameLogLevel,TextBlock>();var filters=new Dictionary<GameLogLevel,ToggleButton>();
        ToggleButton Severity(GameLogLevel level)
        {
            var icon=EditorTokens.Icon(level==GameLogLevel.Error?"IconError":level==GameLogLevel.Warning?"IconWarning":"IconInfo");
            if(level!=GameLogLevel.Info)icon.Foreground=EditorTokens.Brush(level==GameLogLevel.Error?"EditorError":"EditorWarning");
            var count=new TextBlock{Text="0",VerticalAlignment=VerticalAlignment.Center,MinWidth=16,TextAlignment=TextAlignment.Right};
            var toggle=new ToggleButton{Classes={"severity"},IsChecked=true,Content=new StackPanel{Orientation=Orientation.Horizontal,Spacing=EditorTokens.Gap,Children={icon,count}}};
            var name=level==GameLogLevel.Info?"Log":level.ToString();
            AutomationProperties.SetName(toggle,$"Show {name.ToLowerInvariant()} messages");ToolTip.SetTip(toggle,$"Show or hide {name.ToLowerInvariant()} messages");
            counts[level]=count;filters[level]=toggle;return toggle;
        }
        bar.Children.Add(clear);Grid.SetColumn(collapse,1);bar.Children.Add(collapse);Grid.SetColumn(search,2);bar.Children.Add(search);
        for(int i=0;i<levels.Length;i++){var t=Severity(levels[i]);Grid.SetColumn(t,3+i);bar.Children.Add(t);}
        root.Children.Add(new Border{Classes={"panelHeader"},Child=bar});
        var list=new ListBox{Margin=new(4,3)};AutomationProperties.SetName(list,"Console messages");Grid.SetRow(list,1);root.Children.Add(list);
        // The selected message in full (wrapped, selectable) with its source.
        var detailText=new SelectableTextBlock{TextWrapping=TextWrapping.Wrap,Margin=new(8,4)};
        var detail=new Border{BorderBrush=EditorTokens.Brush("EditorDivider"),BorderThickness=new(0,1,0,0),IsVisible=false,
            Child=new ScrollViewer{MaxHeight=120,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=detailText}};
        AutomationProperties.SetName(detailText,"Selected message");Grid.SetRow(detail,2);root.Children.Add(detail);
        var foot=new TextBlock{Classes={"label"},TextTrimming=TextTrimming.CharacterEllipsis,Margin=new(8,2,8,4)};Grid.SetRow(foot,3);root.Children.Add(foot);
        list.ItemTemplate=new FuncDataTemplate<ConsoleItem>((item,_)=>{
            if(item==null)return null; // Virtualized rows are detached when a filter changes.
            var row=item.Row;
            var line=new Grid{ColumnDefinitions=new("16,*,Auto"),ColumnSpacing=EditorTokens.Gap};
            var icon=EditorTokens.Icon(row.Level==GameLogLevel.Error?"IconError":row.Level==GameLogLevel.Warning?"IconWarning":"IconInfo");
            icon.Foreground=EditorTokens.Brush(row.Level==GameLogLevel.Error?"EditorError":row.Level==GameLogLevel.Warning?"EditorWarning":"EditorMuted");
            line.Children.Add(icon);
            var first=row.Message.Split('\n',2)[0];
            var text=new TextBlock{Text=$"{row.At:HH:mm:ss}   {first}",TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};
            if(row.Level!=GameLogLevel.Info) text.Classes.Add(row.Level==GameLogLevel.Error?"error":"warning");
            ToolTip.SetTip(text,row.File!=null?$"{row.File}:{row.Line} · Double-click to open":first);
            Grid.SetColumn(text,1);line.Children.Add(text);
            if(item.Count>1){var badge=new Border{Classes={"consoleCount"},Child=new TextBlock{Text=item.Count.ToString(),Classes={"label"}}};Grid.SetColumn(badge,2);line.Children.Add(badge);}
            return line;
        });
        long shown=-1;
        void Draw(bool force=false){
            ConsoleRow[] all;lock(consoleRows){if(!force&&shown==consoleRevision)return;shown=consoleRevision;all=consoleRows.ToArray();}
            foreach(var level in levels)counts[level].Text=all.Count(r=>r.Level==level).ToString();
            var previous=(list.SelectedItem as ConsoleItem)?.Row;var query=search.Text??"";
            var matching=all.Where(r=>filters[r.Level].IsChecked==true&&r.Message.Contains(query,StringComparison.OrdinalIgnoreCase));
            var rows=collapse.IsChecked==true
                ? matching.GroupBy(ConsoleKey).Select(g=>new ConsoleItem(g.Last(),g.Count())).ToArray()
                : matching.Select(r=>new ConsoleItem(r,1)).ToArray();
            list.ItemsSource=rows;
            var keep=previous==null?null:rows.FirstOrDefault(i=>collapse.IsChecked==true?ConsoleKey(i.Row)==ConsoleKey(previous):i.Row==previous);
            if(keep!=null)list.SelectedItem=keep;
            else if(rows.Length>0)list.ScrollIntoView(rows.Length-1);
            foot.Text=$"{rows.Length} shown · {all.Length} retained / {ConsoleLimit}   ·   Double-click a source message to open";
        }
        list.SelectionChanged+=(_,_)=>{
            detail.IsVisible=list.SelectedItem is ConsoleItem;
            if(list.SelectedItem is ConsoleItem {Row:var row} item)
                detailText.Text=$"{row.At:HH:mm:ss}   {(row.Level==GameLogLevel.Info?"Log":row.Level)}{(item.Count>1?$"   × {item.Count}":"")}{(row.File!=null?$"   {row.File}:{row.Line}":"")}\n{row.Message}";
        };
        clear.Click+=(_,_)=>{lock(consoleRows){consoleRows.Clear();consoleRevision++;}Draw();};
        collapse.IsCheckedChanged+=(_,_)=>Draw(true);search.TextChanged+=(_,_)=>Draw(true);
        foreach(var toggle in filters.Values)toggle.IsCheckedChanged+=(_,_)=>Draw(true);
        list.DoubleTapped+=async (_,_)=>{if(list.SelectedItem is ConsoleItem {Row.File:{} file} item)
            try{await ExternalScriptEditor.Open(file,item.Row.Line);}catch(Exception ex){LogError(ex.Message);}};
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=(_,_)=>{if(root.IsEffectivelyVisible)Draw();};
        Closed+=(_,_)=>timer.Stop();
        AddPanel("Console",root).MinWidth=345;Draw();timer.Start();
    }
}
