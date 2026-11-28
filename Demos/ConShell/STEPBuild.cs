using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Nori.STEP;
namespace Nori;
using static Nori.STEP.EKind;

public partial class STEPReader {
   void Build () {
      var span = ShapeRepr.AsSpan ();
      for (int i = 0; i < span.Length; i++) {
         ref readonly ShapeRepr sr = ref span[i];
         if (sr.Kind == ADVANCED_BREP_SHAPE_REPRESENTATION) {
            bool done = false;
            foreach (var n in sr.Items) {
               var (kind, index) = Data[n];
               switch (kind) {
                  case MANIFOLD_SOLID_BREP:
                     DoShell (Manifold[index], sr.Context); done = true;
                     break;
                  case BREP_WITH_VOIDS:
                     var voids = BRepVoids[index];
                     DoShell (voids.Outer, sr.Context);
                     foreach (var k in voids.Inner) DoShell (k, sr.Context);
                     done = true;
                     break;
               }
               if (mEnts.Count > 0) {
                  Block3 block = new (index.ToString (), mEnts);
                  Color4 color = Color4.RandomLight;
                  mEnts.ForEach (a => a.Color = color);
                  mModel.Add (block);
                  mModel.Ents.Add (new E3Insert (index, block, Matrix3.Identity));
                  mEnts.Clear (); 
               }
            }
            Check (done, 3);
         }
      }
      mModel.CleanupBlocks ();
      if (mModel.Blocks.Count == 0) {
         foreach (var ent in mModel.Ents) ent.Color = Color4.Nil;
      }
      Simplify ();
   }

   void Simplify () {
      for (int i = 0; i < mModel.Ents.Count; i++) {
         if (mModel.Ents[i] is E3NurbsSurface nurb)
            if (nurb.TrySimplify (out var ent)) 
               mModel.Ents[i] = ent;
      }
   } 

   void DoShell (int nShell, int nContext) {
      // First, try to get the units from the CONTEXT
      mEnts.Clear ();
      (mScale, mAngScale, mFudge) = ParseContext (nContext);
      mFudge = Math.Max (mFudge * 1.01, 0.01);

      if (Data[nShell].Kind == ORIENTED_CLOSED_SHELL) 
         nShell = OShell[Data[nShell].Index].Shell;
      var shell = Ints[GetIndex (nShell, CLOSED_SHELL)];
      foreach (var n in shell.Items) {
         if (OnlyThisFace > 0 && n != OnlyThisFace) continue;
         if (DoFace (n) is { } ent) mEnts.Add (ent);
      }
      if (mScale != 1) {
         var xfm = Matrix3.Scaling (mScale);
         for (int i = 0; i < mEnts.Count; i++) mEnts[i] *= xfm;
      }
   }
   List<Ent3> mEnts = [];
   double mScale = 1, mAngScale = 1, mFudge = 0.01;

   public static int OnlyThisFace;

