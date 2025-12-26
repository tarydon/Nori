// ────── ╔╗
// ╔═╦╦═╦╦╬╣ SurfHelper.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

using System.Reactive.Subjects;
using static Math;

#region class SurfaceMesher ------------------------------------------------------------------------
/// <summary>This computes a Mesh3 for a surface</summary>
class SurfaceMesher {
   public SurfaceMesher (E3Surface surf) => mSurf = surf;
   readonly E3Surface mSurf;

   public Mesh3 Build (double tolerance, double maxAngStep) {
      // First, we flatten each trimming curve into the UV space, and compute a
      // 2D triangular tessellation in the UV space. At this point, we compute the
      // following set of data:
      mTolerance = tolerance;
      List<Point3> pts = [];  // Discretization of all the trimming curves of the surface
      List<int> splits = [0]; // Split points that divide pts into individual contours
      List<int> wires = [];   // Elements taken as pairs that defined the silhouette wires
      foreach (var contour in mSurf.Contours) {
         int a = pts.Count;
         contour.Discretize (pts, tolerance, maxAngStep);
         int b = pts.Count; splits.Add (b);
         wires.Add (b - 1);
         for (int i = a; i < b; i++) { wires.Add (i); wires.Add (i); }
         wires.RemoveLast ();
      }
      // Now we can use the 2D tessellator to compute the following:
      var uvs = pts.Select (mSurf.GetUV).ToList (); // Same as the set of pts, flattened to UV space
      var tris = Lib.Tessellate (uvs, splits);  // The indices (taken 3 at a time) forming the tessellation in UV space
      for (int i = pts.Count; i < uvs.Count; i++) {
         var uv = uvs[i];
         pts.Add (mSurf.GetPoint (uv.X, uv.Y));
      }

      // The UV tessellation will have some triangles, but not all of them can directly be lofted
      // and used in the 3D. Some of them will need to be further subdivided into smaller triangles
      // (to cope with the curvature)
      for (int i = 0; i < uvs.Count; i++) {
         Point2 uv = uvs[i];
         mNodes.Add (new (uv, (Point3f)pts[i], (Vec3H)mSurf.GetNormal (uv)));
      }
      Dictionary<Point2, int> cache = new (new PointComparer (1e-6));
      for (int i = 0; i < tris.Count; i += 3)
         AddTriangle (tris[i], tris[i + 1], tris[i + 2]);

      // The AddTriangle calls above are all potentially recursive, subdividing the input
      // fed in into smaller and smaller triangles until each one is sufficiently flat
      // (to within the tessellation error we specify). As additional nodes are added in,
      // the nodes array gets expanded and the final set of triangles is stored in 'triangles'.
      // Note that the wires[] array is still valid into this expanded set of nodes, since
      // that is made up of the original boundary edges only (and not one of the interior
      // nodes we added as a part of curvature subdivision).
      var mnodes = mNodes.Select (a => new Mesh3.Node (a.Pos, a.Normal)).ToImmutableArray ();
      return new Mesh3 (mnodes, [.. mTris], [.. wires]);
   }
   readonly List<Node> mNodes = [];
   readonly List<int> mTris = [];
   double mTolerance;

   void AddTriangle (int a, int b, int c) {
      // Take each of the midpoints and see which one has the worst deviation,
      // that will be where we split
      Node na = mNodes[a], nb = mNodes[b], nc = mNodes[c];
      Point2 p2ab = na.UV.Midpoint (nb.UV), p2bc = nb.UV.Midpoint (nc.UV), p2ca = nc.UV.Midpoint (na.UV);
      Point3 p3ab = mSurf.GetPoint (p2ab.X, p2bc.Y), p3bc = mSurf.GetPoint (p2bc.X, p2bc.Y), p3ca = mSurf.GetPoint (p2ca.X, p2ca.Y);
      double dab = Dist (p3ab, na.Pos, nb.Pos), dbc = Dist (p3bc, nb.Pos, nc.Pos), dca = Dist (p3ca, nc.Pos, na.Pos);

      if (dab > mTolerance && dbc > mTolerance && dca > mTolerance) {   // Split into 4 triangles
         int ab = AddNode (p2ab, p3ab), bc = AddNode (p2bc, p3bc), ca = AddNode (p2ca, p3ca);
         AddTriangle (a, ab, ca); AddTriangle (b, bc, ab); AddTriangle (c, ca, bc);
         AddTriangle (ab, bc, ca);
      } else if (dab >= dbc && dab >= dca && dab > mTolerance) {   // Try splitting ab
         int n = AddNode (p2ab, p3ab);
         AddTriangle (a, n, c); AddTriangle (n, b, c);
      } else if (dbc >= dab && dbc >= dca && dbc > mTolerance) {    // Try splitting bc
         int n = AddNode (p2bc, p3bc);
         AddTriangle (a, b, n); AddTriangle (n, c, a);
      } else if (dca >= dab && dca >= dbc && dca > mTolerance) {    // Try splitting ca
         int n = AddNode (p2ca, p3ca);
         AddTriangle (a, b, n); AddTriangle (n, b, c);
      } else {       // No splitting required, triangle is flat enough to add
         mTris.Add (a); mTris.Add (b); mTris.Add (c);
      }

      static double Dist (Point3 pt, Point3f a, Point3f b)
         => pt.DistToLine ((Point3)a, (Point3)b);
   }

