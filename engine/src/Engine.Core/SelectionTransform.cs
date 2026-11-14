using System.Numerics;

namespace Engine.Core;

/// <summary>A gesture starts from the original world matrices, even after many pointer moves.</summary>
public sealed class SelectionTransform
{
    private sealed record Entry(SceneObject Object, Matrix3x2 World, Matrix3x2 ParentInverse,
        float X,float Y,float Angle,float ScaleX,float ScaleY,float Shear);
    private readonly Entry[] entries;
    public Vector2 Pivot { get; }

    public SelectionTransform(IEnumerable<SceneObject> selection)
    {
        var objects=selection.Distinct().ToHashSet();
        var roots=objects.Where(o=>{
            for(var p=o.Parent;p!=null;p=p.Parent) if(objects.Contains(p))return false;
            return true;
        });
        entries=roots.Select(o=>new Entry(o,o.WorldMatrix,Affine2D.Inverse(o.Parent?.WorldMatrix??Matrix3x2.Identity),
            o.X,o.Y,o.Rotation,o.ScaleX,o.ScaleY,o.Shear)).ToArray();
        Pivot=Center(objects);
    }

    public static Vector2 Center(IReadOnlyCollection<SceneObject> objects)
    {
        if (objects.Count==0)return Vector2.Zero;
        if (objects.Count==1)
        {
            var obj=objects.First();
            return obj.LocalToWorld(new(obj.PivotX*obj.Width,obj.PivotY*obj.Height));
        }
        var bounds=objects.Select(o=>o.Bounds).Aggregate(System.Drawing.RectangleF.Union);
        return new(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2);
    }

    public void Apply(Matrix3x2 delta)
    {
        if (!Affine2D.Finite(delta) || !Matrix3x2.Invert(delta,out _))return;
        foreach(var entry in entries)
        {
            var obj=entry.Object;
            // Returning the pointer to its origin restores the original scalar values exactly.
            obj.X=entry.X;obj.Y=entry.Y;obj.Rotation=entry.Angle;
            obj.ScaleX=entry.ScaleX;obj.ScaleY=entry.ScaleY;obj.Shear=entry.Shear;
            if(delta==Matrix3x2.Identity)continue;
            if(delta.M11==1 && delta.M22==1 && delta.M12==0 && delta.M21==0)
            {
                var local=Vector2.TransformNormal(delta.Translation,entry.ParentInverse);
                obj.X+=local.X;obj.Y+=local.Y;
            }
            else obj.SetLocalMatrix(entry.World*delta*entry.ParentInverse);
        }
    }
}
