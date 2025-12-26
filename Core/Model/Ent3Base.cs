// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ent3Base.cs
// ║║║║╬║╔╣║ Defines some the Ent3 hierarchy of classes (the abstract base classes)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Ent3 ---------------------------------------------------------------------------------
/// <summary>The base class for all Ent3</summary>
/// The hierarchy below shows the different classes derived from Ent3. When we import a STEP file,
/// all the surfaces from the BREP become objects of types derived from E3Surface (that is the level
/// at which we maintain connectivity information). When we do a sheet-metalization of that model, 
/// we create entities derived from E3Thick (then we have a developable sheet-metal model). 
/// 
/// All E3Surface are parametric surfaces, and the documentation of each surface defines the 
/// parametrization. Each point on the surface can be converted to a corresponding point in UV
/// space (parameter space) by the GetUV method. (Caveat: for some points like the poles of a sphere,
/// this is not a well defined function!). From a given UV value in parameter space, we can compute
/// the corresponding point on the surface (using the GetPoint method) and the corresponding normal
/// at that point (using the GetNormal method). 
/// 
/// The "parametrization" is useful in many places. For example, given a particular point on the
/// surface (which we might get from Lux.Pick for example), we can get the UV and then get the normal
/// to compute the normal at that point. Also, the parametrized UV curves of the surface form a set
/// of 2D polylines, and can be used as a starting point to tessellate the surface. 
/// 
/// A subset of the E3Surface types are derived from E3CSSurface. These are some common primitives
/// like sphere, torus, cone etc. They are often canonically defined to be at the origin, oriented
/// along the principal X, Y, Z axes, and then 'lofted up' into their final positions using a 
/// transformation CS (Coordinate System). This definition simplifies the GetPoint, GetUV type methods
/// for these surfaces much simpler, since we can work with a canonical definition and finally just
/// transform to/from the final positioning and orientation of the surface. 
///
/// E3Mesh                - Wrapper around a Mesh
/// E3Thick               - Base for all sheet-metal models (entities with thickness)
///   E3Sheet             - Thick planar surface (equivalent of Flux E3Plane)
///   E3Flex              - Represents a deforming area of the sheet (bend line)
/// E3Curve               - Curves in space
/// E3Surface             - Basis for BRep model (parametric surface with trimming curves, connectivity)
///   E3CSSurface         - Surfaces defined in a canonical world space and lofted up using a CS
///     E3Cylinder        - Cylindrical surface (canonical definition with base center at origin, axis along +Z)
///     E3Cone            - Conical surface (canonical definition with base center at origin, axis along +Z)
///     E3Torus           - Toroidal surface (canonical definition with center at origin, aligned to XY plane)
///     E3Sphere          - Spherical surface (canonical definition with center at origin)
///   E3Plane             - Plane defined by a set of Poly lofted into space
///   E3NurbSurface       - Rational spline surface of arbitrary order / degree
///   E3RuledSurface      - Surface defined by drawing ruling lines between two equi-parametric curves
///   E3PrismSurface      - Surface defined by sweeping a generatrix curve along a line
///   E3SpunSurface       - Surface defined by spinning a generatrix curve about an axis
[EPropClass]
public abstract partial class Ent3 {
   // Constructors -------------------------------------------------------------
   protected Ent3 () { }
   /// <summary>Protected contstructor - each Ent3 has an Id</summary>
   protected Ent3 (int id) => Id = id;

   // Properties ---------------------------------------------------------------
   /// <summary>Returns the Bound of the Ent3 (overridden in descendents)</summary>
   public abstract Bound3 Bound { get; }

   /// <summary>ID of the surface (often used to map to an entity number in STEP / IGES etc)</summary>
   public readonly int Id;

   /// <summary>Is the normal flipped (back-side of surface is to be used)</summary>
   public bool IsNormalFlipped => Get (E3Flags.FlipNormal); 
   /// <summary>Is the Genetrix lying in the XY plane</summary>
   public bool IsGenetrixFlat => Get (E3Flags.FlatGenetrix);

   /// <summary>Is this entity selected?</summary>
   public bool IsSelected {
      get => Get (E3Flags.Selected);
      set { if (Set (E3Flags.Selected, value)) Notify (EProp.Selected); }
   }

   /// <summary>Should this entity be rendered using a translucent (glass) shader</summary>
   public bool IsTranslucent {
      get => Get (E3Flags.Translucent);
      set { if (Set (E3Flags.Translucent, value)) Notify (EProp.Translucency); }
   }

