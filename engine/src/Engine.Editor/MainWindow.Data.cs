using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Engine.Core;
using MoNoon.Dialogue;

namespace Engine.Editor;

public partial class MainWindow
{
    private Task EditData(string? relative) => EditValues(relative,false);
    private async Task EditValues(string? relative,bool table,bool dialogue = false)
    {
        if (project==null && !await Save()) return;
        var root=project!;
        var original=relative==null?null:File.ReadAllText(Path.Combine(root,relative));
        if (!table && original != null)
        {
            try { dialogue = GameData.Parse(original).GetText("format") == DialogueGraph.Format; }
            catch (InvalidDataException) { /* The generic data editor can repair malformed YAML. */ }
        }
        var folder=relative==null?CreationFolder("Assets"):ParentPath(relative);
        PrefabFiles? before = null;
        string Write(string stem,Action<string,bool> write)
        {
            if (!ProjectImport.ValidName(stem)) throw new InvalidDataException("Enter a valid asset name.");
            var target=relative ?? folder+"/"+stem+(table?GameTable.Extension:ProjectAssets.Data);
            var path=Path.Combine(root,target);
            if (original!=null && (!File.Exists(path) || File.ReadAllText(path)!=original))
                throw new IOException("This file changed on disk. Reopen it before saving.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            before = PrefabFileEdit.Read(path);
            write(path,original!=null);
            return target;
        }
        Window editor = dialogue ? new DialogueWindow(relative,original,Write) : new ValuesWindow(relative,original,table) { Write = Write };
        if (await editor.ShowDialog<string?>(this) is not {} saved) return;
        ProjectAssets.Scan(root);
        var savedPath = Path.Combine(root, saved);
        var edit = new PrefabFileEdit(savedPath, before!, PrefabFileEdit.Read(savedPath));
        if (edit.HasChanges) RecordEdit(Selected?.Id, asset: edit);
        BrowseFolder(ParentPath(saved),saved);
        RefreshComponentList();
        Status.Text=$"Saved {saved}.";
    }
}

internal sealed class ValuesWindow : Window
{
    private sealed class Field(string key,string kind,string text)
    {
        public string Key=key, Kind=kind, Text=text;
    }
    private sealed class Record(string key,IEnumerable<string> values)
    {
        public string Key=key;
        public List<string> Cells=values.ToList();
    }
    private readonly List<Field> fields=[];
    private readonly List<Record> records=[];
    private static readonly string[] DataTypes=["Text","Number","Boolean","YAML"];
    private readonly bool table;
    private readonly Grid sheet=new() { ColumnSpacing=5, RowSpacing=4, VerticalAlignment=VerticalAlignment.Top };
    private readonly TextBox assetName, version;
    private readonly TextBox source=new(){AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,VerticalContentAlignment=VerticalAlignment.Top,FontFamily=FontFamily.Parse("Menlo, Consolas, monospace")};
    private readonly Button mode=new(){Content="Edit YAML"};
    private readonly string? original;
    private string? originalValues;
    private bool raw;
    private readonly TextBlock error=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightSalmon,IsVisible=false};
    public Func<string,Action<string,bool>,string> Write { private get; set; }=null!;

