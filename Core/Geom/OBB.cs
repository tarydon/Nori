// ────── ╔╗
// ╔═╦╦═╦╦╬╣ OBB.cs
// ║║║║╬║╔╣║ Implements minimum enclosing 'Orientend Bounding Box' in 3D
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori.Internal;
namespace Nori;

#region struct OBB ---------------------------------------------------------------------------------
/// <summary>Represents a bounding cuboid oriented along an arbitrary axes.</summary>
public partial struct OBB {
   // Constructor --------------------------------------------------------------
   /// <summary>Construct an OBB given the center, X & Y direction vectors, Extent</summary>
   public OBB (Point3f cen, Vector3f x, Vector3f y, Vector3f ext) 
      => (Center, X, Y, Extent) = (cen, x, y, ext);

   /// <summary>Computes an OBB using the di-tetrahedral algorithm (see OBBDitoBuilder for more details)</summary>
   public static OBB Build (ReadOnlySpan<Point3f> pts)
      => new OBBDitoBuilder (pts).OBB;

   /// <summary>Computes an OBB using the fast PCA algorithm (not as tight as Build)</summary>
   /// In general, OBBs can be built 4~8 times faster with this algorithm. 
   /// However, the resulting OBBs can be up to 4x larger (in area) than the ones built
   /// by Build(). Build() should be fast enough for normal use, use this only when you are 
   /// REALLY pressed for time
   public static OBB BuildFast (ReadOnlySpan<Point3f> pts)
      => new OBBPCABuilder (pts).OBB;

   // Properties ---------------------------------------------------------------
   /// <summary> The box center</summary>
   public readonly Point3f Center;
   /// <summary>The 'half extent' along the axes.</summary>
   public readonly Vector3f Extent;

   /// <summary>Bounding box's co-ordinate axes.</summary>
   public readonly Vector3f X, Y;
   public readonly Vector3f Z => X * Y;

   // Pointers to the left and right children of this OBB node
   // These are interpreted thus:
   // - 0 is a null-pointer
   // - If positive they are pointers to another OBB (index into the OBBTree.OBBs array)
   // - If negative, they are pointers to leaf triangles (negative index into
   //   the OBBTree.Tris array)
   // To support this convention, OBBTree.CTris[0] is not used. (No confusion will arise 
   // about OBBTree.OBBs[0], which is the root and can never be a Left or Right child). 
   public int Left, Right;

   /// <summary>The box area</summary>
   public readonly double Area => 8 * (Extent.X * Extent.Y + Extent.X * Extent.Z + Extent.Y * Extent.Z);
   /// <summary>The box volume</summary>
   public readonly double Volume => 8 * (Extent.X * Extent.Y * Extent.Z);

   /// <summary>A 'zero volume' OBB (useful to initialize OBB structs)</summary>
   public static readonly OBB Zero = new (Point3f.Zero, Vector3f.XAxis, Vector3f.YAxis, Vector3f.Zero);

   // Methods ------------------------------------------------------------------
   /// <summary>Compares two OBB for equality</summary>
   public readonly bool EQ (ref OBB b) 
      => Center.EQ (b.Center) && Extent.EQ (b.Extent) && X.EQ (b.X) && Y.EQ (b.Y);
}
#endregion
