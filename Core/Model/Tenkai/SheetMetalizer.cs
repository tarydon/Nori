// ────── ╔╗
// ╔═╦╦═╦╦╬╣ SheetMetalizer.cs
// ║║║║╬║╔╣║ SheetMetalizer : converts surface models into sheet-metal models
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static SheetMetalizer;

#region class SheetMetalizer -----------------------------------------------------------------------
/// <summary>Converts a surface-model into a sheet-metal model that can be unfolded</summary>
/// This uses heuristics to convert a surface model to a sheet-metal model. The surface model
/// should be built with connectivity information, and should be a well-formed sheet metal model.
/// We support only models where the sheet thickness is uniform across the model (we cannot have
/// flanges or bends where the sheet-metal thickness is different than the one at the baseplane). 
public class SheetMetalizer {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a SheetMetalizer given a surface model to work with</summary>
   public SheetMetalizer (Model3 model) { Model = model; ShModel = new (); }

   // Properties ---------------------------------------------------------------
   /// <summary>Converts the surface model to a sheet-metal model</summary>
   public Result<Model3, EResult> Process () {
      Model.Ents.ForEach (a => a.IsTranslucent = true);
      if (ComputeBaseplane () is not SMFlatData tpBase) return EResult.CantFindBaseplane;
      if (Thickness is < 0.0999999 or > 25.4000001) return EResult.InvalidThickness;

      Queue<SMFlexData> todo2 = [];
      Queue<SMFlatData> todo1 = []; todo1.Enqueue (tpBase);
      try {
         while (todo1.Count + todo2.Count > 0) {
            while (todo1.TryDequeue (out SMFlatData? tp)) {
               tp.GatherNeighbors (todo2);
               ShModel.Ents.Add (tp.GetFlat ());
            }
            while (todo2.TryDequeue (out SMFlexData? tf)) {
               tf.GatherNeighbors (todo1);
               ShModel.Ents.Add (tf.GetFlex ());
            }
         }
      } catch (Exception) {
         return EResult.CantSheetMetalize;
      }
      return ShModel;
   }

   // Implementation -------------------------------------------------------------------------------
   // Helper used to mark the overlap-line between two surfaces with the 'IsOverlap' bit.
   // Note that the line-of-overlap is actually a pair of co-edges (one from each of the two adjacent
   // surfaces, we need to mark both of them as IsOverlap)
   internal void MarkOverlaps (E3Surface s1, E3Surface s2) {
      if (!Model.GetSharedEdges (s1, s2, TmpEdges)) Lib.Suspicious ();
      TmpEdges.ForEach (a => a.IsOverlap = true);
      (s1 as E3Plane)?.Polys = default;
      (s2 as E3Plane)?.Polys = default;
   }
   internal List<Curve3> TmpEdges = [];

   // Starting step of the sheet-metalization - we pick a suitable baseplane. 
   // The 'baseplane' is a potential E3Flat that we will build from a pair of planes that are similar,
   // parallel and one sheet-metal thickness apart. Here are the various checks we need to do here.
   // 
   // 1. Pick the largest plane by area, we assume this is one face (front/back) of the final sheet-
   //    metal baseplane (E3Flat) we are going to make (we call this "plane0"). 
   // 2. Pick the other parallel plane of similar area, and that will be our 'pair' to create the 
   //    E3Flat. The following additional checks are needed since we may find multiple candidates
   //    for this pair (we call that "pair")
   // 3. Multiple candidates may emerge for pair, all parallel to plane0 and all with the same area. 
   // 4. First step: thickness elimation - we measure the parallel distance between plane0 and pair
   //    (the sheet metal thickness). If this is zero, we can't use this. Otherwise, we pick the one
   //    that is minimal (to avoid selecting two matching planes from two opposite sides of a box)
   // 5. There may be ties even with the step 4 filtering. To break this, find the pair that has the
   //    same 'projection' as viewed from their common shared normal. This is to avoid selecting the
   //    bottom from one flange and the top from another parallel flange on the other side
   // If all these checks pass, we measure and store the thickness.
   SMFlatData? ComputeBaseplane () {
      E3Plane? pair = null;
      double minDist = double.MaxValue;
      var planes = Model.Ents.OfType<E3Plane> ().OrderByDescending (a => a.OuterArea).ToList ();

      E3Plane plane0 = planes[0];
      var (area0, pdef0) = (plane0.OuterArea, new PlaneDef (plane0.CS));
      for (int i = 1; i < planes.Count; i++) {
         E3Plane plane1 = planes[i]; double area1 = plane1.OuterArea;
         if (!area0.EQ (area1, EAREA)) break;
         double cos = plane0.CS.VecZ.CosineToAlreadyNormalized (plane1.CS.VecZ);
         if (!cos.EQ (-1, ECOS)) continue;

         // We found a parallel plane, see if this could be a pair for this plane
         double dist = pdef0.Dist (plane1.CS.Org);
         if (dist < ETHICK) continue;       // This is coplanar, cannot be the pair
         if (dist.EQ (minDist, ETHICK)) 
            pair = PickBetterPair (plane0, pair!, plane1);
         else 
            if (dist < minDist) (pair, minDist) = (plane1, dist);
      }
      if (pair == null) return null;
      Thickness = minDist;

      // Between plane0 and plane1, pick the one that is further away from the
      // center point of the model as the 'base' plane
      var pdef1 = new PlaneDef (pair.CS);
      Point3 center = Model.Bound.Midpoint;
      if (pdef1.Dist (center) > pdef0.Dist (center))
         (plane0, pair, pdef0, pdef1) = (pair, plane0, pdef1, pdef0);
      return new (this, null, plane0, pdef0, pair);
   }

