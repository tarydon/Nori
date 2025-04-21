// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Triangle.cs
// ║║║║╬║╔╣║ Implements variaous tessellators to generate triangles in 2D and 3D
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori; 

/// <summary>A base class for all Nori tessellators.</summary>
public abstract class Tessellator {
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
