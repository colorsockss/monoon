using Engine.Core;
using SkiaSharp;
using System.Security.Cryptography;

namespace Engine.Desktop;

public static class AtlasPacker
{
    private const int Limit = 4096;
    private sealed record Part(Guid Id, string Path, SpriteRegion Crop, SpriteCanvas Canvas, string Hash);
    public static SpriteAtlas Pack(string root, string file, IEnumerable<Guid> selection, bool overwrite)
    {
        var assets = ProjectAssets.Scan(root);
        var used = assets.Entries.Where(a => a.Path.EndsWith(SpriteAtlas.Extension))
            .Select(a => Path.Combine(root,a.Path)).Where(p => File.Exists(p) && p != file)
            .SelectMany(p => SpriteAtlas.Load(p).Entries).Select(c => c.Sprite).ToHashSet();
        var parts = new List<Part>();
        foreach (var id in selection.Distinct())
        {
            var path = assets.PathFor(id);
            if (path == null || !SpriteAssets.IsReference(path) || used.Contains(id))
                throw new InvalidDataException($"Missing PNG or already packed in another atlas: {path ?? id.ToString()}.");
            var full = Path.Combine(root,path);
            using var bitmap = Read(full,out var hash);
            int l = bitmap.Width, t = bitmap.Height, r = -1, b = -1;
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x,y).Alpha != 0) { l = Math.Min(l,x); t = Math.Min(t,y); r = Math.Max(r,x); b = Math.Max(b,y); }
            if (r < 0) l=t=r=b=0;
            parts.Add(new(id,full,new(l,t,r-l+1,b-t+1),new(bitmap.Width,bitmap.Height,l,t),hash));
        }
        if (parts.Count == 0) throw new InvalidDataException("Select at least one PNG.");
        var (cells,right,bottom)=Layout(parts.Select(p => (p.Id,p.Crop.Width,p.Crop.Height,p.Canvas)));
        var byId=parts.ToDictionary(p => p.Id);
        using var sheet = new SKBitmap(right,bottom,SKColorType.Rgba8888,SKAlphaType.Unpremul);
        sheet.Erase(SKColors.Transparent);
        foreach (var entry in cells)
        {
            var p=byId[entry.Sprite]; var cell=entry.Source;
            using var bitmap=Read(p.Path,out var hash);
            if (p.Hash!=hash) throw new IOException($"PNG changed while packing: {p.Path}");
            for (int y=-1;y<=cell.Height;y++)
            for (int x=-1;x<=cell.Width;x++)
                sheet.SetPixel(cell.X+x,cell.Y+y,bitmap.GetPixel(p.Crop.X+Math.Clamp(x,0,cell.Width-1),p.Crop.Y+Math.Clamp(y,0,cell.Height-1)));
        }
        using var png=sheet.Encode(SKEncodedImageFormat.Png,100);
        var bytes=png.ToArray();
        var atlas=new SpriteAtlas(Convert.ToHexString(SHA256.HashData(bytes)),cells);
        var dest=Path.Combine(root,atlas.ImagePath());
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest))
        {
            var stage=dest+"."+Guid.NewGuid().ToString("N")+".tmp";
            try { File.WriteAllBytes(stage,bytes); File.Move(stage,dest,false); }
            finally { File.Delete(stage); }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        atlas.Save(file,overwrite);
        return atlas;
    }
    // Shelf packing with a 1-pixel clamped border around each cell. Pack and the game build's repack share it.
    private static (AtlasEntry[] Cells,int Width,int Height) Layout(IEnumerable<(Guid Id,int Width,int Height,SpriteCanvas Canvas)> sprites)
    {
        var parts = sprites.OrderByDescending(p => p.Height).ThenByDescending(p => p.Width).ThenBy(p => p.Id).ToList();
        var width = Math.Min(Limit,Math.Max(parts.Max(p => p.Width+2),(int)Math.Ceiling(Math.Sqrt(parts.Sum(p => (double)(p.Width+2)*(p.Height+2))))));
        while (true)
        {
            var cells = new List<AtlasEntry>(); int x=0,y=0,row=0,right=0;
            foreach (var p in parts)
            {
                int w=p.Width+2,h=p.Height+2;
                if (w>Limit || h>Limit) throw new InvalidDataException("Trimmed sprite exceeds 4094 pixels.");
                if (x+w>width) { x=0; y+=row; row=0; }
                cells.Add(new(p.Id,new(x+1,y+1,p.Width,p.Height),p.Canvas));
                x+=w; right=Math.Max(right,x); row=Math.Max(row,h);
            }
            if (y+row<=Limit) return (cells.ToArray(),right,y+row);
            if (width==Limit) throw new InvalidDataException("Sprites do not fit a 4096 × 4096 atlas.");
            width=Math.Min(Limit,width*2);
        }
    }

    // Roadmap 15.13: a game build ships only the cells it uses. They are laid out again as Pack would and copied from the
    // source sheet with the same clamped border, so every kept sprite samples the same texels. 'keep' must not be empty.
    internal static (AtlasEntry[] Cells,int Width,int Height) Layout(SpriteAtlas source,ISet<Guid> keep) =>
        Layout(source.Entries.Where(e => keep.Contains(e.Sprite)).Select(e => (e.Sprite,e.Source.Width,e.Source.Height,e.Canvas)));

    internal static unsafe void WriteSheet(string sourceSheet,SpriteAtlas source,(AtlasEntry[] Cells,int Width,int Height) layout,string output)
    {
        using var codec=SKCodec.Create(sourceSheet) ?? throw new InvalidDataException($"Invalid PNG: {sourceSheet}.");
        // Unpremultiplied RGBA both ways: the copy is byte-exact, with no premultiply rounding.
        using var from=new SKBitmap(new SKImageInfo(codec.Info.Width,codec.Info.Height,SKColorType.Rgba8888,SKAlphaType.Unpremul));
        if (codec.GetPixels(from.Info,from.GetPixels())!=SKCodecResult.Success) throw new InvalidDataException($"Could not decode {sourceSheet}.");
        using var sheet=new SKBitmap(layout.Width,layout.Height,SKColorType.Rgba8888,SKAlphaType.Unpremul);
        sheet.Erase(SKColors.Transparent);
        var old=source.Entries.ToDictionary(e => e.Sprite,e => e.Source);
        uint* src=(uint*)from.GetPixels(), dst=(uint*)sheet.GetPixels();
        int srcRow=from.RowBytes/4, dstRow=sheet.RowBytes/4;
        foreach (var cell in layout.Cells)
        {
            var o=old[cell.Sprite]; var n=cell.Source;
            if (!o.Fits(from.Width,from.Height)) throw new InvalidDataException($"Atlas cell {cell.Sprite} lies outside {sourceSheet}.");
            for (int y=-1;y<=n.Height;y++)
            for (int x=-1;x<=n.Width;x++)
                dst[(n.Y+y)*dstRow+n.X+x]=src[(o.Y+Math.Clamp(y,0,n.Height-1))*srcRow+o.X+Math.Clamp(x,0,n.Width-1)];
        }
        using var png=sheet.Encode(SKEncodedImageFormat.Png,100);
        File.WriteAllBytes(output,png.ToArray());
    }

    private static SKBitmap Read(string path,out string hash)
    {
        using var stream=File.OpenRead(path);
        hash=Convert.ToHexString(SHA256.HashData(stream)); stream.Position=0;
        using var codec=SKCodec.Create(stream) ?? throw new InvalidDataException($"Invalid PNG: {path}.");
        if ((long)codec.Info.Width*codec.Info.Height>16777216) throw new InvalidDataException($"Source exceeds 16 megapixels: {path}.");
        return SKBitmap.Decode(codec) ?? throw new InvalidDataException($"Could not decode {path}.");
    }
}
