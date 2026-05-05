// ────── ╔╗
// ╔═╦╦═╦╦╬╣ E3Flat.cs
// ║║║║╬║╔╣║ Implements E3Flat (planar areas of sheet metal), and E3Thick (base class)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class E3Thick ------------------------------------------------------------------------------
/// <summary>Base class for various types of sheet metal entities (E3Flat, E3Flex etc)</summary>
public abstract class E3Thick : Ent3 {
   /// <summary>Initialize an E3Thick</summary>
   protected E3Thick (int id, CoordSystem cs, double thickness, IEnumerable<Poly> input, bool discretize) : base (id) {
      (mCS, mThickness) = (cs, thickness);
      List<Poly> poly = [.. input.Where (a => a.IsClosed)];
      if (poly.Count == 0) throw new ArgumentException ("Open Poly passed to E3Thick");
      poly.Swap (0, poly.MaxIndexBy (a => a.GetBound ().Area));
      for (int i = 0; i < poly.Count; i++)
         if (poly[i].GetWinding () == Poly.EWinding.CCW == i > 0)
            poly[i] = poly[i].Reversed ();
      if (discretize)
         for (int i = 0; i < poly.Count; i++) poly[i] = poly[i].DiscretizeP (MeshQuality);

      mShape = [.. poly];
   }
   protected E3Thick () => mShape = null!;