   int AddNode (Point2 uv, Point3 pt) {
      if (mCache.TryGetValue (uv, out int n)) return n;
      mNodes.Add (new (uv, (Point3f)pt, (Vec3H)mSurf.GetNormal (uv)));
      mCache.Add (uv, n = mNodes.Count - 1);
      return n;
   }
   Dictionary<Point2, int> mCache = new (new PointComparer (1e-6));

   struct Node (Point2 uv, Point3f pos, Vec3H normal) {
      public readonly Point2 UV = uv;
      public readonly Point3f Pos = pos;
      public readonly Vec3H Normal = normal;
   }
}
#endregion

#region class Unlofter --------------------------------------------------------------------------
/// <summary>This class provides 'unloft' capabilities for any parametric surface.</summary>
/// Given a Point3 in 3D space, this returns the corresponding Point2 in the UV parameter space.
/// This is done using an 'adaptive' subdivision method. Here's how it works.
/// 
/// A parametric surface is defined using a rectangular 'domain' in UV space, which is lofted
/// into 3D space using the parametric function (this is implemented as the virtual function
/// Evaluate of the parametric surface). Now, we first create a very coarse 4x4 grid of tiles
/// by dividing the U and V spans of the UV space into 4. As we need to unloft some points
/// from 3D space to UV space, we may successively keep sub-dividing these domains finer and 
/// finer. 
/// 
/// When we get a point to be unlofted, we first check which of the 16 'root' tiles it
/// potentially lies within. To do this, we consider each of these tiles to be an approximation
/// to a planar quadrilateral. We take the normal vector of the quad and find out which is
/// the dominant axis of this normal; we then project the quadrilateral of the tile's outline
/// and the input point into the corresponding XY, YZ or XZ planes and work there. In that
/// plane, if we find that the point lies within the projection of the tile, we ask that tile
/// to provide the UV value corresponding to the point.
/// 
/// The tile knows the UV values at the 4 'corners' as well as the corresponding XYZ values. 
/// It first checks if it is 'flat enough' (close enough to a planar approximation). If it 
/// is not, it subdivides itself into 4 smaller tiles and recursively asks them to do the
/// unevaluation. At places where the surface is highly curved, this subdivision will proceed
/// more levels deep, and at places where it is more 'planar' it will stop much earlier. 
/// 
/// Once we have reached a tile which is a fairly planar approximation, we have a quadrilateral
/// (projected into a 2D plane such as XY, YZ etc) and a 2-D point (projected to the same plane).
/// Knowing the UV values at the corner, we can compute the UV values of the point itself by
/// solving a simple quadratic equation (this is derived by trying to express the target point
/// as a weighted sum of the 4 corner vertices). 
/// 
/// Thus we have a spatial subdivision tree of the UV parameter space which is built up 
/// incrementally as needed; the tree is flattened and stored in a simple list, with each
/// tile holding onto the starting index of its first child, and the number of children (which
/// all follow successively in the list). 
/// 
/// Note: Not all parametric surfaces use this Unlofter. For some surfaces the reverse
/// evaluation from XYZ to UV space is very easily accomplished analytically (for example, 
/// cones and cylinders). Only when the parametric surface does not override the GetUV
/// method do we even create an unlofter and use it to do the reverse evaluation.
public class Unlofter {
   #region Constructor --------------------------------------------
   /// <summary>Construct a ParaUnloft, given a ParaSurface3 to work with</summary>
   public Unlofter (E3Surface surf) {
      Surf = surf;
      int uDiv = Surf.IsULinear ? 1 : 4;
      int vDiv = Surf.IsVLinear ? 1 : 4;
      var dom = Surf.Domain;
      double du = dom.X.Length / uDiv, dv = dom.Y.Length / vDiv;

      // First, build up the grid of UV3D points
      for (int v = 0; v <= vDiv; v++) {
         double tv = dom.Y.Min + v * dv;
         for (int u = 0; u <= uDiv; u++) {
            double tu = dom.X.Min + u * du;
            mNodes.Add (new UV3D (Surf, tu, tv));
         }
      }
      // Next, make tiles out of them. 
      int step = uDiv + 1;
      for (int v = 0; v < vDiv; v++) {
         for (int u = 0; u < uDiv; u++) {
            int bt = v * step + u;
            mTiles.Add (new Tile (this, bt, bt + 1, bt + step + 1, bt + step, 0));
         }
      }
      mcRootTiles = mTiles.Count;
   }
   #endregion

