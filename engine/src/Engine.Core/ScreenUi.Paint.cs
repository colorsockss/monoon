using System.Drawing;
using System.Globalization;

namespace Engine.Core;

public sealed partial class ScreenUi
{
    public void Draw(ICanvas2D canvas,float x=0,float y=0,float scale=1)
    {
        canvas.UseFonts(Language.Fonts);
        Arrange(canvas);
        if (inputModes.Count == 0) { DrawLayer(canvas, x, y, scale, nodes); return; }
        // Paint each panel, including its scrollbars, before painting the panel above it.
        for (int start = 0; start < nodes.Length;)
        {
            int layer = PaintLayer(nodes[start]), end = start + 1;
            while (end < nodes.Length && PaintLayer(nodes[end]) == layer) end++;
            DrawLayer(canvas, x, y, scale, nodes.AsSpan(start, end - start));
            start = end;
        }
    }

    private void DrawLayer(ICanvas2D canvas, float x, float y, float scale, ReadOnlySpan<Node> widgets)
    {
        RectangleF At(RectangleF b)=>new(x+b.X*scale,y+b.Y*scale,b.Width*scale,b.Height*scale);
        foreach(var n in widgets)
        {
            if(!Shown(n))continue;
            using var clip=n.Clip!=screen?canvas.PushClip(At(n.Clip)):null;
            var box=At(n.Box);var id=n.Object.Id;
            if(n.Button!=null||n.Field!=null)
                canvas.FillRectangle(box,!Enabled(n)?Color.DimGray:pressed==id?Color.DarkSlateGray:Preferences.HighContrast?Color.Black:
                    focus==id?Color.SteelBlue:Color.FromArgb(59,83,107));
            if(n.Picture!=null)
            {
                if(n.Picture.Sprite==null&&SceneObject.TryColor(n.Picture.Color,out var c))canvas.FillRectangle(box,c);
                else if(n.Image==null||!canvas.DrawSprite(n.Image,box))canvas.FillRectangle(box,Color.Magenta);
            }
            var label=n.Label;
            if(label!=null||n.Field!=null)
            {
                float size=(label?.Size??16)*Preferences.TextScale*scale, tx=box.Left+(label?.InsetX??5)*scale, ty=box.Top+(label?.InsetY??2)*scale;
                var ink=label!=null&&SceneObject.TryColor(label.Color,out var color)?color:Color.White;
                var text=Label(n);bool editing=edit==id;
                if(Preferences.HighContrast){canvas.FillRectangle(box,Color.Black);ink=Color.White;}
                if(n.Field!=null)
                {
                    var value=Value(id)!;
                    if(value.Length>0||editing)text=editing?value.Insert(caret,Preedit):value;
                    // Keep the edit point visible without splitting a Unicode text element.
                    if(editing)
                    {
                        var prefix=value[..caret]+Preedit;
                        float available=Math.Max(0,box.Right-tx-5*scale);
                        if(canvas.MeasureText(prefix,size)>available)
                        {
                            int[] starts=StringInfo.ParseCombiningCharacters(prefix);
                            int lo=0,hi=starts.Length;
                            while(lo<hi)
                            {
                                int mid=(lo+hi)/2;
                                if(canvas.MeasureText(prefix[starts[mid]..],size)>available)lo=mid+1;else hi=mid;
                            }
                            int cut=lo==starts.Length?prefix.Length:starts[lo];
                            prefix=prefix[cut..];text=text[cut..];
                        }
                        float cursor=tx+canvas.MeasureText(prefix,size);
                        using var fieldClip=canvas.PushClip(box);
                        if(Preedit.Length>0)
                        {
                            float w=Math.Min(cursor-tx,canvas.MeasureText(Preedit,size));
                            canvas.FillRectangle(new(cursor-w,ty+size*1.12f,w,scale),Color.Gold);
                        }
                        canvas.FillRectangle(new(cursor,ty,scale,size*1.15f),Color.White);
                    }
                }
                bool clipText=n.Field!=null || n.Parent?.Layout!=null || Preferences.TextScale!=1;
                using var textClip=clipText?canvas.PushClip(box):null;
                if(label?.Wrap==true && n.Field==null)
                    foreach(var line in n.Lines){canvas.DrawText(line,tx,ty,size,ink);ty+=size*1.4f;}
                else canvas.DrawText(text,tx,ty,size,ink);
            }
            if(Enabled(n)&&focus==id)
            {
                var ring=Preferences.HighContrast?Color.Yellow:Color.LightSkyBlue;var u=scale*(Preferences.HighContrast?2:1);
                canvas.FillRectangle(new(box.X,box.Y,box.Width,u),ring);canvas.FillRectangle(new(box.X,box.Bottom-u,box.Width,u),ring);
                canvas.FillRectangle(new(box.X,box.Y,u,box.Height),ring);canvas.FillRectangle(new(box.Right-u,box.Y,u,box.Height),ring);
            }
        }
        foreach(var n in widgets)if(n.Scroll&&Shown(n)&&Limit(n)>0)
        {
            using var clip=canvas.PushClip(At(RectangleF.Intersect(n.Box,n.Clip)));
            canvas.FillRectangle(At(Track(n)),Color.FromArgb(44,54,67));canvas.FillRectangle(At(Thumb(n)),Preferences.HighContrast?Color.White:Color.LightSlateGray);
        }
    }
}