   // Helper used in the ComputeBasePlane routine above to implement the step 5 of the 
   // filtering for 'pair'. This is also used by some of the assistance classes like TFlat. 
   internal E3Plane PickBetterPair (E3Plane plane0, E3Plane planeA, E3Plane planeB) {
      Poly p0 = plane0.Contours[0].Flatten (plane0.FromXfm);
      Bound2 b0 = p0.GetBound ();
      Poly pa = planeA.Contours[0].Flatten (plane0.FromXfm), pb = planeB.Contours[0].Flatten (plane0.FromXfm);
      Bound2 ba = b0 + pa.GetBound (), bb = b0 + pb.GetBound ();
      return ba.Area < bb.Area ? planeA : planeB;
   }

   // Helper used to pick a better 'pair cylinder' for the given cylinder cyl0
   internal E3Cylinder PickBetterPair (E3Cylinder cyl0, E3Cylinder cylA, E3Cylinder cylB) {
      Bound3 b0 = cyl0.Bound, bA = cylA.Bound + b0, bB = cylB.Bound + b0;
      return bA.Volume < bB.Volume ? cylA : cylB;
   }

   // Private data -------------------------------------------------------------
   internal HashSet<E3Surface> Used = [];             // These are the surfaces we've already used up
   internal static readonly double ETHICK = 0.01;     // Thickness DELTA for comparision
   internal static readonly double EAREA = 0.1;       // Area DELTA for comparisions
   internal static readonly double ECOS = 1e-5;       // Cosine DELTA for parallelness determination
   internal static readonly double EDIST = 0.0001;    // Points coincident if they are within this amount
   internal readonly Model3 Model;                    // The input surface model
   internal readonly Model3 ShModel;                  // The output sheet-metal model
   internal double Thickness;                         // Computed thickness
}
#endregion

