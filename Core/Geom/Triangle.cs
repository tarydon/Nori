// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Triangle.cs
// ║║║║╬║╔╣║ Implements variaous tessellators to generate triangles in 2D and 3D
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

/// <summary>A base class for 2D/3D tesselator selectors.</summary>
public abstract class TessBase {
   /// <summary>Error results after tessellation (if any)</summary>
   public string Error => mError;
   protected string mError = string.Empty;
}

/// <summary>A base class to help select the appropriate 2D tessellation implementation</summary>
public abstract class Tess2D : TessBase {
   public abstract List<int> Do (List<Point2> pts, IReadOnlyList<int> splits);
}

/// <summary>A base class to help select the appropriate 3D tessellation implementation</summary>
public abstract class Tess3D : TessBase {
   public abstract List<int> Do (ReadOnlySpan<Point3> pts, ReadOnlySpan<int> splits);
}

/// <summary>A base class for all Nori tessellators.</summary>
public abstract class Tessellator {
   /// <summary>Given the implementation type, instantiates and returns a 2D tessellator object</summary>
   /// In practice, the actual tessellator implementation may reside in different assembly from
   /// where the interface is defined. This method helps _inject_ the implementation into the interface.
   /// <typeparam name="TessImp">The 2D tessellator implementation type</typeparam>
   /// <returns>The 2D tessellator implementation instance</returns>
   public static Tess2D TwoD<TessImp> () where TessImp : Tess2D, new () => new TessImp ();

   public static Tess3D ThreeD<TessImp> () where TessImp : Tess3D, new () => new TessImp ();

   /// <summary>Error results after tessellation (if any)</summary>
   public string Error => mError;
   protected string mError = string.Empty;

   /// <summary>What is the minimum area of triangles below which they are rejected?</summary>
   public double MinArea { get; set; } = 1E-12;

   /// <summary>Performs the tessellation and returns the tessellation results.</summary>
   public abstract List<int> Process ();

   /// <summary>This stores the resultant tessellation</summary>
   protected readonly List<int> mResult = [];
}

class EarClip2 (ImmutableArray<Point2> pts, ImmutableArray<int> splits) : Tessellator {
   readonly ImmutableArray<Point2> Pts = pts;
   readonly ImmutableArray<int> Splits = splits;
   public override List<int> Process () {
      return [0];
   }
}
