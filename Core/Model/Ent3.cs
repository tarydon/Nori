// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ent3.cs
// ║║║║╬║╔╣║ Defines the Ent3 hierarchy of classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Ent3 ---------------------------------------------------------------------------------
/// <summary>The base class for all Ent3</summary>
public abstract class Ent3 {
   public abstract Bound3 Bound { get; }
}
#endregion

#region class E3Plane ------------------------------------------------------------------------------
/// <summary>Represents a planar surface</summary>
/// The outer and inner contours of the plane are defined as Poly objects in the
/// XY plane, which are then lofted into the CoordSystem CS of the E3Plane
public class E3Plane : Ent3 {
   /// <summary>Create an E3Plane given the coordinate system to loft to and the set of 2D trims</summary>
   public E3Plane (CoordSystem cs, IEnumerable<Poly> trims) {
      mCS = cs; mTrims.AddRange (trims);
   }

   // Properties ------------,-------------------------------------------------
   public override Bound3 Bound => Bound3.Update (ref mBound, ComputeBound);
   Bound3 mBound = new ();

   /// <summary>The CoordSystem the plane is lofted into (after being defined in the XY plane)</summary>
   public CoordSystem CS => mCS;
   readonly CoordSystem mCS;

   /// <summary>The set of contours in 2D (lofted into the CoordSystem cs)</summary>
   public IReadOnlyList<Poly> Trims => mTrims;
   readonly List<Poly> mTrims = [];

   // Implementation -----------------------------------------------------------
   // Helper to compute the bound of the Plane3
   Bound3 ComputeBound () {
      var xfm = ToXfm;
      List<Point2> pts = [];
      mTrims[0].Discretize (pts, Lib.Delta);
      return new (pts.Select (a => (Point3)a * xfm));
   }

   // Private data -------------------------------------------------------------
   // The matrix to go from the world coordinate system to the plane's private CS
   Matrix3 ToXfm => _toXfm ??= Matrix3.To (mCS);
   Matrix3? _toXfm;
}
#endregion