#region class SMFlatData ---------------------------------------------------------------------------
/// <summary>SheetMetalizer uses this to store data about an E3Flat we are making</summary>
/// An E3Flat is defined by two E3Plane objects, marking the top and bottom faces of the flat. 
/// These planes should be parallel, similar in contours, and lie one thickness apart from each other.
/// In addition, this SMFlatData might also have an SMFlexData as its parent
class SMFlatData {
   // Constructors -------------------------------------------------------------
   /// <summary>Build an SMFlatData given the two E3Plane surfaces making up the top and bottom faces</summary>
   /// In addition, we also pass in the PlaneDef we've computed for p0 (since this is useful, and
   /// we already computed it and don't want to repeat that computation)
   public SMFlatData (SheetMetalizer owner, SMFlexData? parent, E3Plane p0, PlaneDef def0, E3Plane p1) {
      mOwner = owner; mParent = parent; Plane0 = p0; mDef0 = def0; Plane1 = p1;
      owner.Used.Add (p0); owner.Used.Add (p1);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The two planes making up the bottom and top surfaces of the thick-plane</summary>
   public readonly E3Plane Plane0, Plane1;

   // Methods ------------------------------------------------------------------
   /// <summary>Called to obtain the E3Flat that is synthesized from the given top/bottom E3Planes</summary>
   /// The E3Plane.Polys collection stores the contours of the E3Plane flattened into 2D
   /// using the local CS of the E3Plane. Since the E3Flat uses a similar organisation 
   /// (a CS with a set of Poly making the contours), this routine is fairly trivial. Note that
   /// the CS of the E3Flat lies in the middle of the thickness (so the shift by half the gap
   /// between Plane0 and Plane1)
   public E3Flat GetFlat () {
      if (mFlat != null) return mFlat;
      var (cs, set) = (Plane0.CS, Plane0.Polys.ToList ());
      cs += cs.VecZ * mDef0.SignedDist (Plane1.CS.Org) / 2;
      return mFlat = new E3Flat (mOwner.ShModel.Ents.Count + 1, cs, mOwner.Thickness, set) { Parent = mParent?.GetFlex () };
   }
   E3Flat? mFlat;

   /// <summary>Given an E3Flat, this examines neighbors and finds candidates to make E3Flexes from</summary>
   /// We want to find cylinders sharing an edge with the Plane0 and Plane1. We want to avoid
   /// side-walls, so we just take cylinders whose axes are perpendicular to the normal of the 
   /// plane. 
   public void GatherNeighbors (Queue<SMFlexData> todo) {
      var set0 = GetBendCylinders (Plane0); var set1 = GetBendCylinders (Plane1);
      // We want to gather pairs of cylinders from set0 (touching Plane0) and set1 (touching Plane1)
      // that will form the top and bottom faces of an E3Flex that we will create
      foreach (var cyl0 in set0) {
         int n = GetPair (cyl0, set1);
         if (n != -1) {
            E3Cylinder cyl1 = set1[n];
            mOwner.MarkOverlaps (Plane0, cyl0);
            mOwner.MarkOverlaps (Plane1, cyl1);
            todo.Enqueue (new SMFlexData (mOwner, this, cyl0, cyl1));
            set1.RemoveAt (n);
         }
      }
   }

   // Implementation -----------------------------------------------------------
   // Given a plane, gathers the bend-cylinders touching it. 
   // These cylinders should connect to the plane via a straight line, and the cylinder axis should
   // be perpendicular to the plane's normal. Only then could this possibly be a bend cylinder that
   // is connecting to the plane.
   // TODO: This still does not ensure tangentiality... 
   List<E3Cylinder> GetBendCylinders (E3Plane plane) {
      // Fetch only the neighbor cylinders whose axes are perpendicular to the 
      // normal of the plane. The ones whose axes are parallel to the normal of the plane are
      // cylinders making up the sidewalls
      List<E3Cylinder> set = [];
      Vector3 vec = plane.CS.VecZ;
      foreach (var cyl in mOwner.Model.GetNeighbors (plane).OfType<E3Cylinder> ()) {
         if (mOwner.Used.Contains (cyl)) continue;
         double cos = vec.CosineToAlreadyNormalized (cyl.CS.VecZ);
         if (cos.IsZero (1e-5)) set.Add (cyl);
      }
      return set;
   }

   // Given a set of cylinders (set), returns the one among them that could be a 'pair' for the
   // given cylinder cyl0 (this means that cylinder cyl1 is parallel to cyl0, has a radius
   // difference that is equal to sheet-metal thickness). 
   int GetPair (E3Cylinder cyl0, List<E3Cylinder> set) {
      double thick = mOwner.Thickness;
      Vector3 vecz = cyl0.CS.VecZ;
      int iBest = -1;
      for (int i = 0; i < set.Count; i++) {
         // The radii of the two cylinder should differ by 1 thickness
         E3Cylinder cyl1 = set[i];
         if (!Math.Abs (cyl0.Radius - cyl1.Radius).EQ (thick, ETHICK)) continue;
         // Cylinder axes should be parallel to each other (or anti-parallel)
         double cos = Math.Abs (vecz.CosineToAlreadyNormalized (cyl1.CS.VecZ));
         if (!cos.EQ (1, ECOS)) continue;
         // Actually, the two cylinder axes should be coincident
         double dist = cyl1.CS.Org.DistToLine (cyl0.CS.Org, cyl0.CS.Org + cyl0.CS.VecZ);
         if (!dist.IsZero (EDIST)) continue;
         if (iBest == -1 || mOwner.PickBetterPair (cyl0, set[iBest], cyl1) == cyl1)
            iBest = i;
      }
      return iBest;
   }

   // Private data -------------------------------------------------------------
   readonly SheetMetalizer mOwner;  // Sheet-metalizer we belong to
   readonly SMFlexData? mParent;    // Parent Flex (unless this is the root plane)
   readonly PlaneDef mDef0;         // The PlaneDef of the baseplane Plane0
}
#endregion

#region class SMFlexData ---------------------------------------------------------------------------
/// <summary>SheetMetalizer uses this to store data about an E3Flex we are making</summary>
/// An E3Flex is defined by two Cylinder objects, marking the top and bottom faces of the flex. 
/// These cylinders should be co-axial, should overlap along the axis and differ in radius by one
/// sheet-metal thickness. 
class SMFlexData {
   // Constructors -------------------------------------------------------------
   /// <summary>Create an SMFlexData given the two cylinders to work with, and the parent FlatData</summary>
   /// Note that since the sheet-metal hierarchy is always rooted in an E3Flat, each E3Flex will
   /// definitely have a parent E3Flat (and 'parent' is the pointer to the SMFlatData from which 
   /// that flat is built)
   public SMFlexData (SheetMetalizer owner, SMFlatData parent, E3Cylinder c0, E3Cylinder c1) {
      mOwner = owner; mParent = parent; mCyl0 = c0; mCyl1 = c1;
      owner.Used.Add (c0); owner.Used.Add (c1);
   }

