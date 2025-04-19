// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Triangle.cs
// ║║║║╬║╔╣║ Implements variaous tessalators to generate triangles in 2D and 3D
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori; 

public abstract class Tesselator {
   public static Tesselator GLU => new GLUTess ();
   public static Tesselator EarClip => new EarClipTess ();

   public abstract List<int> Do (ImmutableArray<Point2> pts, ImmutableArray<int> splits);
   public abstract List<int> Do (ImmutableArray<Point3> pts, ImmutableArray<int> splits);

   class GLUTess : Tesselator {
      public override List<int> Do (ImmutableArray<Point2> pts, ImmutableArray<int> splits) 
         => new GLU2D (pts, splits).Process ();

      public override List<int> Do (ImmutableArray<Point3> pts, ImmutableArray<int> splits)
         => new GLU3D (pts, splits).Process ();
   }

   class EarClipTess : Tesselator {
      public override List<int> Do (ImmutableArray<Point2> pts, ImmutableArray<int> splits)
         => new EarClip2 (pts, splits).Process ();

      public override List<int> Do (ImmutableArray<Point3> pts, ImmutableArray<int> splits)
         => throw new NotImplementedException ();
   }
}

abstract class TesselatorImp {
   public abstract List<int> Process ();
}

class GLU2D (ImmutableArray <Point2> pts, ImmutableArray<int>splits): TesselatorImp {
   readonly ImmutableArray<Point2> Pts = pts;
   readonly ImmutableArray<int> Splits = splits;
   public override List<int> Process () {
      return [0];
   }
}

class GLU3D (ImmutableArray<Point3> pts, ImmutableArray<int> splits) : TesselatorImp {
   readonly ImmutableArray<Point3> Pts = pts;
   readonly ImmutableArray<int> Splits = splits;
   public override List<int> Process () {
      return [0];
   }
}

class EarClip2 (ImmutableArray<Point2> pts, ImmutableArray<int> splits) : TesselatorImp {
   readonly ImmutableArray<Point2> Pts = pts;
   readonly ImmutableArray<int> Splits = splits;
   public override List<int> Process () {
      return [0];
   }
}