   // Protected ----------------------------------------------------------------
   // Bitflags for this entity
   protected E3Flags mFlags;
   // Returns true if the specified bit is set
   protected bool Get (E3Flags bit) => (mFlags & bit) != 0;
   // Sets/resets one bit from the flags, returns true if state changed
   protected bool Set (E3Flags bits, bool value) {
      var old = mFlags;
      if (value) mFlags |= bits; else mFlags &= ~bits;
      return mFlags != old;
   }

   // Implementation -----------------------------------------------------------
   public override string ToString () => $"{GetType ().Name} #{Id}";
}
#endregion

#region enum E3Flags -------------------------------------------------------------------------------
/// <summary>Bitflags for Ent3</summary>
[Flags]
public enum E3Flags {
   Selected = 0x1, Translucent = 0x2, FlipNormal = 0x4, FlatGenetrix = 0x8,
   ULinear = 0x10, VLinear = 0x20,
}
#endregion

#region class E3Surface ----------------------------------------------------------------------------
/// <summary>E3Surface is the base class for parametric surfaces with no thickness</summary>
/// The surfaces are parametric - the 3D points can be unlofted into a UV parameter space 
/// (using the GetUV method), and given a UV value, we can compute the corresponding 3D position
/// (using the GetPoint method) and the corresponding normal (using the GetNormal method). 
/// The surface is bounded by a set of trimming curves - each one is a Contour3 made up of
/// multiple Curve3 entities (like Line3, Arc3, NurbsCurve3 etc). 
/// 
/// Each Curve3 has a PairId which may match a PairId of some other edge on some other surface,
/// thus generating connectivity information between surfaces. This connectivity information 
/// can be used to fetch the neighbors of a surface (using Model.GetNeighbors). 
public abstract class E3Surface : Ent3 {
   // Constructors -------------------------------------------------------------
   protected E3Surface () { }
   /// <summary>Create an E3Surface given the ID and set of trimming curves</summary>
   protected E3Surface (int id, IEnumerable<Contour3> trims) : base (id) => mContours = [.. trims];

   // Properties ---------------------------------------------------------------
   /// <summary>The Bound of the surface in 3D</summary>
   /// This is computed by the ComputeBound override, and cached here
   public override Bound3 Bound => Bound3.Update (ref mBound, ComputeBound);
   Bound3 mBound = new ();

   /// <summary>Set of contours of the surface</summary>
   public IReadOnlyList<Contour3> Contours => mContours;
   protected Contour3[] mContours = [];

   /// <summary>The Domain of the surface in UV parameter space</summary>
   /// This is computed by the ComputeDomain override, and cached here
   public Bound2 Domain => Bound2.Update (ref mDomain, ComputeDomain);
   Bound2 mDomain = new ();

   /// <summary>Is the surface linear in the U parameter direction?</summary>
   public bool IsULinear => Get (E3Flags.ULinear);
   /// <summary>Is the surface linear in the V parameter dimension</summary>
   public bool IsVLinear => Get (E3Flags.VLinear);

   /// <summary>The tessellation of the surface is computed on demand by BuildMesh (which can be overridden)</summary>
   public Mesh3 Mesh {
      get => _mesh ??= BuildMesh (Lib.FineTess, Lib.FineTessAngle);
      set {
         _mesh = value;
         if (this is E3Plane or E3Cylinder or E3Torus or E3Cone or E3NurbsSurface) {
            if (_mesh.Triangle.Length > 0) {
               var node = _mesh.Vertex[_mesh.Triangle[0]];
               Point2 uv = GetUV ((Point3)node.Pos);
               Vector3 vec1 = GetNormal (uv.X, uv.Y), vec2 = (Vector3)node.Vec;
               if (vec1.Opposing (vec2)) mFlags |= E3Flags.FlipNormal;
            }
         }
      }
   }
   Mesh3? _mesh;

   // Overrides ----------------------------------------------------------------
   /// <summary>BuildMesh is called to compute a tessellated mesh for this surface</summary>
   /// We have a default implementation here that uses the SurfaceMesher to compute
   /// a mesh. This unlofts the contours to create a set of UV contours. These are flat,
   /// and can be tessellated by the 2D tessellator. Then, 3D triangles are created from
   /// these 2D triangles (by lofting them using GetPoint). These 3D triangles are then
   /// adaptively subdivided until they are 'flat enough' to meet our tolerance requirements.
   /// This is expensive, and for a lot of surfaces like Plane, Cylinder, Cone etc, we will
   /// have overrides that simplify this process considerably.
   /// 
   /// Also note that this method cannot reliably handle cylical surfaces like spheres,
   /// cylinders, torus etc where the 3D surface often does not have any 'edges' in one or
   /// both of the parameter directions. The UV parametrization in such cases needs to introduce
   /// a 'seam' and all that logic is not yet handled by SurfaceMesher. 
   protected virtual Mesh3 BuildMesh (double tolerance, double maxAngStep) 
      => new SurfaceMesher (this).Build (tolerance, maxAngStep);

