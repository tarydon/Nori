namespace Nori;

public partial class ModelThickener {
   public ModelThickener (Model3 model) { mModel = model; mShModel = model; }

   public Model3 Process () {
      mModel.Ents.ForEach (a => a.IsTranslucent = true);
      var planes = mModel.Ents.OfType<E3Plane> ().OrderByDescending (a => a.Area).ToList ();
      if (ComputeBaseplane () is not TFlat tpBase) return mShModel;

      Queue<TFlex> todo2 = [];
      Queue<TFlat> todo1 = []; todo1.Enqueue (tpBase);
      while (todo1.Count + todo2.Count > 0) {
         while (todo1.TryDequeue (out TFlat? tp)) {
            mShModel.Ents.Add (tp.Get ());
            tp.GatherNeighbors (todo2);
         }
         while (todo2.TryDequeue (out TFlex? tf)) {
            if (tf.Build () is { } flex) mShModel.Ents.Add (flex);
         }
      }
      return mShModel;
   }

   TFlat? ComputeBaseplane () {
      E3Plane? pair = null;
      double minDist = double.MaxValue;
      var planes = mModel.Ents.OfType<E3Plane> ().OrderByDescending (a => a.Area).ToList ();

      E3Plane plane0 = planes[0];
      var (area0, pdef0) = (plane0.Area, new PlaneDef (plane0.CS));
      for (int i = 1; i < planes.Count; i++) {
         E3Plane plane1 = planes[i]; double area1 = plane1.Area;
         if (!area0.EQ (area1, 0.001)) break;
         double cos = plane0.CS.VecZ.CosineToAlreadyNormalized (plane1.CS.VecZ);
         if (!cos.EQ (-1)) continue;

         // We found a parallel plane, see if this could be a pair for this plane
         double dist = pdef0.Dist (plane1.CS.Org);
         if (dist < 0.01) continue;       // This is coplanar, cannot be the pair
         if (dist < minDist) (pair, minDist) = (plane1, dist);
      }
      if (pair == null) return null;
      Thickness = minDist;

      // Between plane0 and plane1, pick the one that is further away from the
      // center point of the model as the 'base' plane
      var pdef1 = new PlaneDef (pair.CS);
      Point3 center = mModel.Bound.Midpoint;
      if (pdef1.Dist (center) > pdef0.Dist (center))
         (plane0, pair, pdef0, pdef1) = (pair, plane0, pdef1, pdef0);
      return new (this, plane0, pdef0, pair);
   }

   // Private data -------------------------------------------------------------
   readonly Model3 mModel;             // The input surface model
   readonly Model3 mShModel;           // The output sheet-metal model
   HashSet<E3CSSurface> mUsed = [];
   double Thickness;
}

// Helper class for ModelThickener - represents a 'thick plane' (an E3Plane
// with its pair)
public partial class ModelThickener {
   class TFlat {
      public TFlat (ModelThickener owner, E3Plane p0, PlaneDef def0, E3Plane p1) {
         mOwner = owner; Plane0 = p0; mDef0 = def0; Plane1 = p1;
         owner.mUsed.Add (p0); owner.mUsed.Add (p1);
      }

      public E3Flat Get () {
         if (mFlat == null) {
            var (cs, set) = (Plane0.CS, Plane0.Polys.ToList ());
            if (mDef0.SignedDist (Plane1.CS.Org) < 0) {
               cs = new CoordSystem (cs.Org, cs.VecX, -cs.VecY);
               for (int i = 0; i < set.Count; i++) set[i] = (set[i] * Matrix2.VMirror).Reversed ();
            }
            cs += cs.VecZ * mOwner.Thickness / 2;
            mFlat = new E3Flat (mOwner.mShModel.Ents.Count + 1, cs, mOwner.Thickness, set);
         }
         return mFlat;
      }
      E3Flat? mFlat;