   /// <summary>Compute the E3Flex from the bottom & top cylinder pair and returns it</summary>
   /// The trimming curve of the cylinder is in 3D space. We need to convert that into a 
   /// flex trimming curve (Poly) where for each node of the poly we have:
   /// - X is the linear distance along the cylinder, with 0 being at the csFlex origin,
   ///   and +X being along the csFlex.VecX direction
   /// - Y is the distance along the midpoint axis of the flex (where the Spine lies), 
   ///   exactly mid-way between the inner and outer cylinders. Y=0 corresponds to the
   ///   common line with the parent plane (this line also passes through csFlex.Org),
   ///   and +Y is along the spine curve (so Y goes to a maximum of Rmid * Theta, where
   ///   Rmid is the mid-point radius, and Theta is the angular span of the cylinder as it
   ///   wraps around the center axis
   public E3Flex GetFlex () {
      if (mFlex != null) return mFlex;
      GetFlexCS (out var csFlex, out var xfmProj, out var upward);

      List<Poly> trims = [];
      double radius = (mCyl0.Radius + mCyl1.Radius) / 2, angSpan = Lib.HalfPI;
      var cylinder = mCyl0.Radius > mCyl1.Radius ? mCyl0 : mCyl1;
      List<Point3> pts = ListPool<Point3>.Borrow ();
      try {
         // Compute the angular span with the outer contour. If we have computed our
         // xfmProj correctly, then the minimium of this should be 0 and the span should extend
         // in the +ve direction
         Bound1D yBound = new ();
         cylinder.Contours[0].Discretize (pts, ETess.VeryCoarse);
         foreach (var pt in pts) yBound += Flatten (pt).Y;
         Lib.Check (yBound.Mid > 0 && yBound.Min.EQ (0, 0.001));
         angSpan = yBound.Max / radius;

         // Now add the trimming curves for all the contours
         trims.AddRange (cylinder.Contours.Select (Discretize));
      } finally {
         ListPool<Point3>.Return (pts);
      }

      // Now we are ready to make the flex
      var spine = new BSpine (radius, angSpan, 0.5, upward);
      return mFlex = new E3Flex (mOwner.ShModel.Ents.Count + 1, csFlex, mOwner.Thickness, spine, trims) { Parent = mParent.GetFlat () };

      // Helpers ........................................
      Poly Discretize (Contour3 con) {
         PolyBuild pb = new ();
         pb.Begin (Flatten (con.Curves[0].Start), true);
         foreach (var curve in con.Curves) {
            if (curve is Line3 line) {
               pb.Line (Flatten (line.End));
               if (line.IsOverlap) pb.TagLastOverlap ();
            } else {
               pts.Clear (); curve.Discretize (pts, ETess.Medium);
               pts.Skip (1).ForEach (pt => pb.Line (Flatten (pt)));
            }
         }
         return pb.End (true);
      }

      Point2 Flatten (Point3 pt) {
         pt *= xfmProj;
         return new (pt.Z, radius * Math.Atan2 (pt.Y, pt.X));
      }
   }
   E3Flex? mFlex;