    public ValuesWindow(string? relative,string? text,bool table)
    {
        this.table=table;original=text;
        Title=table?"Data Table":"Data Asset";Width=table?640:540;Height=370;MinWidth=385;MinHeight=255;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;
        assetName=Box("Asset name",relative==null?(table?"NewTable":"NewData"):Path.GetFileNameWithoutExtension(relative));
        assetName.IsReadOnly=relative!=null;
        version=Box("Version","1");version.Width=45;
        if(table)
        {
            var data=text==null?new GameTable(1,[new("value","Text")],[new("item",[TextValue("")])]):GameTable.Parse(text);
            fields.AddRange(data.Columns.Select(c=>new Field(c.Name,c.Type,"")));
            records.AddRange(data.Rows.Select(r=>new Record(r.Key,r.Cells.Select(Display))));
        }
        else
        {
            try { LoadFields(GameData.Parse(text??"version: 1\nvalues: {amount: 1}")); originalValues=BuildData().ToYaml(); }
            catch (InvalidDataException ex) when(text!=null) {raw=true;source.Text=text;ShowError(ex);}
        }
        var layout=new Grid{Margin=new Thickness(12),RowSpacing=8,RowDefinitions=new("Auto,Auto,*,Auto,Auto")};
        var nameRow=new Grid{ColumnDefinitions=new(table?"Auto,*":"Auto,*,Auto,Auto"),ColumnSpacing=7};
        Put(nameRow,Label("Name"),0,0);Put(nameRow,assetName,0,1);
        if(!table){Put(nameRow,Label("Version"),0,2);Put(nameRow,version,0,3);}
        Put(layout,nameRow,0,0);
        var bar=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};
        var add=ActionButton(table?"+ Row":"+ Field",()=>{
            if(table) records.Add(new(Unique("row",records.Select(r=>r.Key)),fields.Select(c=>Initial(c.Kind))));
            else fields.Add(new(Unique("field",fields.Select(f=>f.Key)),"Text",""));
            Draw();
        });
        bar.Children.Add(add);
        if(table) bar.Children.Add(ActionButton("+ Column",()=>{
            fields.Add(new(Unique("column",fields.Select(f=>f.Key)),"Text",""));foreach(var r in records) r.Cells.Add("");Draw();
        }));
        else bar.Children.Add(mode);
        Put(layout,bar,1,0);
        var scroller=new ScrollViewer{Content=sheet,HorizontalScrollBarVisibility=table?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled};
        Put(layout,scroller,2,0);Put(layout,source,2,0);AutomationProperties.SetName(source,"YAML");
        void View(){scroller.IsVisible=!raw;source.IsVisible=raw;add.IsEnabled=version.IsEnabled=!raw;mode.Content=raw?"Edit fields":"Edit YAML";}
        mode.Click+=(_,_)=>Attempt(()=>{
            if(raw){LoadFields(GameData.Parse(source.Text??""));Draw();}
            else {var assembled=BuildData().ToYaml(); source.Text=assembled==originalValues && original!=null?original:assembled;}
            raw=!raw; View();
        });
        Put(layout,error,3,0);
        var cancel=ActionButton("Cancel",()=>Close());cancel.IsCancel=true;
        var save=ActionButton("Save",()=>Attempt(()=>{
            Action<string,bool> writer;
            if(table) writer=BuildTable().Save;
            else {
                var yaml=raw?source.Text??"":BuildData().ToYaml();
                if(!raw && yaml==originalValues && original!=null) yaml=original;
                writer=(path,overwrite)=>GameData.SaveYaml(path,yaml,overwrite);
            }
            Close(Write(assetName.Text?.Trim()??"",writer));
        }));save.Classes.Add("primary");
        Put(layout,new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=6,Children={cancel,save}},4,0);
        Content=layout;Draw();View();
    }
    private static string Unique(string stem,IEnumerable<string> names) {var used=names.ToHashSet();int i=1;while(used.Contains(stem+i))i++;return stem+i;}
    private static string Initial(string kind)=>kind=="Number"?"0":kind=="Boolean"?"false":"";
    private static JsonElement TextValue(string s)=>JsonSerializer.SerializeToElement(s);
    private static string Display(JsonElement e)=>e.ValueKind switch{JsonValueKind.String=>e.GetString()!,JsonValueKind.Object or JsonValueKind.Array =>GameData.FormatValue(e),_=>e.GetRawText()};
    private static string Kind(JsonElement e)=>e.ValueKind switch{JsonValueKind.String=>"Text",JsonValueKind.Number=>"Number",JsonValueKind.True or JsonValueKind.False=>"Boolean",_=>"YAML"};
    private static JsonElement Value(string name,string kind,string text)
    {
        if(kind=="Text")return TextValue(text);
        if(kind=="YAML")return GameData.ParseValue(text);
        if(kind=="Boolean" && bool.TryParse(text,out var b))return JsonSerializer.SerializeToElement(b);
        if(kind=="Number")
        {
            try{using var doc=JsonDocument.Parse(text);var e=doc.RootElement;if(e.ValueKind==JsonValueKind.Number && e.TryGetDouble(out var n) && double.IsFinite(n))return e.Clone();}
            catch(JsonException){}
        }
        throw new InvalidDataException($"{name}: enter a valid {kind.ToLowerInvariant()}.");
    }
    private GameTable BuildTable()=>new GameTable(1,fields.Select(f=>new TableColumn(f.Key.Trim(),f.Kind)).ToArray(),
        records.Select(r=>new TableRow(r.Key.Trim(),r.Cells.Select((c,i)=>Value(r.Key+" / "+fields[i].Key,fields[i].Kind,c)).ToArray())).ToArray()).Validate();
    private GameData BuildData()
    {
        if(!int.TryParse(version.Text,out int v)||v<1)throw new InvalidDataException("Version must be a positive integer.");
        var data=new GameData{Version=v};
        foreach(var f in fields)
            if(string.IsNullOrWhiteSpace(f.Key)||!data.Values.TryAdd(f.Key,Value(f.Key,f.Kind,f.Text)))throw new InvalidDataException("Use a unique, nonempty field name.");
        return data;
    }
    private void LoadFields(GameData data)
    {
        version.Text=data.Version.ToString(CultureInfo.InvariantCulture);fields.Clear();
        fields.AddRange(data.Values.Select(p=>new Field(p.Key,Kind(p.Value),Display(p.Value))));
    }
    private void Draw()
    {
        sheet.Children.Clear();sheet.RowDefinitions.Clear();sheet.ColumnDefinitions.Clear();
        void Row()=>sheet.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Row();
        if(table)
        {
            sheet.ColumnDefinitions.Add(new(110,GridUnitType.Pixel));
            foreach(var _ in fields)sheet.ColumnDefinitions.Add(new(145,GridUnitType.Pixel));
            sheet.ColumnDefinitions.Add(new(GridLength.Auto));
            Put(sheet,Label("Row key"),0,0);
            for(int i=0;i<fields.Count;i++){
                int col=i;var f=fields[i];
                var header=new Grid {RowDefinitions=new("Auto,Auto"),ColumnDefinitions=new("*,Auto"),RowSpacing=4,ColumnSpacing=3};
                Put(header,Box("Column name",f.Key,v=>f.Key=v),0,0);
                var remove=Remove("Remove column "+f.Key,()=>{fields.RemoveAt(col);foreach(var r in records)r.Cells.RemoveAt(col);Draw();});
                remove.IsEnabled=fields.Count>1;Put(header,remove,0,1);
                var picker=TypePicker(f,GameTable.Types);Grid.SetColumnSpan(picker,2);Put(header,picker,1,0);
                Put(sheet,header,0,col+1);
            }
            foreach(var r in records){Row();int y=sheet.RowDefinitions.Count-1;Put(sheet,Box("Row key",r.Key,v=>r.Key=v),y,0);
                for(int i=0;i<fields.Count;i++){int col=i;Put(sheet,Box(r.Key+" / "+fields[i].Key,r.Cells[i],v=>r.Cells[col]=v),y,i+1);}
                Put(sheet,Remove("Remove row "+r.Key,()=>{records.Remove(r);Draw();}),y,fields.Count+1);}
        }
        else
        {
            sheet.ColumnDefinitions=new("1.2*,98,2*,Auto");
            Put(sheet,Label("Field"),0,0);Put(sheet,Label("Type"),0,1);Put(sheet,Label("Value"),0,2);
            foreach(var f in fields){Row();int y=sheet.RowDefinitions.Count-1;
                Put(sheet,Box("Field name",f.Key,v=>f.Key=v),y,0);
                var box=Box(f.Key+" value",f.Text,v=>f.Text=v);box.AcceptsReturn=f.Kind=="YAML";
                Put(sheet,TypePicker(f,DataTypes,()=>box.AcceptsReturn=f.Kind=="YAML"),y,1);
                Put(sheet,box,y,2);Put(sheet,Remove("Remove field "+f.Key,()=>{fields.Remove(f);Draw();}),y,3);}
        }
    }
    private static ComboBox TypePicker(Field f,IReadOnlyList<string> kinds,Action? changed=null)
    {
        var box=new ComboBox{ItemsSource=kinds,SelectedItem=f.Kind,HorizontalAlignment=HorizontalAlignment.Stretch};AutomationProperties.SetName(box,"Type of "+f.Key);
        box.SelectionChanged+=(_,_)=>{if(box.SelectedItem is string s){f.Kind=s;changed?.Invoke();}};return box;
    }
    private static TextBox Box(string label,string text,Action<string>? change=null){
        var box=new TextBox{Text=text};AutomationProperties.SetName(box,label);
        if(change!=null)box.TextChanged+=(_,_)=>change(box.Text??"");return box;
    }
    private static Button ActionButton(string text,Action action){var b=new Button{Content=text};b.Click+=(_,_)=>action();return b;}
    private static Button Remove(string label,Action action){var b=ActionButton("×",action);AutomationProperties.SetName(b,label);ToolTip.SetTip(b,label);return b;}
    private static TextBlock Label(string s)=>new(){Text=s,Classes={"label"}};
    private void ShowError(Exception ex){error.Text=ex.Message;error.IsVisible=true;}
    private void Attempt(Action action){try{error.IsVisible=false;action();}catch(Exception e) when(e is InvalidDataException or IOException or UnauthorizedAccessException or FormatException or JsonException){ShowError(e);}}
    private static void Put(Grid g,Control c,int row,int col){Grid.SetRow(c,row);Grid.SetColumn(c,col);g.Children.Add(c);}
}
