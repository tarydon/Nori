// ────── ╔╗
// ╔═╦╦═╦╦╬╣ SheetMetalizerAux.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static SheetMetalizer;

class SMFlatData {
   public SMFlatData (SheetMetalizer owner, SMFlexData? parent, E3Plane p0, PlaneDef def0, E3Plane p1) {
      mOwner = owner; mParent = parent; Plane0 = p0; mDef0 = def0; Plane1 = p1;
      owner.Used.Add (p0); owner.Used.Add (p1);
   }

   public E3Flat GetFlat () {
      if (mFlat == null) {
         var (cs, set) = (Plane0.CS, Plane0.Polys.ToList ());
         cs += cs.VecZ * mDef0.SignedDist (Plane1.CS.Org) / 2;
         mFlat = new E3Flat (mOwner.ShModel.Ents.Count + 1, cs, mOwner.Thickness, set) { Parent = mParent?.GetFlex () };
      }
      return mFlat;
   }
   E3Flat? mFlat;

   public void GatherNeighbors (Queue<SMFlexData> todo) {
      var set0 = GetBendCylinders (Plane0); var set1 = GetBendCylinders (Plane1);
      foreach (var cyl0 in set0) {
         int n = GetPair (set1, cyl0);
         if (n != -1) {
            E3Cylinder cyl1 = set1[n];
            mOwner.MarkOverlaps (Plane0, cyl0);
            mOwner.MarkOverlaps (Plane1, cyl1);
            todo.Enqueue (new SMFlexData (mOwner, this, cyl0, cyl1));
            set1.RemoveAt (n);
         }
      }

      // Helper .........................................
      int GetPair (List<E3Cylinder> set, E3Cylinder cyl) {
         double thick = mOwner.Thickness;
         Vector3 vecz = cyl.CS.VecZ;
         for (int i = 0; i < set.Count; i++) {
            // The radii of the two cylinder should differ by 1 thickness
            E3Cylinder cyl1 = set[i];
            if (!Math.Abs (cyl.Radius - cyl1.Radius).EQ (thick, ETHICK)) continue;
            // Cylinder axes should be parallel to each other (or anti-parallel)
            double cos = Math.Abs (vecz.CosineToAlreadyNormalized (cyl1.CS.VecZ));
            if (!cos.EQ (1, ECOS)) continue;
            return i;
         }
         return -1;
      }
   }

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

   readonly SheetMetalizer mOwner;
   readonly SMFlexData? mParent;
   public readonly E3Plane Plane0, Plane1;
   readonly PlaneDef mDef0;
}

class SMFlexData {
   public SMFlexData (SheetMetalizer owner, SMFlatData parent, E3Cylinder c0, E3Cylinder c1) {
      mOwner = owner; mParent = parent; mCyl0 = c0; mCyl1 = c1;
      owner.Used.Add (c0); owner.Used.Add (c1);
   }

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
            todo.Enqueue (new SMFlatData (mOwner, this, plane0, pdef, plane1));
            set1.RemoveAt (n);
         }
      }
   }

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
         if (mOwner.Used.Contains (plane)) continue;
         double cos = axis.CosineToAlreadyNormalized (plane.CS.VecZ);
         if (!cos.IsZero (1e-5)) continue;

         // Shared line parallel to cylinder axis
         if (model.GetSharedEdge (cylinder, plane) is not Line3 line) continue;
         cos = Math.Abs ((line.End - line.Start).Normalized ().CosineToAlreadyNormalized (axis));
         if (!cos.EQ (1, 1e-5)) continue;

         set.Add (plane);
      }
      return set;
   }

   bool GetFlexCS (out CoordSystem csFlex, out Matrix3 xfmProj, out bool upward) {
      var model = mOwner.Model;
      var plane0 = mParent.Plane0;
      csFlex = CoordSystem.World; xfmProj = Matrix3.Identity; upward = true;
      if (model.GetSharedEdge (plane0, mCyl0) is not Line3 line0) return false;
      if (model.GetSharedEdge (mParent.Plane1, mCyl1) is not Line3 line1) return false;

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

   public E3Flex? GetFlex () {
      if (mFlex != null) return mFlex;
      if (!GetFlexCS (out var csFlex, out var xfmProj, out var upward)) return null;

      List<Poly> trims = [];
      double radius = (mCyl0.Radius + mCyl1.Radius) / 2, angSpan = Lib.HalfPI;
      var cylinder = mCyl0.Radius > mCyl1.Radius ? mCyl0 : mCyl1;
      List<Point3> pts = ListPool<Point3>.Borrow ();
      try {
         // Compute the angular span with the outer contour
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
      mFlex = new E3Flex (mOwner.ShModel.Ents.Count + 1, csFlex, mOwner.Thickness, spine, trims) { Parent = mParent.GetFlat () };
      return mFlex;

      // Helpers ........................................
      Poly Discretize (Contour3 con) {
         PolyBuild2 pb = new ();
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

   readonly SheetMetalizer mOwner;
   readonly E3Cylinder mCyl0, mCyl1;
   readonly SMFlatData mParent;
}