      public void GatherNeighbors (Queue<TFlex> todo) {
         var set0 = GetBendCylinders (Plane0); var set1 = GetBendCylinders (Plane1);
         foreach (var cyl0 in set0) {
            int n = GetPair (set1, cyl0);
            if (n != -1) {
               E3Cylinder cyl1 = set1[n];
               todo.Enqueue (new TFlex (mOwner, this, cyl0, cyl1));
            }
         }

         // Helper .........................................
         int GetPair (List<E3Cylinder> set, E3Cylinder cyl) {
            double thick = mOwner.Thickness;
            Vector3 vecz = cyl.CS.VecZ;
            for (int i = 0; i < set.Count; i++) {
               // The radii of the two cylinder should differ by 1 thickness
               E3Cylinder cyl1 = set[i];
               if (!Math.Abs (cyl.Radius - cyl1.Radius).EQ (thick, 0.001)) continue;
               // Cylinder axes should be parallel to each other (or anti-parallel)
               double cos = Math.Abs (vecz.CosineToAlreadyNormalized (cyl1.CS.VecZ));
               if (!cos.EQ (1)) continue;
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
         foreach (var cyl in mOwner.mModel.GetNeighbors (plane).OfType<E3Cylinder> ()) {
            if (mOwner.mUsed.Contains (cyl)) continue; 
            double cos = vec.CosineToAlreadyNormalized (cyl.CS.VecZ);
            if (cos.IsZero (1e-5)) set.Add (cyl);
         }
         return set; 
      }

      readonly ModelThickener mOwner;
      public readonly E3Plane Plane0, Plane1;
      readonly PlaneDef mDef0;
   }

   class TFlex {
      public TFlex (ModelThickener owner, TFlat parent, E3Cylinder c0, E3Cylinder c1) {
         mOwner = owner; mParent = parent; mCyl0 = c0; mCyl1 = c1;
         owner.mUsed.Add (c0); owner.mUsed.Add (c1);
      }

      public E3Flex? Build () {
         var model = mOwner.mModel;
         Lib.Trace ("Building...");

         var plane0 = mParent.Plane0; 
         if (model.GetSharedEdge (plane0, mCyl0) is not Line3 line0) return null;
         if (model.GetSharedEdge (mParent.Plane1, mCyl1) is not Line3 line1) return null;

         // Let's propose a foundation coordinate system for the Flex
         double radius = (mCyl0.Radius + mCyl1.Radius) / 2;
         Point3 side0 = line0.Midpoint, side1 = side0.SnappedToLine (line1.Start, line1.End);
         Point3 org = side0.Midpoint (side1);
         Vector3 vecx = (line0.Start - line0.End).Normalized (), vecz = plane0.CS.VecZ, vecy = vecz * vecx;

         // See if the cylinder lies on the +Y side of this proposed coordinate system
         List<Poly> trims = [];
         List<Point3> pts = ListPool<Point3>.Borrow ();
         List<Point2> uvs = ListPool<Point2>.Borrow ();
         var cylinder = mCyl0.Radius > mCyl1.Radius ? mCyl0 : mCyl1;
         bool upward = true; double angSpan = 0;

         // This is the transform we're going to use while projecting the E3Cylinder contours to
         // trimming curves on the E3Flex (we'll compute it in the first iteration below)
         Matrix3 xfmProj = Matrix3.Identity;

         try {
            for (int i = 0; i < cylinder.Contours.Length; i++) {
               pts.Clear (); uvs.Clear (); 
               cylinder.Contours[i].Discretize (pts, ETess.Medium);
               if (i == 0) {
                  // If this is the outer contour, do some checks to ensure the flex orientation.
                  // First, the +Y direction of the flex CS should point out of the plane, and
                  // into the flex. Check that:
                  PlaneDef pdef = new (org, vecy);
                  Bound1 bound = new (pts.Select (pdef.SignedDist));
                  if (bound.Mid < 0) (vecx, vecy) = (-vecx, -vecy);
                  // Next, determine of the flex is upward in Z 
                  pdef = new (org, vecz);
                  bound = new (pts.Select (pdef.SignedDist));
                  if (bound.Mid < 0) upward = false;

                  // Compute the xfmProj defined above here
                  Point3 fOrg = org.SnappedToLine (cylinder.CS.Org, cylinder.CS.Org + cylinder.CS.VecZ);
                  Vector3 fVecX = (org - fOrg).Normalized (), fVecZ = cylinder.CS.VecZ, fVecY = fVecZ * fVecX;
                  if (fVecY.Opposing (vecy)) fVecY = -fVecY;
                  xfmProj = Matrix3.From (new (fOrg, fVecX, fVecY));
               }

               // Next: compute the flex trimming curve corresponding to this contour. 
               foreach (var pt in pts) {
                  var ptf = pt * xfmProj;
                  uvs.Add (new (ptf.Z, radius * Math.Atan2 (ptf.Y, ptf.X)));
               }

               Bound1D yBound = new ();
               trims.Add (Poly.Lines (uvs, true).Clean ());
               foreach (var pt in uvs) yBound += pt.Y;
               Lib.Check (yBound.Mid > 0, "ModelThickener 2");
               if (i == 0) angSpan = yBound.Max / radius;
            }
         } finally { 
            ListPool<Point3>.Return (pts); 
         }
         return new E3Flex (mOwner.mShModel.Ents.Count + 1,
                     new CoordSystem (org, vecx, vecy),
                     mOwner.Thickness,
                     new BSpine (radius, angSpan, 0.5, upward),
                     trims);
      }

      readonly ModelThickener mOwner;
      readonly E3Cylinder mCyl0, mCyl1;
      readonly TFlat mParent;
   }
}