   #region Methods ------------------------------------------------
   /// <summary>This is the main function that takes a point in 3D space and returns the corresponding UV point</summary>
   public Point2 GetUV (Point3 xyz) {
      Point2 uv = new (), uvBest = uv;

      // See if there is a tile that contains this point; since we're using a simple
      // 'shadow' project in one of the three 2-D directions, it possible multiple tiles may
      // match; fetch the one that which is closest
      double minDist = 1000000;
      for (int pass = 0; pass < 2; pass++) {
         bool iPass2 = (pass == 1);
         for (int i = mcRootTiles - 1; i >= 0; i--) {
            if (mTiles[i].GetUV (this, i, xyz, ref uv, iPass2)) {
               double dist = Surf.GetPoint (uv.X, uv.Y).DistTo (xyz);
               if (dist < minDist) { minDist = dist; uvBest = uv; }
            }
         }
         if (minDist < 2) return uvBest;
      }
      // If everything fails, unloft into the 'mipoint'
      return Surf.Domain.Midpoint;
   }

   /// <summary>This routine is used purely for debugging; it returns the outlines of the tiles</summary>
   public List<Point3f> GetTileOutlines () {
      List<Point3f> a = new ();
      foreach (var tile in mTiles) {
         if (tile.HasChildren) continue;
         Point3f p = mNodes[tile.A].XYZ, q = mNodes[tile.B].XYZ, r = mNodes[tile.C].XYZ, s = mNodes[tile.D].XYZ;
         Point3f m1 = new ((p.X + q.X + r.X + s.X) / 4, (p.Y + q.Y + r.Y + s.Y) / 4, (p.Z + q.Z + r.Z + s.Z) / 4), m2 = m1;
         a.AddM (p, q, q, r, r, s, s, p);

         //const float diag = 2.5f;
         //switch (tile.Proj) {
         //   case Tile.EProject.XY:
         //      m1 = new Point3f (m1.X, m1.Y, m1.Z + diag);
         //      m2 = new Point3f (m2.X, m2.Y, m2.Z - diag);
         //      break;
         //   case Tile.EProject.XZ:
         //      m1 = new Point3f (m1.X, m1.Y + diag, m1.Z);
         //      m2 = new Point3f (m2.X, m2.Y - diag, m2.Z);
         //      break;
         //   default:
         //      m1 = new Point3f (m1.X + diag, m1.Y, m1.Z);
         //      m2 = new Point3f (m2.X - diag, m2.Y, m2.Z);
         //      break;
         //}
         //a.AddM (m1, m2);
      }
      return a;
   }
   #endregion

   public static IObservable<Unlofter> NewTile => (mSubject ??= new ());
   static Subject<Unlofter>? mSubject;

   #region Nested types -------------------------------------------
   // This struct holds a UV space point and the corresponding 3D point
   readonly struct UV3D {
      public UV3D (E3Surface surf, double u, double v) {
         U = (float)u; V = (float)v;
         XYZ = (Point3f)surf.GetPoint (U, V);
      }
      public readonly float U;
      public readonly float V;
      public readonly Point3f XYZ;
   }

