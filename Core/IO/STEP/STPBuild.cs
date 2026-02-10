// ────── ╔╗
// ╔═╦╦═╦╦╬╣ STPBuild.cs
// ║║║║╬║╔╣║ Implements the 'Build' phase of STEP reading
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori.STEP;
namespace Nori;

// Implements the build phase of STEP reading.
// This converts the Nori.STEP.Entity objects into Model3 surfaces, curves etc
partial class STEPReader {
   // Methods ------------------------------------------------------------------
   public Model3 Load () {
      if (mModel.Ents.Count == 0) {
         Parse ();
         foreach (var m in D.OfType<Manifold> ()) Process (m);
         foreach (var s in D.OfType<ShellBasedSurfaceModel> ()) Process (s);
         foreach (var gs in D.OfType<GeometricSet> ()) Process (gs);
      }
      return mModel;
   }
   Model3 mModel = new ();

   // Implementation -----------------------------------------------------------
   // Given a vertex point object, fetches the underlying point
   Point3 GetPoint (int nVertexPoint) {
      var vp = (VertexPoint)D[nVertexPoint]!;
      var cp = (Cartesian)D[vp.Cartesian]!;
      return cp.Pt;
   }

   // Given a cartesian point object, fetches the underlying point
   Point3 GetCartesianPoint (int nCartesian) =>((Cartesian)D[nCartesian]!).Pt;

   CoordSystem GetCoordSys (int nCoordSys) {
      CoordSys cs = (CoordSys)D[nCoordSys]!;
      Point3 org = ((Cartesian)D[cs.Origin]!).Pt;
      Vector3 zaxis = GetDirection (cs.ZAxis), xaxis = GetDirection (cs.XAxis);
      return new (org, xaxis, zaxis * xaxis);
   }

   // Given the direction object, fetches the underlying Vector3
   Vector3 GetDirection (int nVector)
      => ((Direction)D[nVector]!).Vec;

   // Given the Vector object, returns a Vector3 of the specified direction and length
   Vector3 GetVector (int nVector) {
      Vector v = (Vector)D[nVector]!;
      var dir = (Direction)D[v.Direction]!;
      return dir.Vec.Normalized () * v.Length;
   }

   Arc3 MakeArc (int pairId, Circle circle, Point3 start, Point3 end, bool ccw) {
      CoordSystem cs = GetCoordSys (circle.CoordSys);
      Lib.Check (cs.Org.DistTo (start).EQ (circle.Radius), "MakeArc.1");
      Lib.Check (cs.Org.DistTo (end).EQ (circle.Radius), "MakeArc.2");

      // Try to compose a local coordinate system for this Arc3
      // 1. The center is just cs.Org (the original center of the underlying circle)
      // 2. The x-axis is the direction of the start point from the origin (we divide
      //    by Radius to normalize this, avoiding a square-root calculation)
      // 3. If the Arc was winding CCW about the original ZAxis, then we can keep
      //    that as our final Zaxis as well, otherwise we flip it
      Vector3 xaxis = (start - cs.Org) / circle.Radius;
      Vector3 zaxis = ccw ? cs.VecZ : -cs.VecZ;

      // Now, compose a new coordinate system in which this arc is canonically
      // defined
      double angSpan;
      var csFinal = new CoordSystem (cs.Org, xaxis, zaxis * xaxis);
      if (start.EQ (end)) angSpan = Lib.TwoPI;
      else {
         Vector3 endV = (end - cs.Org) / circle.Radius;
         angSpan = Lib.Acos (csFinal.VecX.CosineToAlreadyNormalized (endV));
         if (endV.Opposing (csFinal.VecY)) angSpan = Lib.TwoPI - angSpan;
      }
      var a3 = new Arc3 (pairId, csFinal, circle.Radius, angSpan);
      Lib.Check (a3.Start.EQ (start), "MakeArc.3");
      Lib.Check (a3.End.EQ (end), "MakeArc.4");
      return a3;
   }

   Contour3 MakeContour (int edgeLoop, bool dir, bool outer) {
      mEdges.Clear ();
      EdgeLoop el = (EdgeLoop)D[edgeLoop]!;
      foreach (var n in el.Edges) {
         OrientedEdge oe = (OrientedEdge)D[n]!;
         EdgeCurve ec = (EdgeCurve)D[oe.Edge]!;
         Point3 start = GetPoint (ec.Start), end = GetPoint (ec.End);
         if (!oe.Dir) (start, end) = (end, start);
         Curve3 edge = getEdge (ec.Basis);
         mEdges.Add (edge);

         // helper function (sometimes called recursively)
         Curve3 getEdge (int cent) => D[cent] switch {
            Line => new Line3 (oe.Edge, start, end),
            Circle circle => MakeArc (oe.Edge, circle, start, end, !(!ec.SameSense ^ !oe.Dir)),
            SurfaceCurve sc => getEdge (sc.Curve),
            _ => throw new BadCaseException (cent)
         };
      }
      for (int i = 0; i < mEdges.Count; i++)
         Lib.Check (mEdges[i].End.EQ (mEdges[(i + 1) % mEdges.Count].Start), "MakeContour");
      return new Contour3 ([..mEdges]);
   }
   List<Curve3> mEdges = [];