   /// <summary>This examines neighbors of this Flex and finds candidates to make downstream E3Flats from</summary>
   /// We want to find planes sharing linear edges with each of the two cylinders (see GetLeewardPlanes for more
   /// details). Then, with these two sets of planes, we try to find the 'pairs' that can be combined into
   /// the flats later (we just build SMFlatData and add those to the queue, and E3Flats will get built
   /// from those later)
   public void GatherNeighbors (Queue<SMFlatData> todo) {
      var set0 = GetLeewardPlanes (mCyl0); var set1 = GetLeewardPlanes (mCyl1);
      for (int i = 0; i < set0.Count; i++) {
         E3Plane plane0 = set0[i];
         var pdef = new PlaneDef (plane0.CS);
         int n = GetPair (set1, plane0, in pdef);
         if (n != -1) {
            E3Plane plane1 = set1[n];
            mOwner.MarkOverlaps (mCyl0, plane0);
            mOwner.MarkOverlaps (mCyl1, plane1);
            // It's possible we have already visited and converted this plane0-plane1 to a E3Flat.
            // If so, don't queue up a SMFlatData for processing. Note that we still don't eliminate
            // these 'already-visited' planes early, since we still need to do the MarkOverlaps call
            // shown above for those joints as well. 
            if (!mOwner.Used.Contains (plane0))
               todo.Enqueue (new SMFlatData (mOwner, this, plane0, pdef, plane1));
            set1.RemoveAt (n);
         }
      }
   }

   // Implementation -----------------------------------------------------------
   // Gathers the list of planes that could be connecting to this cylinder
   List<E3Plane> GetLeewardPlanes (E3Cylinder cylinder) {
      // We get planes that could be 'leeward planes' for this cylinder. We want to try and
      // eliminate any sidewalls etc here. So we pick planes whose normal is perpendicular to
      // the cylinder axis, and where the common shared line between the cylinder and the plane
      // is parallel to that axis
      List<E3Plane> set = [];
      var model = mOwner.Model;
      Vector3 axis = cylinder.CS.VecZ;
      foreach (var plane in model.GetNeighbors (cylinder).OfType<E3Plane> ()) {
         // Plane normal perpendicular to cylinder axis
         double cos = axis.CosineToAlreadyNormalized (plane.CS.VecZ);
         if (!cos.IsZero (1e-5)) continue;

         // Shared line parallel to cylinder axis. 
         if (!GetSharedLine (cylinder, plane, out var line)) continue;
         // Note that up to this point, we may have even picked a plane that we have already
         // visited (and have stored in mOwner.Used). However, since we might have reached this
         // plane by a different route, we still add that plane in (so that we can mark the
         // shared edge between these). However, we don't need to do this if the edge between
         // these two has already been marked as shared (see the check below)
         if (line.IsOverlap) continue;
         cos = Math.Abs ((line.End - line.Start).Normalized ().CosineToAlreadyNormalized (axis));
         if (cos.EQ (1, ECOS)) set.Add (plane);
      }
      return set;
   }

   // Given a set of planes, and a 'bottom' plane plane0, this picks the best candidate for
   // the corresponding 'top' plane plane1. Conditions: 
   // - Plane1 & plane0 should be parallel
   // - Should have equal areas
   // - Should be 1 thickness away from each other
   // - When viewed from the 'top', should project onto each other
   int GetPair (List<E3Plane> planes, E3Plane plane0, in PlaneDef pdef0) {
      int nResult = -1;
      double area0 = plane0.Area;
      for (int i = 0; i < planes.Count; i++) {
         E3Plane plane1 = planes[i];
         if (!area0.EQ (plane1.Area, EAREA)) continue;
         double cos = plane0.CS.VecZ.CosineToAlreadyNormalized (plane1.CS.VecZ);
         if (!cos.EQ (-1, ECOS)) continue;

         // We found a parallel plane, see if this could be a pair for this plane
         double dist = pdef0.Dist (plane1.CS.Org);
         if (!dist.EQ (mOwner.Thickness, ETHICK)) continue;
         if (nResult == -1 || mOwner.PickBetterPair (plane0, plane1, planes[nResult]) == plane1)
            nResult = i;
      }
      return nResult;
   }

   // Returns the shared line (if any) between two surfaces
   bool GetSharedLine (E3Surface s1, E3Surface s2, [NotNullWhen (true)] out Line3? line) {
      mOwner.Model.GetSharedEdges (s1, s2, mOwner.TmpEdges);
      line = mOwner.TmpEdges.OfType<Line3> ().FirstOrDefault ();
      return line != null;
   }