   // Builds a face (surface), given a pointer to an ADVANCED_FACE. 
   Ent3 DoFace (int nFace) {
      Face face = Face[GetIndex (nFace, ADVANCED_FACE)];
      var (kind, index) = Data[face.Surface];
      List<Contour3> atrims = DoContours (face.Contours, out bool outerFound);
      ImmutableArray<Contour3> trims = [.. atrims];
      switch (kind) {
         case PLANE:
            var cs = MakeCoordSys (Int[index], face.Reverse);
            if (!outerFound && atrims.Count > 1) {
               // Sometimes, the outer contour of a plane is not specified as FACE_OUTER_BOUND. 
               // If so, we need to pick the largest contour and shuffle it to index 0 within the
               // set of contours
               var xfm = Matrix3.From (cs);
               int iOuter = atrims.MaxIndexBy (a => a.Flatten (xfm).GetArea (ETess.Coarse));
               if (iOuter != 0) { 
                  Contour3 co = atrims[iOuter]; atrims.RemoveAt (iOuter); atrims.Insert (0, co);
                  trims = [.. atrims];
               }
            }
            return new E3Plane (nFace, trims, cs);
         case CYLINDRICAL_SURFACE:
            Cylinder cyl = Cylinder[index];
            return new E3Cylinder (nFace, trims, MakeCoordSys (cyl.CoordSys), cyl.Radius, face.Reverse);
         case SPHERICAL_SURFACE:
            Sphere sph = Sphere[index];
            return new E3Sphere (nFace, trims, MakeCoordSys (sph.CoordSys), sph.Radius) { IsNormalFlipped = face.Reverse };
         case TOROIDAL_SURFACE or DEGENERATE_TOROIDAL_SURFACE:
            Torus tor = Torus[index];
            bool isMinor = kind == DEGENERATE_TOROIDAL_SURFACE && !tor.Apple;
            return new E3Torus (nFace, trims, MakeCoordSys (tor.CoordSys), tor.RMajor, tor.RMinor) { IsNormalFlipped = face.Reverse, IsMinorTorus = isMinor };
         case CONICAL_SURFACE:
            Cone con = Cone[index];
            double halfAngle = con.HalfAngle * mAngScale;
            var csys = MakeCoordSys (con.CoordSys); csys += csys.VecZ * (-con.Radius / Math.Tan (halfAngle));
            return new E3Cone (nFace, trims, csys, halfAngle);
         case B_SPLINE_SURFACE_WITH_KNOTS:
            BSplineSurface spline = SplineSurface[index];
            mTmpPts.Clear ();
            foreach (var row in spline.Pts)
               foreach (var n in row) mTmpPts.Add (Point3[GetIndex (n, CARTESIAN_POINT)]);
            var uknots = GetKnots (spline.UMultiplicities, spline.UKnots);
            var vknots = GetKnots (spline.VMultiplicities, spline.VKnots);
            return new E3NurbsSurface (nFace, [.. mTmpPts], [], spline.Pts.Length, uknots, vknots, trims) { IsNormalFlipped = face.Reverse };
         case QUASI_UNIFORM_SURFACE:
            return DoQuasiSurface (nFace, face.Surface, trims, face.Reverse);
         case COMPOUND:
            return DoNURBSurface (nFace, face.Surface, trims, face.Reverse);
         case SURFACE_OF_LINEAR_EXTRUSION or SURFACE_OF_REVOLUTION or OFFSET_SURFACE:
            return null!;  // TO-DO support these
      }
      throw new STEPException (16, kind);
   }

   Ent3 DoQuasiSurface (int id, int index, ImmutableArray<Contour3> trims, bool flipped) {
      QuasiSurface qs = QuasiSurface[Data[index].Index];
      mTmpPts.Clear ();
      foreach (var row in qs.Pts)
         foreach (var n in row) mTmpPts.Add (Point3[GetIndex (n, CARTESIAN_POINT)]);
      var uknots = MakeUniformKnots (qs.Pts.Length, qs.UDegree);
      var vknots = MakeUniformKnots (qs.Pts[0].Length, qs.VDegree);
      return new E3NurbsSurface (id, [.. mTmpPts], [], qs.Pts.Length, uknots, vknots, trims) { IsNormalFlipped = flipped };
   }

   Curve3 DoQuasiCurve (int id, int index) {
      QuasiCurve qc = QuasiCurve[Data[index].Index];
      mTmpPts.Clear (); foreach (var n in qc.Pts) mTmpPts.Add (Point3[GetIndex (n, CARTESIAN_POINT)]);
      if (qc.Degree == 1) {
         Check (mTmpPts.Count == 2, 25);
         return new Line3 (id, mTmpPts[0], mTmpPts[1]);
      }
      return new NurbsCurve3 (id, [.. mTmpPts], MakeUniformKnots (mTmpPts.Count, qc.Degree), []);
   }

   ImmutableArray<double> MakeUniformKnots (int N, int p) {
      mTmpDouble.Clear ();
      for (int i = 0; i < p + 1; i++) mTmpDouble.Add (0);
      for (int j = 1; j < N - p - 1; j++) mTmpDouble.Add (j);
      for (int i = 0; i < p + 1; i++) mTmpDouble.Add (N - p);
      return [..mTmpDouble];
   }