   // <summary>This unloder uses an adaptive grid of these tiles</summary>
   public struct Tile {
      #region Constructor -------------------------------
      /// <summary>Constructs a tile, given the 4 corner points making up the tile</summary>
      /// <param name="owner">The tile's owner ParaUnloft</param>
      /// <param name="a">Index of the bottom-left corner UV3D Point2</param>
      /// <param name="b">Index of the bottom-right corner</param>
      /// <param name="c">Index of the top-right corner</param>
      /// <param name="d">Index of the top-left corner</param>
      /// <param name="level">The Tile level </param>
      public unsafe Tile (Unlofter owner, int a, int b, int c, int d, int level) {
         int* pn = stackalloc int[4];
         pn[0] = A = a; pn[1] = B = b; pn[2] = C = c; pn[3] = D = d;

         // Compute the 3D bounding box of the tile, so we can see which is
         // a good projection axis
         Point3[] ptn = new Point3[4];
         for (int i = 0; i < 4; i++) ptn[i] = (Point3)owner.mNodes[pn[i]].XYZ;
         Vector3 vec = Geo.GetNormal (ptn);
         vec = new (Abs (vec.X), Abs (vec.Y), Abs (vec.Z));
         if (vec.X >= vec.Y && vec.X >= vec.Z) Proj = EProject.YZ;
         else if (vec.Y >= vec.X && vec.Y >= vec.Z) Proj = EProject.XZ;
         else Proj = EProject.XY;

         // Now, compute the 2-D shadow of this Tile on the appropriate
         // projection plane
         mnLevel = (byte)level;
         mnFirstChild = mcChildren = 0xff;
         var rect = mShadowBound = new Bound2 ();
         for (int i = 0; i < 4; i++) {
            Point3f pt = owner.mNodes[pn[i]].XYZ;
            rect += Project (pt);
         }
         mShadowBound = rect.InflatedF (1.1);
         mSubject?.OnNext (owner);
      }
      #endregion

      #region Properties --------------------------------
      /// <summary>Does this tile have any children, or is it a 'leaf-level' tile?</summary>
      public readonly bool HasChildren => mcChildren is > 0 and < 0xFF;

      public int Rung;
      #endregion

