// ────── ╔╗
// ╔═╦╦═╦╦╬╣ E3Flex.cs
// ║║║║╬║╔╣║ E3Flex represents the deforming (bending) areas of a sheet metal model
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static Nori.Mesh3;
namespace Nori;

#region class E3Flex -------------------------------------------------------------------------------
/// <summary>E3Flex represents the deforming (bending) areas of a sheet-metal model.</summary>
/// E3Flex appear between E3Flat objects in a model. Each sheet-metal model starts with a root
/// E3Flat and the entities in this model form a tree connected via the E3Thick.Parent pointer
/// in each of these flats/flexes. The root E3Flat.Parent is null, but is non-null for all the
/// other entities. 
/// 
/// In a sheet-metal model, this tree has flats and flexes at alternate layers. Thus, no plane
/// connects directly to another plane, and no flex connects directly to another flex. 
/// 
/// See this image: file://N://Doc/Img/E3Flex.png
/// An E3Flex is defined by a set of contours that are lofted up into a coordinate system (much 
/// like an E3Flat). In addition, there is a "Spine" that provides the deformation that a Flex
/// undergoes. Initially, the spine is just a straight line, and you can imagine that this spine
/// is affixed at the origin of the CS and extends in the +Y direction. The trimming curve has
/// exactly the same height as the length of the spine. 
/// 
/// Then, during the bending, the spine deforms into a shape (a simple circular arc for a 
/// basic bend), and what was the original Y axis is now deformed into this arc, bending the
/// whole thickened shape along with it. Different types of spines result in different types
/// of bends like bump-bends, hems, Z-bends etc. 
/// 
/// Because of the way E3Flex objects are defined, it is necessary that the trimming curve
/// have YMin=0, and has a Y-Height that is exactly equal to the flat length of the unfolded 
/// spine (Spine at lie = 0). 
/// 
/// NOTE: The spine always represents the line at the exact midpoint of the thickness. It is not
/// to be confused with the neutral axis (that is the line that does not change in length as the
/// bending is done). Depending on the position of the neutral axis, the spine for a simple radius
/// bend typically increases slightly in length as the bending happens. 
public class E3Flex : E3Thick {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct an E3Flex given a lofting coordinate system, thickness and a set of poly</summary>
   /// <param name="id">The ID of this flex</param>
   /// <param name="cs">The root of the </param>
   /// <param name="thick">Thickness of the plane</param>
   /// <param name="spine">The spine is a line drawn in the middle of the thickness of
   /// this flex. When the flex is unbent (full flat) at a lie of 0, the spine is a straight
   /// line. As the flex is bent (spine lie = 1) it deforms into the final bend shape, 
   /// which is a single arc for simple bends, but could be more complex for other types
   /// of bends (like a Z-Bend)</param>
   /// <param name="shape">The trimming curves for the E3Flex. There is no particular
   /// ordering/winding implied, and this routine will move the outer contour to index 0,
   /// ensure it is CCW (while the rest of them are holes, with CW winding). The min Y of
   /// the trimming curves should be at Y=0, and the point (0,0) in the trimming curve will
   /// map to the origin of cs, with +X,+Y directions here corrdsponding to the +X,+Y 
   /// directions in the cs. The Z span of the flex stretches from -thickness/2 to 
   /// +thickness/2 in that coordinate system</param>
   public E3Flex (int id, CoordSystem cs, double thick, BSpine spine, IEnumerable<Poly> shape) : base (id, cs, thick, shape, true)
      => mSpine = spine;
   E3Flex (E3Flex other, Matrix3 xfm) : base (other, xfm) => mSpine = other.mSpine;
   E3Flex () => mSpine = null!;

   // Properties ---------------------------------------------------------------
   /// <summary>The spine representing the center-line of this bend</summary>
   public BSpine Spine => mSpine;
   readonly BSpine mSpine;