   // Computes some foundational parameter of the Flex we are going to build:
   // - csFlex is the rooting coordinate system of the Flex. This marks the 'base' of the flex
   //   where it attaches to the parent plane. csFlex.Org is located at that base-line, and is
   //   at the middle of the thickness. csFlex.X points along the common line between the parent
   //   flat and the flex, and csFlex.Y points 'outward' from the plane and is the tangent direction
   //   at which the flex starts off, and the curves up towards csFlex.Z (or down towards -csFlex.Z)
   // - upward specifies if the Flex curves upwards or downwards (in some ways, this is basically 
   //   the determinant of whether this going to finally be a mountain bend or a valley bend)
   // - xfmProj is a projection matrix that takes the cylinder and positions it in a coordinate
   //   space like this:
   //   = Org is at the midpoint of the axis of the cylinder
   //   = VecZ runs along the axis of the cylinder
   //   = VecX points toward the csFlex.Org
   //   Effectively, we are looking at the Flex 'edge on' when we view from this xfmProj's
   //   local Z. Then, the Z coordinate in this projection is directly the X value in the Flex's
   //   local trimming space, and the Radius*Atan2(Y,X) is the Y value in the Flex's local trimming
   //   space (where Radius is the mean radius between the two cylinders).
   bool GetFlexCS (out CoordSystem csFlex, out Matrix3 xfmProj, out bool upward) {
      var model = mOwner.Model;
      var plane0 = mParent.Plane0;
      csFlex = CoordSystem.World; xfmProj = Matrix3.Identity; upward = true;
      if (!GetSharedLine (plane0, mCyl0, out var line0)) return false;
      if (!GetSharedLine (mParent.Plane1, mCyl1, out var line1)) return false;

      // Let's try to gather the components of the flex coordinate system
      Point3 side0 = line0.Midpoint, side1 = side0.SnappedToLine (line1.Start, line1.End);
      Point3 csOrg = side0.Midpoint (side1);
      Vector3 csVecX = (line0.Start - line0.End).Normalized (), csVecZ = plane0.CS.VecZ, csVecY = csVecZ * csVecX;

      // Let's see if the cylinder mass lies on the +Y side of this proposed coordinate system
      var (outer, inner) = (mCyl0, mCyl1);
      if (outer.Radius < inner.Radius) (outer, inner) = (inner, outer);
      var pts = ListPool<Point3>.Borrow ();
      try {
         // The +Y direction of the flex CS should point OUT of the plane and into the Flex. 
         // Check that first:
         inner.Contours[0].Discretize (pts, ETess.VeryCoarse);
         PlaneDef pdef = new (csOrg, csVecY);
         Bound1 bound = new (pts.Select (pdef.SignedDist));
         if (bound.Mid < 0) (csVecX, csVecY) = (-csVecX, -csVecY);
         // Next, check if the flex is turned upward in Z
         pdef = new (csOrg, csVecZ);
         bound = new (pts.Select (pdef.SignedDist));
         if (bound.Mid < 0) upward = false;

         // Compute a projection in which the cylinder winds CCW around the axis, with 0 at
         // the shared line with the parent plane, and +ve going into the flex. We'll use this to 
         // get the correct parametrization of the E3Flex trimming curve
         Point3 projOrg = csOrg.SnappedToLine (outer.CS.Org, outer.CS.Org + outer.CS.VecZ);
         Vector3 projVecX = (csOrg - projOrg).Normalized (), projVecZ = outer.CS.VecZ, projVecY = projVecZ * projVecX;
         if (projVecY.Opposing (csVecY)) projVecY = -projVecY;
         csFlex = new (csOrg, csVecX, csVecY);

         Point3 fOrg = csOrg.SnappedToLine (outer.CS.Org, outer.CS.Org + outer.CS.VecZ);
         Vector3 fVecX = (csOrg - projOrg).Normalized (), fVecZ = outer.CS.VecZ, fVecY = projVecZ * projVecX;
         if (projVecY.Opposing (csVecY)) projVecY = -projVecY;
         xfmProj = Matrix3.From (new (projOrg, projVecX, projVecY));
      } finally {
         ListPool<Point3>.Return (pts);
      }
      return true;
   }

   // Private data -------------------------------------------------------------
   readonly SheetMetalizer mOwner;
   readonly E3Cylinder mCyl0, mCyl1;
   readonly SMFlatData mParent;
}
#endregion