      #region Methods -----------------------------------
      /// <summary>The main method of the Tile; this returns the UV point for a given XYZ point, if it lies within the tile</summary>
      /// <param name="owner">The owner ParaUnloft object</param>
      /// <param name="idx">The index of this tile within the tiles list of that unlofter</param>
      /// <param name="xyz">The input point to unevaluate</param>
      /// <param name="uv">The output uv value we computed</param>
      /// <param name="iPass2">If this is set, we are much more relaxed about the point lying within the
      /// 'shadow' projection of the tile's quadrilateral onto the 2D plane. We inflate this shoadow more
      /// before testing the point for containment. The owner unloder first tries the unevaluate
      /// with this set to false (for all tiles). If that fails, it makes a second pass. This is necessary
      /// because for some highly curved parametric surfaces we might need considerable inflation of the shadow
      /// quads before the point is unevaluated correctly. However, doing this always will slow down unevaluation 
      /// in general, since there will be too many 'false positives'. Thus, we reserve this for 'pass 2'</param>
      /// <returns>True if the given XYZ point was found within this tile; false otherwise.</returns>
      internal bool GetUV (Unlofter owner, int idx, Point3 xyz, ref Point2 uv, bool iPass2) {
         // The Point2 does not lie within our 'shadow' as projected on one of
         // the orthogonal planes, then we can skip this. 
         Point2 s = Project ((Point3f)xyz);
         if (iPass2) {
            Bound2 sb2 = mShadowBound.InflatedF (2);
            if (!sb2.Contains (s)) return false;
         } else {
            if (!mShadowBound.Contains (s)) return false;
         }

         // If mcChildren is 0xffff, then we haven't decided yet whether we need
         // to sub-divide this tile or now. Figure that out now. First, if we are already
         // at level 6, then we don't want to subdivide any further.
         if (mcChildren == 0xff) Subdivide (owner, idx);

         if (mcChildren > 0) {
            // If we have some children, it is possible that one of them might contain
            // this Point2, let's recursively ask them and if any of them responds true, then
            // we can return this back to the owner. It is possible that none of them
            // might, since our Shadow is a bit inflated, and some points actually lying
            // outside might also have passed the earlier check. 
            for (int i = 0; i < mcChildren; i++)
               if (owner.mTiles[mnFirstChild + i].GetUV (owner, mnFirstChild + i, xyz, ref uv, iPass2))
                  return true;
            return false;
         }
         // Now, we 4 points a, b, c, d making up a quadrilateral in 2D space, and 
         // we also have the Point2 s that lies (ostensibly) somewhere within it. We have
         // to find out the u,v coordinates of s within a,b,c,d.
         Point2 a = Project (owner.mNodes[A].XYZ), b = Project (owner.mNodes[B].XYZ);
         Point2 c = Project (owner.mNodes[C].XYZ), d = Project (owner.mNodes[D].XYZ);
         Vector2 e = b - a, f = d - a, g = (a - b) + (c - d), h = s - a;

         // Compose the quadratic for v
         double u, v;
         double k2 = g.X * f.Y - g.Y * f.X;
         double k1 = e.X * f.Y - e.Y * f.X + g.Y * h.X - g.X * h.Y;
         double k0 = h.X * e.Y - e.X * h.Y;
         double toler = iPass2 ? 0.1 : Lib.Epsilon;
         if (k2.IsZero ()) {
            // If k2 is zero, then the 4 points make up a rectangle, and we have actually
            // a linear equation.
            if (k1.IsZero ()) v = 0.5; else v = -k0 / k1;
         } else {
            // If k2 is not zero, we have proper quadrilateral
            double tmp = k1 * k1 - 4 * k0 * k2;
            if (tmp < 0) return false;
            tmp = Math.Sqrt (tmp);
            v = (-k1 + tmp) / (2 * k2); double v1 = (-k1 - tmp) / (2 * k2);
            if (v < -toler || v > 1 + toler) v = v1;
         }

         // Now that we know v, compute the value for u
         double d1 = e.X + g.X * v, d2 = e.Y + g.Y * v;
         if (Math.Abs (d1) > Math.Abs (d2))
            u = (h.X - f.X * v) / d1;
         else
            u = (h.Y - f.Y * v) / d2;

         if (u.IsNaN ()) return false;
         if (v < -toler || v > 1 + toler || u < -toler || u > 1 + toler) return false;
         double left = owner.mNodes[A].U, right = owner.mNodes[B].U;
         double bottom = owner.mNodes[A].V, top = owner.mNodes[D].V;
         uv = new Point2 (left * (1 - u) + right * u, bottom * (1 - v) + top * v);
         return true;
      }
      #endregion

      #region Impementation -----------------------------
      // Given 2 nodes, this computes the middle node.
      // Returns true if the middle node needs a sub-division
      static bool GetMid (Unlofter owner, int a, int b, out UV3D mid) {
         UV3D ua = owner.mNodes[a], ub = owner.mNodes[b];
         mid = new UV3D (owner.Surf, (ua.U + ub.U) / 2, (ua.V + ub.V) / 2);
         Point3f mid2 = (ua.XYZ + ub.XYZ) * 0.5;
         return mid.XYZ.DistToSq (mid2) > Lib.FineTessSq;
      }

      // Another variant that works with two UV3D objects directly
      static bool GetMid (Unlofter owner, UV3D ua, UV3D ub, out UV3D mid) {
         mid = new UV3D (owner.Surf, (ua.U + ub.U) / 2, (ua.V + ub.V) / 2);
         Point3f mid2 = (ua.XYZ + ub.XYZ) * 0.5;
         return mid.XYZ.DistToSq (mid2) > Lib.FineTessSq;
      }

      // Given a point, projects it into one of the 2D planes XY, YZ or XZ.
      // We project based on the 'dominant axis' of this tile, which we computed right when the
      // tile was created
      readonly Point2 Project (Point3f pt) => Proj switch {
         EProject.XY => new Point2 (pt.X, pt.Y),
         EProject.XZ => new Point2 (pt.X, pt.Z),
         _ => new Point2 (pt.Y, pt.Z),
      };