   // Methods ------------------------------------------------------------------
   /// <summary>Computes the (deformed) mesh of the Flex at a particular bending lie</summary>
   /// A lie of 0 returns the 'flat' mesh of this Flex, and a lie of 1 returns the fully 
   /// bent mesh (the same that is returned by E3Flex.Mesh)
   public Mesh3 BuildMesh (double lie) {
      // Cache the mesh at a lie of 0 into _zeroMesh (we will need this often)
      if (lie.IsZero (Lib.Delta)) return _zeroMesh ??= BuildMesh (ToXfm, true);
      if (lie.EQ (1) && _oneMesh != null) return _oneMesh;

      // Compute the 'base mesh' that we will deform - this is the mesh of the Flex in
      // a flattened state. This is not much different from the mesh for a E3Flat, so we build
      // it using the E3Thick.BuildMesh base routine
      if (_baseMesh == null) {
         int cSplits = Lib.GetArcSteps (mSpine.Radius, mSpine.Angle, MeshQuality);
         _ySplits = new double[cSplits - 1];
         double yMin = -0.01, yMax = mSpine.FlatWidth + 0.01;
         for (int i = 1; i < cSplits; i++) {
            double yLie = (double)i / cSplits;
            _ySplits[i - 1] = yLie.Along (yMin, yMax);
         }
         for (int i = 0; i < mShape.Length; i++)
            mShape[i] = SlicePoly (mShape[i], _ySplits);
         _baseMesh ??= BuildMesh (Matrix3.Identity, true);
      }

      // In this stage, we 'bend' this mesh by passing each node through a deformation
      // function. Let's describe this for a simple bend, where the spine transforms from being
      // a straight line to an arc. The starting point/tangent of this line/arc is fixed, while
      // the other end of this bends up/down depending on the spine. The length of the flat (line)
      // is exactly equal to the height of the trimming curve (or outer trimming curve, if there are
      // multiple). 
      // The positions close to Y=0 on the trimming curve correspond to positions near the start
      // of the spine (minimal deflection), while positions close to Y=max correspond to positions
      // closer to the end of the spine (maximal deflection)
      var xfm = ToXfm;
      double radius = mSpine.GetRadius (lie); bool down = !mSpine.Upward;
      double rInner = radius - Thickness / 2, rOuter = radius + Thickness / 2;
      double dirFactor = down ? -1 : 1;
      Node[] nodes = new Node[_baseMesh.Vertex.Length];
      for (int i = 0; i < nodes.Length; i++) {
         // Take each vertex in the base mesh
         var node = _baseMesh.Vertex[i];
         var (pos, vec) = (node.Pos, (Vector3)node.Vec);
         // From the Y position of this vertex, compute the angle (along the spine curve)
         double angle = pos.Y / radius;
         var (sin, cos) = Math.SinCos (angle);
         // Compute the new position and new perpendicular from this
         double r = pos.Z < 0 ^ down ? rOuter : rInner;
         double x = pos.X, y = r * sin, z = (radius - r * cos) * dirFactor;
         double vx = vec.X, vy = cos * vec.Y - sin * vec.Z, vz = (sin * vec.Y + cos * vec.Z) * dirFactor;
         // And create an updated node
         nodes[i] = new (new Point3 (x, y, z) * xfm, new Vector3 (vx, vy, vz) * xfm);
      }
      // Finally, create a new Mesh3 with the transformed set of nodes (but using the same triangle
      // connectivity indexes and wire-frame indices).
      Mesh3 mesh = new ([..nodes], _baseMesh.Triangle, _baseMesh.Wire);
      // If we just computed the mesh for a lie of 1, cache that since we need that mesh often
      if (lie.EQ (1)) _oneMesh = mesh;
      return mesh; 
   }