   Curve3 DoNURBCurve (int id, int index) {
      var span = GetCompoundBytes (index);
      Check (Seek (span, "B_SPLINE_CURVE"u8), 24); mR.Match ('(');
      int deg = RInt (); var pts = RRefs (); ImmutableArray<double> knots;
      if (Seek (span, "B_SPLINE_CURVE_WITH_KNOTS"u8)) {
         mR.Match ('(');
         int[] mults = RInts (); knots = GetKnots (mults, RDoubles ());
      } else {
         Check (Seek (span, "QUASI_UNIFORM_CURVE"u8), 24);
         knots = MakeUniformKnots (pts.Length, deg);
      }
      Check (Seek (span, "RATIONAL_B_SPLINE_CURVE"u8), 24); mR.Match ('(');
      double[] weights = RDoubles ();
      mTmpPts.Clear ();
      foreach (var n in pts) mTmpPts.Add (Point3[GetIndex (n, CARTESIAN_POINT)]);
      return new NurbsCurve3 (id, [.. mTmpPts], knots, [.. weights]); 
   }

   Ent3 DoNURBSurface (int id, int index, ImmutableArray<Contour3> trims, bool flipped) {
      var span = GetCompoundBytes (index);
      Check (Seek (span, "B_SPLINE_SURFACE"u8), 22); mR.Match ('(');
      int uDeg = RInt (), vDeg = RInt (); var pts = RRefsList ();
      ImmutableArray<double> uknots, vknots;
      if (Seek (span, "B_SPLINE_SURFACE_WITH_KNOTS"u8)) {
         mR.Match ('(');
         int[] uMults = RInts (), vMults = RInts ();
         uknots = GetKnots (uMults, RDoubles ());
         vknots = GetKnots (vMults, RDoubles ());
      } else {
         Check (Seek (span, "QUASI_UNIFORM_SURFACE"u8), 24);
         uknots = MakeUniformKnots (pts.Length, uDeg);
         vknots = MakeUniformKnots (pts[0].Length, vDeg);
      }
      Check (Seek (span, "RATIONAL_B_SPLINE_SURFACE"u8), 22); mR.Match ('(');
      var weights = RDoublesList ();
      mTmpPts.Clear (); mTmpDouble.Clear ();
      foreach (var row in pts)
         foreach (var n in row) mTmpPts.Add (Point3[GetIndex (n, CARTESIAN_POINT)]);
      foreach (var row in weights)
         foreach (var f in row) mTmpDouble.Add (f);
      return new E3NurbsSurface (id, [.. mTmpPts], [.. mTmpDouble], pts.Length, uknots, vknots, trims) { IsNormalFlipped = flipped };
   }

   ImmutableArray<double> GetKnots (int[] multiplicities, double[] knots) {
      mTmpDouble.Clear ();
      for (int i = 0; i < multiplicities.Length; i++) {
         var (mult, knot) = (multiplicities[i], knots[i]);
         for (int j = 0; j < mult; j++) mTmpDouble.Add (knot);
      }
      return [.. mTmpDouble];
   }

   List<Contour3> DoContours (int[] contours, out bool outerFound) {
      mTmpContours.Clear ();
      Check (contours.Length > 0, 17);
      outerFound = false; 
      for (int i = 0; i < contours.Length; i++) {
         mTmpCurves.Clear ();
         FaceBound faceBound = FaceBound[GetIndex (contours[i], FACE_BOUND, FACE_OUTER_BOUND)];
         var tup = Data[faceBound.EdgeLoop]; if (tup.Kind == VERTEX_LOOP) continue; 
         var oedges = Ints2[GetIndex (faceBound.EdgeLoop, EDGE_LOOP)];
         if (faceBound.Reverse) {
            for (int j = oedges.Length - 1; j >= 0; j--) mTmpCurves.Add (DoCurve (oedges[j], true));
         } else {
            foreach (var oe in oedges) mTmpCurves.Add (DoCurve (oe, false));
         }
         if (mTmpCurves.Count == 0) continue;

         Curve3 c0 = mTmpCurves[^1];
         for (int k = 0; k < mTmpCurves.Count; k++) {
            Curve3 c1 = mTmpCurves[k];
            Check (c0.End.EQ (c1.Start, mFudge), 21);
            c0 = c1;
         }
         var contour = new Contour3 ([.. mTmpCurves]);
         if (Data[contours[i]].Kind == FACE_OUTER_BOUND) { outerFound = true; mTmpContours.Insert (0, contour); }
         else mTmpContours.Add (contour);
      }
      return mTmpContours;
   }
   List<Curve3> mTmpCurves = [];
   List<Contour3> mTmpContours = [];

