using System.Drawing;
using System.Numerics;
using Engine.Core;

internal static class TileContactChecks
{
    internal static void Run()
    {
        static void Require(bool value, string reason)
        {
            if (!value) throw new InvalidDataException(reason);
        }
        var def = new TileDefinition(1,new(0,0,16,16),true);
        var face = new TileCollision([new(0,.25f),new(1,.25f)],true);
        face.Validate("test",1);
        foreach (var matrix in new[]{Matrix3x2.Identity, Matrix3x2.CreateScale(-2,1.2f) * Matrix3x2.CreateRotation(.4f) * Matrix3x2.CreateTranslation(-75,141)})
        {
            var target = TileCollision.WorldShape(def with { Collision=face },new(0,0,32,24),TileGrid.Orthogonal,matrix)!.Value;
            var center = Vector2.Transform(new(16,6),matrix);
            var normal = target.FaceNormal;
            Require(Vector2.Dot(normal,Vector2.TransformNormal(-Vector2.UnitY,matrix))>0,"Mirroring flipped the blocking side");
            var circle = Shape2D.Circle(center+normal*6,2);
            var hit = CollisionGeometry.Cast(circle,-normal*12,target,false,true);
            Require(hit != null && Math.Abs(hit.Value.Time-1f/3)<.00001,"Fast sweep of a circle missed the finite face");
            Require(CollisionGeometry.Cast(Shape2D.PointAt(center-normal*6),normal*12,target,true,false)==null,"Backface blocked a ray");
            Require(CollisionGeometry.Cast(Shape2D.Circle(center-normal*2,2),normal*12,target,false,true)==null,"Backface blocked an ascending body");
            Require(CollisionGeometry.Cast(Shape2D.PointAt(center+normal*6+Vector2.Normalize(target.Vertices![1]-target.Vertices[0])*100),
                -normal*12,target,true,false)==null,"Infinite platform endpoints");
            Require(CollisionGeometry.Overlap(Shape2D.Circle(center,2),target),"Overlap missed a finite face");
        }
        foreach(var invalid in new TileCollision[] { new([new(0,0),new(0,0)],true), new([new(0,0),new(2,0)],true),
            new([new(0,0),new(1,0),new(.5f,.5f),new(1,1),new(0,1)]) })
        {
            try { invalid.Validate("test",1); }
            catch (InvalidDataException) { continue; }
            throw new Exception("Invalid tile collision accepted");
        }
        Console.WriteLine("PASS tile collision: convex validation, transformed one-way normals, front/back sweeps, overlap and finite endpoints");
    }
}