   E3Plane MakePlane (int id, Plane plane, ImmutableArray<Contour3> contours, bool aligned) {
      var cs = GetCoordSys (plane.CoordSys);
      if (!aligned) cs = new (cs.Org, cs.VecX, -cs.VecY);
      return new E3Plane (id, contours, cs);
   }

   E3Cylinder MakeCylinder (int id, Cylinder cylinder, ImmutableArray<Contour3> contours, bool aligned)
      => E3Cylinder.Build (id, contours, GetCoordSys (cylinder.CoordSys), cylinder.Radius, !aligned);

   E3Surface MakeSurfaceOfRevolution (int id, SpunSurface spunSurface, ImmutableArray<Contour3> contours, bool aligned) {
      Axis axis = (Axis)D[spunSurface.Axis]!;
      Point3 org = GetCartesianPoint (axis.Origin); Vector3 zaxis = GetDirection (axis.Direction).Normalized ();

      Curve3 generatix = D[spunSurface.Curve] switch {
         Line line => new Line3 (0, GetCartesianPoint (line.Start), GetCartesianPoint (line.Start) + GetVector (line.Ray)),
         _ => throw new BadCaseException (spunSurface.Curve) // TODO support other curves
      };

      Vector3 yaxis = (zaxis * ((org.DistToSq (generatix.End) > org.DistToSq (generatix.Start) ? generatix.End : generatix.Start) - org)).Normalized (), xaxis = yaxis * zaxis;
      var cs = new CoordSystem (org, xaxis, yaxis);
      generatix *= Matrix3.From (cs); // This is now expected to be a line in the XZ plane.

      if (generatix is Line3 ln && !ln.Start.Z.EQ (ln.End.Z)) {
         if (ln.Start.X.EQ (ln.End.X)) { // If the line is parallel to the Z axis, then we have a cylinder, otherwise a cone.
            return E3Cylinder.Build (id, contours, cs, ((Point2)ln.Start).DistTo (Point2.Zero), !aligned);
         } else { // Cone.
            var gv = ln.End - ln.Start;
            return new E3Cone (id, contours, cs, Math.Atan (Math.Abs (gv.X / gv.Z)));
         }
      } else {
         var ret = new E3SpunSurface (id, contours, cs, generatix);
         if (!aligned) ret.FlipNormal ();
         return ret;
      }
   }

   void Process (Manifold m)
      => Process ((Shell)D[m.Outer]!);

   void Process (ShellBasedSurfaceModel s)
      => s.Shells.ForEach (n => Process ((Shell)D[n]!));

   void Process (Shell s)
      => s.Faces.ForEach (f => Process ((AdvancedFace)D[f]!));

   void Process (AdvancedFace a) {
      Lib.Check (a.Contours.Length > 0, "Contours.Length > 0");
      var fb0 = (FaceBound)D[a.Contours[0]]!; 
      Lib.Check (fb0.Outer == true, "First contour is FaceOuterBound");

      List<Contour3> cons = [];
      foreach (var n in a.Contours) {
         Contour3 c = D[n] switch {
            FaceBound fb => MakeContour (fb.EdgeLoop, fb.Dir, false),
            _ => throw new BadCaseException (n)
         };
         cons.Add (c);
      }

      ImmutableArray<Contour3> contours = [.. cons];
      Ent3 ent = D[a.Face] switch {
         Plane plane => MakePlane (a.Id, plane, contours, a.Dir),
         Cylinder cylinder => MakeCylinder (a.Id, cylinder, contours, a.Dir),
         SpunSurface ss => MakeSurfaceOfRevolution (a.Id, ss, contours, a.Dir),
         _ => throw new BadCaseException (a.Face)
      };
      mModel.Ents.Add (ent);
   }

   void Process (GeometricSet gs) {
      foreach (var n in gs.Items)
         if (D[n] is CompositeCurve cc)
            Process (cc);
   }

   void Process (CompositeCurve cc) {
      mEdges.Clear ();
      // TODO build the segments into mEdges
      mModel.Ents.Add (new Contour3 ([..mEdges]));
   }
}