   Curve3 DoCurve (int id, bool reverse) {
      var oedge = OEdge[GetIndex (id, ORIENTED_EDGE)];
      reverse ^= oedge.Reverse;
      int edgeId = oedge.Edge;
      var ecurve = Edge[GetIndex (edgeId, EDGE_CURVE)];

      if (mEdges.TryGetValue (edgeId, out var curve)) {
         reverse ^= ecurve.Reverse;
         return reverse ? new RevCurve3 (curve) : curve;
      }

      Point3 start = DoPoint (ecurve.Start), end = DoPoint (ecurve.End);
      var tup = Data[ecurve.Curve];
      if (reverse) (start, end) = (end, start);
      reverse ^= ecurve.Reverse;

      if (tup.Kind == SURFACE_CURVE)
         tup = Data[SurfCurve[tup.Index].Curve3D];

      switch (tup.Kind) {
         case LINE: 
            curve = new Line3 (edgeId, start, end); 
            break;
         case CIRCLE: 
            Circle circle = Circle[tup.Index];
            curve = MakeArc (edgeId, circle, start, end, reverse);
            break;
         case ELLIPSE:
            Ellipse ell = Ellipse[tup.Index];
            var cs = MakeCoordSys (ell.CoordSys, reverse);
            Matrix3 xfmFlat = Matrix3.From (cs);
            Point3 ps = start * xfmFlat, pe = end * xfmFlat;
            Point2 ps2 = new (ps.X / ell.RMajor, ps.Y / ell.RMinor), pe2 = new (pe.X / ell.RMajor, pe.Y / ell.RMinor);
            double sang = Point2.Zero.AngleTo (ps2), eang = Point2.Zero.AngleTo (pe2);
            curve = new Ellipse3 (edgeId, cs, ell.RMajor, ell.RMinor, sang, eang);
            break;
         case B_SPLINE_CURVE_WITH_KNOTS:
            BSplineCurve spline = SplineCurve[tup.Index];
            if (spline.Degree == 1) curve = new Line3 (edgeId, start, end);
            else {
               mTmpPts.Clear (); mTmpDouble.Clear ();
               foreach (var n in spline.Pts) mTmpPts.Add (Point3[GetIndex (n, CARTESIAN_POINT)]);
               var knots = GetKnots (spline.Multiplicities, spline.Knots);
               curve = CacheAndReturn (new NurbsCurve3 (edgeId, [.. mTmpPts], knots, []), reverse);
            }
            break;
         case QUASI_UNIFORM_CURVE:
            curve = CacheAndReturn (DoQuasiCurve (edgeId, ecurve.Curve), reverse);
            break;
         case COMPOUND:
            curve = CacheAndReturn (DoNURBCurve (edgeId, ecurve.Curve), reverse);
            break;
         default: throw new STEPException (23, tup.Kind);
      }

      Curve3 CacheAndReturn (Curve3 curve, bool reverse) {
         mEdges.Add (edgeId, curve);
         if (reverse) curve = new RevCurve3 (curve);
         return curve;
      }
      return curve;
   }
   List<Point3> mTmpPts = [];
   List<double> mTmpDouble = [];
   Dictionary<int, Curve3> mEdges = [];

