using System.Drawing;
using System.Numerics;
using Engine.Core;

internal static class ComponentKeyChecks
{
    private sealed class Canvas : ICanvas2D
    {
        internal readonly List<(string? Path, Matrix3x2 Matrix, Color Tint)> Calls = [];
        public void Clear(Color ignored) => Calls.Clear();
        public void FillRectangle(RectangleF rect,Color color) { }
        public bool DrawSprite(string path,RectangleF rect) => true;
        public bool DrawFiltered(SpriteFilter filter,RectangleF rect,Matrix3x2 matrix,Color tint)
            => DrawAffine(filter.Image,rect,matrix,tint);
        public bool DrawAffine(string? path,RectangleF rect,Matrix3x2 matrix,Color tint,SpriteRegion? source=null,string layout=TileGrid.Orthogonal)
        { Calls.Add((path,matrix,tint)); return true; }
    }

    internal static void Run(string root)
    {
        var curves = new ComponentCurves([ new("ScaleX",[new(.2,"-1"),new(.8,"-3")]) ],1);
        if (curves.Sample(0).ScaleX!=-1 || curves.Sample(.5).ScaleX!=-2 || curves.Sample(1).ScaleX!=-3 || curves.Sample(.5).Opacity!=null)
            throw new Exception("Sparse keys must interpolate just their property and hold both endpoints");
        void Reject(ComponentTrack track)
        {
            try { _=new ComponentCurves([track],1); }
            catch (InvalidDataException) { return; }
            throw new Exception("Accepted invalid component track "+track.Property);
        }
        Reject(new("ScaleY",[new(0,"-1"),new(1,"1")]));
        Reject(new("Rotation",[new(.5,"1"),new(.5,"2")]));
        Reject(new("Opacity",[new(0,"2")]));
        Reject(new("Rotation",[new(double.NaN,"1")]));
        Reject(new("Tint",[new(0,"red")]));

        using var session=new GameSession(SceneDocument.Load(Path.Combine(root,"Scenes","KeyRoom.scene")));
        var game=session.Current;
        var lid=game.Objects.Single(o=>o.Name=="Chest Lid").Id;
        var canvas=new Canvas();
        for (int n=0;n<15;n++) session.FixedUpdate(Vector2.Zero,1f/60);
        game.Draw(canvas,1);
        var draw=canvas.Calls.Single(c=>c.Path==SpriteAssets.SquarePath && c.Tint.ToArgb()==Color.FromArgb(0xEB,0xB8,0x54).ToArgb());
        if (!game.TryGetWorldMatrix(lid,out var world) || Math.Abs(Affine2D.Angle(draw.Matrix)+12)>.002 ||
            Math.Abs(Affine2D.Angle(world)-Affine2D.Angle(draw.Matrix))>.002)
            throw new Exception("Lid rotation did not reach the camera draw command");
        session.FixedUpdate(new GameInput(Vector2.Zero,true),1f/60);
        for (int n=0;n<30;n++) session.FixedUpdate(Vector2.Zero,1f/60);
        game.Draw(canvas,1);
        if (!canvas.Calls.Any(c=>Path.GetFileName(c.Path)=="Coin.png" && c.Tint.ToArgb()==Color.FromArgb(51,63,195,159).ToArgb()))
            throw new Exception("RGBA keys did not reach the spawned coin draw command");
        Console.WriteLine("PASS component curves, invalid key rejection, KeyRoom affine draw and RGBA modulation");
    }
}
