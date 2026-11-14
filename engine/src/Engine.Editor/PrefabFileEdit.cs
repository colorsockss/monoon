namespace Engine.Editor;

internal sealed record PrefabFiles(byte[]? Document,byte[]? Metadata);

// The scene and the source file are one Undo step. Never erase a later external edit.
internal sealed record PrefabFileEdit(string Path,PrefabFiles Before,PrefabFiles After)
{
    public static PrefabFiles Read(string path)=>new(ReadFile(path),ReadFile(path+".meta"));
    private static byte[]? ReadFile(string path)=>File.Exists(path)?File.ReadAllBytes(path):null;
    private static bool Same(byte[]? a,byte[]? b)=>a==null?b==null:b!=null && a.AsSpan().SequenceEqual(b);
    public bool HasChanges => !Same(Before.Document,After.Document) || !Same(Before.Metadata,After.Metadata);
    public void Restore(bool redo)
    {
        var expected=redo?Before:After;
        var current=Read(Path);
        if(!Same(expected.Document,current.Document)||!Same(expected.Metadata,current.Metadata))
            throw new IOException($"Cannot restore {System.IO.Path.GetFileName(Path)} because its source changed on disk.");
        RestoreFiles(Path,redo?After:Before);
    }
    public static void RestoreFiles(string path,PrefabFiles files)
    {
        void Write(string target,byte[]? value)
        {
            if(value==null){File.Delete(target);return;}
            string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {File.WriteAllBytes(temp,value);File.Move(temp,target,true);}
            finally {File.Delete(temp);}
        }
        Write(path,files.Document);Write(path+".meta",files.Metadata);
    }
}