   /// <summary>Constructor used by Xform</summary>
   protected E3Thick (E3Thick other, Matrix3 xfm) : base (other.Id) {
      mCS = other.mCS * xfm;
      if (other._mesh != null) _mesh = other._mesh * xfm;
      mShape = other.mShape;
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Computes the bounding cuboid of this E3Thick</summary>
   public override Bound3 Bound => Bound3.Cached (ref mBound, () => Mesh.Bound);
   Bound3 mBound = new ();

   /// <summary>Definition coordinate system for the E3Thick</summary>
   /// Interpretation depends on the type of entity:
   /// - For E3Flat, the poly are all lofted up to this CS 
   /// - For E3Flex, this is the fixed 'root' of the flex that does not change
   ///   during flexure - the othr end (tip flexes)
   public CoordSystem CS => mCS;
   readonly CoordSystem mCS;

   /// <summary>The parent of this E3Thick (will be null for root plane)</summary>
   public E3Thick? Parent;

   /// <summary>The rendering mesh for this E3Thick</summary>
   public Mesh3 Mesh => _mesh ??= ComputeMesh ();
   Mesh3? _mesh;

   /// <summary>The shape of the E3Thick (0=outer contour : CCW, 1=inner contours, CW)</summary>
   public ImmutableArray<Poly> Shape => mShape.ToIArray ();
   protected readonly Poly[] mShape;

   /// <summary>The thickness of this E3Thick</summary>
   public double Thickness => mThickness;
   readonly double mThickness;

   /// <summary>Transform to move from world to E3Thick local coordinates</summary>
   public Matrix3 ToXfm => _toXfm ??= Matrix3.To (mCS);
   Matrix3? _toXfm;

   // Overrides / overrideables ------------------------------------------------
   protected abstract Mesh3 ComputeMesh ();

   // Helper used by E3Flat and E3Flex to build a mesh
   protected Mesh3 BuildMesh (Matrix3 xfm, bool horizontalBias) {
      // Mesh data
      List<Mesh3.Node> nodes = [];
      List<int> tris = [], wires = [], counts = [];

      // Do the tessellation
      using var tess = FastTess2D.Borrow ();
      tess.Tolerance = MeshQuality;
      if (horizontalBias) tess.BiasAngle = 0.0001;
      for (int i = 0; i < Shape.Length; i++) {
         var poly = Shape[i].DiscretizeP (MeshQuality);
         counts.Add (tess.AddPoly (poly, i > 0, true));
      }
      tess.Process ();
      var (pts, tris0, n) = (tess.Pts, tess.Tris, tess.Pts.Count);

      // Add the bottom and top planes
      double t = Thickness / 2;
      CollectionsMarshal.SetCount (nodes, n * 2);
      Vec3H vec0 = -Vector3.ZAxis * xfm, vec1 = Vector3.ZAxis * xfm;
      for (int i = 0; i < pts.Count; i++) {
         Point2 pt = pts[i];
         nodes[i] = new (new Point3f (pt.X, pt.Y, -t) * xfm, vec0);
         nodes[i + n] = new (new Point3f (pt.X, pt.Y, t) * xfm, vec1);
      }
      tris.AddRange (tris0); tris.Reverse ();
      tris.AddRange (tris0.Select (a => a + n));
      for (int i = 0; i < 2; i++) {
         int start = i * n;
         foreach (var count in counts) {
            for (int j = 0; j < count; j++) {
               wires.Add (j + start);
               wires.Add ((j + 1) % count + start);
            }
            start += count;
         }
      }

      // Add the sidewalls
      foreach (var shape in Shape) {
         int start = nodes.Count; Point2 last = Point2.Nil;
         foreach (var (pt, slope, wire) in shape.DiscretizeTangent (MeshQuality)) {
            int a = nodes.Count;
            Vec3H normal = ((Vector3)Vector2.UnitVec (slope - Lib.HalfPI)) * xfm;
            Point3 pos0 = new Point3 (pt.X, pt.Y, -t) * xfm;
            Point3 pos1 = new Point3 (pt.X, pt.Y, t) * xfm;
            nodes.Add (new ((Point3f)pos0, normal));
            nodes.Add (new ((Point3f)pos1, normal));
            if (wire) { wires.Add (a); wires.Add (a + 1); }
            if (last.IsNil) last = pt;
            if (!last.EQ (pt)) tris.AddM (a - 2, a, a + 1, a - 2, a + 1, a - 1);
            last = pt;
         }
      }
      return new ([.. nodes], [.. tris], [.. wires]);
   }
}
#endregion

#region class E3Flat -------------------------------------------------------------------------------
/// <summary>E3Flat represents the planar/flat parts of a sheet-metal model</summary>
/// These connect to other E3Flats via E3Flex objects (that represent the defoming areas,
/// or bends). An E3Flat is defined with a set of contours, and a 'lofting' coordinate system, 
/// as shown here: file://N:/Doc/Img/E3Flat.png.
/// 
/// The set of contours is lofted up into the frame defined by the CS. The zero point of this
/// CS is at the midpoint of the thickness, which extends by Thickness/2 in each of the -Z and +Z
/// directions around this. 
public class E3Flat : E3Thick {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct an E3Flat given a lofting coordinate system, thickness and a set of poly</summary>
   /// <param name="cs">Each poly is lofted up into this space - the plane lies in the
   /// XY plane of this CoordSystem, and the origin of this coordsystem lies exactly in the
   /// midpoint of the thickness (the plane extends from -thickness/2 to +thickness/2 
   /// in this CoordSystem</param>
   /// <param name="thick">Thickness of the plane</param>
   /// <param name="shape">Set of poly representing the shape. No particular ordering/winding
   /// is expected, the constructor will move the outer poly to the outside, and make it 
   /// CCW (while the holes are all made CW)</param>
   public E3Flat (int id, CoordSystem cs, double thick, IEnumerable<Poly> shape) : base (id, cs, thick, shape, false) { }
   E3Flat (E3Flat other, Matrix3 xfm) : base (other, xfm) { }
   E3Flat () { }

   // Overrides -----------------------------------------------------------------
   /// <summary>Computes the mesh for this E3Flat</summary>
   protected override Mesh3 ComputeMesh () => BuildMesh (ToXfm, false);

   /// <summary>Returns a transformed version of this E3Flat</summary>
   protected override Ent3 Xformed (Matrix3 xfm) => new E3Flat (this, xfm);
}
#endregion