   /// <summary>Gets the 'tail' coordinate system at the downward end of the flex.</summary>
   /// The head coordinate system is passed in as the CS parameter when the E3Flex is constructed,
   /// and never changes. That point maps to the point (0,0) on the trimming curve. 
   public CoordSystem GetTailCS (double lie) {
      var (pos, vecy) = Spine.GetTailCS (lie);
      var cs = new CoordSystem (pos, Vector3.XAxis, vecy);
      return cs * ToXfm;
   }

   // Implementation -----------------------------------------------------------
   // Computes the mesh for this E3Flex (at full bent state)
   protected override Mesh3 ComputeMesh () => BuildMesh (1);

   // Helper used to slice a Poly at given Y split points, used before tessellating the
   // trimming curves of the Flex. This is needed for the step where we 'bend' the flat
   // mesh into a curved mesh by passing each point/vector through a deformation field
   // (see BuildMesh above)
   static Poly SlicePoly (Poly input, double[] split) {
      var pb = new PolyBuilder ();
      foreach (var seg in input.Monotoned ().Segs) {
         pb.Add (seg);
         if (seg.B.Y.EQ (seg.A.Y)) continue;
         if (seg.B.Y > seg.A.Y) {
            foreach (var y in split) {
               if (y <= seg.A.Y + Lib.Delta) continue;
               if (y >= seg.B.Y - Lib.Delta) break;
               double lie = y.GetLieOn (seg.A.Y, seg.B.Y);
               double x = lie.Along (seg.A.X, seg.B.X);
               Point2 pt = new (x, y);
               if (seg.IsArc) pb.Arc (pt, seg.Center, seg.Flags);
               else pb.Line (pt);
            }
         } else {
            for (int i = split.Length - 1; i >= 0; i--) {
               double y = split[i];
               if (y >= seg.A.Y - Lib.Delta) continue;
               if (y <= seg.B.Y + Lib.Delta) break;
               double lie = y.GetLieOn (seg.A.Y, seg.B.Y);
               double x = lie.Along (seg.A.X, seg.B.X);
               Point2 pt = new (x, y);
               if (seg.IsArc) pb.Arc (pt, seg.Center, seg.Flags);
               else pb.Line (pt);
            }
         }
      }
      return pb.Close ().Build ();
   }

   // Returns a transformed version of this Flex
   protected override Ent3 Xformed (Matrix3 xfm) => new E3Flex (this, xfm);

   // Private data -------------------------------------------------------------
   Mesh3? _baseMesh, _zeroMesh, _oneMesh;
   double[]? _ySplits;
}
#endregion

/// <summary>BSpine is a place-holder class that handles only radius bending with K-Factor = 0.5</summary>
/// We put this here for testing the rest of the system. This will later be replaced by an
/// IBSpine interface and set of implementations of that representing different types of bends
/// like air-bends, bump-bends, Z-bends etc. 
public class BSpine {
   public BSpine (double radius, double angle, double kfactor, bool upward) {
      (Radius, Angle, KFactor, Upward) = (radius, angle, kfactor, upward);
      Lib.Check (KFactor == 0.5, "KFactor should be 0.5");
      FlatWidth = Radius * Angle;
   }

   public double GetRadius (double lie) {
      if (lie < Lib.Delta) return 1e6;
      double arcLen = FlatWidth, theta = Angle * lie;
      return arcLen / theta;
   }

   public (Point3 Pt, Vector3 VecY) GetTailCS (double lie) {
      if (lie < Lib.Delta) return (new (0, FlatWidth, 0), new (0, 1, 0));
      double arcLen = FlatWidth, theta = Angle * lie, radius = arcLen / theta;
      var (sin, cos) = Math.SinCos (theta);
      double zFactor = Upward ? 1 : -1;
      return (new (0, radius * sin, zFactor * radius * (1 - cos)), new (0, cos, sin * zFactor)); 
   }

   // Properties ---------------------------------------------------------------
   public readonly double FlatWidth;
   public readonly double Radius;
   public readonly double Angle;
   public readonly double KFactor;
   public readonly bool Upward;
}