      // This sub-divides a tile as needed.
      void Subdivide (Unlofter owner, int idx) {
         List<Tile> tiles = owner.mTiles;
         mnFirstChild = tiles.Count;
         if (mnLevel >= 12) { mcChildren = 0; owner.mTiles[idx] = this; return; }

         bool uSplit = GetMid (owner, A, B, out var e);
         uSplit |= GetMid (owner, C, D, out var g);
         bool vSplit = GetMid (owner, B, C, out var f);
         vSplit |= GetMid (owner, D, A, out var h);
         // Check if the mid Point2 requires a split in either U or V (for example,
         // consider a gaussian hump. This will have no boundary segments requesting a
         // split, but the mid Point2 will rise up and mandate a split in both U and V
         uSplit |= GetMid (owner, f, h, out _);
         vSplit |= GetMid (owner, e, g, out var m1);

         // Now, we know what we are going to split
         List<UV3D> nodes = owner.mNodes;
         int q = nodes.Count, nl = mnLevel + 1;
         if (uSplit && vSplit) {
            // We have to split in both U & V
            mcChildren = 4;
            nodes.AddM (e, f, g, h, m1);
            tiles.Add (new Tile (owner, A, q + 0, q + 4, q + 3, nl));  // A E M H
            tiles.Add (new Tile (owner, q + 0, B, q + 1, q + 4, nl));  // E B F M
            tiles.Add (new Tile (owner, q + 4, q + 1, C, q + 2, nl));  // M F C G
            tiles.Add (new Tile (owner, q + 3, q + 4, q + 2, D, nl));  // H M G D
         } else if (uSplit) {
            // Split only in U
            mcChildren = 2;
            nodes.AddM (e, g);
            tiles.Add (new Tile (owner, A, q + 0, q + 1, D, nl));    // A E G D
            tiles.Add (new Tile (owner, q + 0, B, C, q + 1, nl));    // E B C G
         } else if (vSplit) {
            // Split only in V
            mcChildren = 2;
            nodes.AddM (f, h);
            tiles.Add (new Tile (owner, A, B, q + 0, q + 1, nl));    // A B F H 
            tiles.Add (new Tile (owner, q + 1, q + 0, C, D, nl));    // H F C D
         } else {
            // No splits at all (the tile is linear enough)
            mcChildren = 0;
         }
         owner.mTiles[idx] = this;
      }
      #endregion

      #region Nested types ------------------------------
      // Which plane are we going to project our shadow upon?
      // This depends on which is the dominant axis of the tile's perpendicular vector.
      // Suppose that axis is Z, then we should project onto the XY plane. 
      internal enum EProject : byte { XY, YZ, XZ };
      #endregion

      #region Private data ------------------------------
      readonly Bound2 mShadowBound;       // The bounding rectangle of the 'shadow' in the projection plane
      internal readonly int A, B, C, D;   // Indices of the UV3D nodes making up this tile (these are into the parent's mNodes array)
      int mnFirstChild;                   // The index of the first child tile of this tile (this is into the parent's mTiles array)
      // What is the dominant axis for our tile (which plane are we projecting into).
      // This is used both for 'containment' testing to see whether a point lies within a tile,
      // and also later to do the actual interpolation between the 4 corners to find the UV of 
      // an interior point
      internal readonly EProject Proj;
      readonly byte mnLevel;              // The level of this tile within the tile tree
      byte mcChildren;                    // How many children do we have? (0xFF means undecided)
      #endregion
   }
   #endregion

   #region Private data ---------------------------------
   readonly E3Surface Surf;     // Owner parametric surface
   readonly List<Tile> mTiles = [];   // The list of tiles (this grows as the subdivision proceeds)
   // The list of UV+XYZ points (each contains an UV coordinate and a corresponding XYZ).
   // Each tile is defined by 4 corner points, and those are stored as simply indices within
   // this array. Most nodes (except ones on the boundaries) are shared by at least 4 tiles, 
   // so this saves a considerable amount of storage. (Why 'at least' 4 tiles? A node could
   // be used by a tile, which could subsequently get subdivided. One of the 4 children will
   // also share this node; thus a node could be used by more than 4 tiles, since there will
   // be tiles at various of various generations using the same node).
   readonly List<UV3D> mNodes = [];
   // The number of 'root' tiles we have.
   // The first few tiles are 'root' tiles of level 0; the rest are children. When
   // we are doing an unevaluate, we simply have to ask each of these root tiles to 
   // GetUV(). They will, in turn, ask their children. Beacuse of the adaptive nature 
   // of this algorithm, this means that they will actually 'beget' children as needed
   // to complete this query.
   readonly int mcRootTiles;
   #endregion
}
#endregion