   /// <summary>Override this to compute the domain of the surface</summary>
   public virtual Bound2 ComputeDomain () => throw new NotImplementedException ();

   /// <summary>Computes the 3D point given a particular UV parameter values</summary>
   public abstract Point3 GetPoint (double u, double v);

   /// <summary>Computes the normal given a particular UV parameter value</summary>
   public virtual Vector3 GetNormal (double u, double v) {
      var d = Domain;
      double du = d.X.Length / 40, dv = d.Y.Length / 40;
      double u0 = d.X.Clamp (u - du), u1 = d.X.Clamp (u + du);
      double v0 = d.Y.Clamp (v - dv), v1 = d.Y.Clamp (v + dv);
      var vecx = GetPoint (u1, v) - GetPoint (u0, v);
      var vecy = GetPoint (u, v1) - GetPoint (u, v0);
      var vecz = (vecx * vecy).Normalized ();
      return IsNormalFlipped ? -vecz : vecz;
   }

   /// <summary>Compute the UV parameter value corresponding to a point lying on the surface</summary>
   public abstract Point2 GetUV (Point3 pt3d);

   // Implementation -----------------------------------------------------------
   // If a mesh exists, we use that to return the bound. Otherwise, we compute a bound
   // by evaluating the contours
   Bound3 ComputeBound () {
      if (_mesh != null) return _mesh.Bound;
      List<Point3> pts = [];
      mContours[0].Discretize (pts, Lib.CoarseTess, Lib.CoarseTessAngle);
      return new (pts);
   }
}
#endregion

#region class E3CSSurface --------------------------------------------------------------------------
/// <summary>E3CSSurface are defined in canonical space and lofted up into a CS</summary>
public abstract class E3CSSurface : E3Surface {
   // Constructors -------------------------------------------------------------
   protected E3CSSurface () { }
   protected E3CSSurface (int id, IEnumerable<Contour3> trims, CoordSystem cs) : base (id, trims) => mCS = cs;

   // Properties ---------------------------------------------------------------
   /// <summary>The coordinate system into which the surface has to be lofted up</summary>
   public CoordSystem CS => mCS;
   readonly CoordSystem mCS;

   /// <summary>Matrix to go from the entity's private CS to the world Coordinate System</summary>
   public Matrix3 FromXfm => _fromXfm ??= Matrix3.From (mCS);
   Matrix3? _fromXfm;

   // The matrix to go from the world coordinate system to the entity's private CS
   public Matrix3 ToXfm => _toXfm ??= Matrix3.To (mCS);
   Matrix3? _toXfm;

   // Overrides ----------------------------------------------------------------
   /// <summary>GetPoint computes the point in canonical space and lofts it up using the ToXfm</summary>
   /// The underlying GetPointCanonical is overridden by derived classes, and the 
   /// reasoning is that it would be much simpler to implement some surfaces like cones,
   /// spheres, torii etc in a canonical space (world coordinate system) rather than in 
   /// an arbitary position / orientation
   public sealed override Point3 GetPoint (double u, double v) 
      => GetPointCanonical (u, v) * ToXfm;

   /// <summary>GetNormal computes the normal in canonical space and lofts it up</summary>
   public sealed override Vector3 GetNormal (double u, double v) {
      Vector3 vec = GetNormalCanonical (u, v) * ToXfm;
      return IsNormalFlipped ? -vec : vec;
   }

   /// <summary>GetUV unlofts the point into canonical space, and then uses GetUVCanonical</summary>
   public override Point2 GetUV (Point3 pt) 
      => GetUVCanonical (pt * FromXfm);

   /// <summary>GetPointCanonical computes the point in canonical space, given UV coordinates</summary>
   protected abstract Point3 GetPointCanonical (double u, double v);
   /// <summary>GetNormalCanonical computes a normal in canonical space, given UV coordinates</summary>
   protected abstract Vector3 GetNormalCanonical (double u, double v);
   /// <summary>Computes UV coordinates, given a point in canonical space</summary>
   protected abstract Point2 GetUVCanonical (Point3 ptCanon);
}
#endregion