   CoordSystem MakeCoordSys (int nCS, bool flipped = false) {
      var (kind, index) = Data[nCS];
      Check (kind == AXIS2_PLACEMENT_3D, 11);
      var cs = Axis3D[index];
      Vector3 zAxis = BVector (cs.ZAxis), xAxis;
      xAxis = (cs.XAxis == -1) ? Geo.GetXYFromZ (zAxis).x : BVector (cs.XAxis);
      Vector3 yAxis = flipped ? xAxis * zAxis : zAxis * xAxis;
      return new (BPoint (cs.Origin), xAxis, yAxis);
   }

   Point3 DoPoint (int nVertex) {
      int nCartesian = Int[GetIndex (nVertex, VERTEX_POINT)];
      return Point3[GetIndex (nCartesian, CARTESIAN_POINT)];
   }

   Point3 BPoint (int n) => Point3[Data[n].Index];

   Vector3 BVector (int n) => Vector3[Data[n].Index];

   // Helpers ------------------------------------------------------------------
   // Gets the index given an object id, after verifying that the expected kind 
   // of object is stored at that id
   int GetIndex (int id, EKind kind) {
      var tup = Data[id];
      if (tup.Kind != kind) throw new STEPException (15, kind, tup.Kind, id);
      return tup.Index;
   }

   int GetIndex (int id, EKind k1, EKind k2) {
      var tup = Data[id];
      if (tup.Kind != k1 && tup.Kind != k2) throw new STEPException (18, k1, k2, tup.Kind);
      return tup.Index;
   }

   ReadOnlySpan<byte> GetCompoundBytes (int id) {
      var tup = Data[id]; if (tup.Kind != COMPOUND) return [];
      int end = mRaw.AsSpan (tup.Index).IndexOf (';'); if (end < 0) return [];
      return mRaw.AsSpan (tup.Index, end);
   }

   string GetCompoundString (int id) => Encoding.ASCII.GetString (GetCompoundBytes (id));

   Arc3 MakeArc (int pairId, Circle circle, Point3 start, Point3 end, bool reverse) {
      CoordSystem cs = MakeCoordSys (circle.CoordSys, reverse);
      OptCheck (cs.Org.DistTo (start).EQ (circle.Radius, MAGIC_ArcRadiusError), 19);
      OptCheck (cs.Org.DistTo (end).EQ (circle.Radius, MAGIC_ArcRadiusError), 19);

      // Try to compose a local coordinate system for this Arc3
      // 1. The center is just cs.Org (the original center of the underlying circle)
      // 2. The x-axis is the direction of the start point from the origin (we divide
      //    by Radius to normalize this, avoiding a square-root calculation)
      // 3. If the Arc was winding CCW about the original ZAxis, then we can keep
      //    that as our final Zaxis as well, otherwise we flip it
      Vector3 xaxis = (start - cs.Org).Normalized ();

      // Now, compose a new coordinate system in which this arc is canonically
      // defined
      double angSpan;
      var csFinal = new CoordSystem (cs.Org, xaxis, cs.VecZ * xaxis);
      if (start.EQ (end)) angSpan = Lib.TwoPI;
      else {
         Vector3 endV = (end - cs.Org) / circle.Radius;
         angSpan = Lib.Acos (csFinal.VecX.CosineToAlreadyNormalized (endV));
         if (endV.Opposing (csFinal.VecY)) angSpan = Lib.TwoPI - angSpan;
      }
      var a3 = new Arc3 (pairId, csFinal, circle.Radius, angSpan);
      OptCheck (a3.Start.EQ (start, MAGIC_ArcRadiusError), 19);
      OptCheck (a3.End.EQ (end, MAGIC_ArcRadiusError), 19);
      return a3;
   }
   const double MAGIC_ArcRadiusError = 0.01;

   bool Seek (ReadOnlySpan<byte> line, ReadOnlySpan<byte> word) {
      int pos = line.IndexOf (word); if (pos < 0) return false;
      int start = (int)Unsafe.ByteOffset (ref mRaw[0], ref MemoryMarshal.GetReference (line));
      mR.Pos = start + pos + word.Length;
      return true; 
   }

   bool FindInCompound (int id, ReadOnlySpan<byte> word) => Seek (GetCompoundBytes (id), word);
}
