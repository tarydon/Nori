// ────── ╔╗
// ╔═╦╦═╦╦╬╣ STPBuild.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori.STEP;
namespace Nori;

partial class STEPReader {
   public Model3 Build () {
      foreach (var m in D.OfType<Manifold> ()) Process (m);
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

   CoordSystem GetCoordSys (int nCoordSys) {
      CoordSys cs = (CoordSys)D[nCoordSys]!;
      Point3 org = ((Cartesian)D[cs.Origin]!).Pt;
      Vector3 zaxis = GetVector (cs.ZAxis), xaxis = GetVector (cs.XAxis);
      return new (org, xaxis, zaxis * xaxis);
   }

   Vector3 GetVector (int nVector)
      => ((Direction)D[nVector]!).Vec;

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
         Edge3 edge = D[ec.Basis] switch {
            Line line => new Line3 (oe.Edge, start, end),
            Circle circle => MakeArc (oe.Edge, circle, start, end, !(!ec.SameSense ^ !oe.Dir)),
            _ => throw new BadCaseException (ec.Basis)
         };
         mEdges.Add (edge);
      }
      for (int i = 0; i < mEdges.Count; i++)
         Lib.Check (mEdges[i].End.EQ (mEdges[(i + 1) % mEdges.Count].Start), "MakeContour");
      return new Contour3 ([..mEdges]);
   }
   List<Edge3> mEdges = [];

   E3Plane MakePlane (int id, Plane plane, List<Contour3> contours, bool aligned) {
      var cs = GetCoordSys (plane.CoordSys);
      if (!aligned) cs = new (cs.Org, cs.VecX, -cs.VecY);
      return new E3Plane (id, contours, cs);
   }

   E3Cylinder MakeCylinder (int id, Cylinder cylinder, List<Contour3> contours, bool aligned)
      => E3Cylinder.Build (id, contours, GetCoordSys (cylinder.CoordSys), cylinder.Radius, !aligned);

   void Process (Manifold m)
      => Process ((Shell)D[m.Outer]!);

   void Process (Shell s)
      => s.Faces.ForEach (f => Process ((AdvancedFace)D[f]!));

   void Process (AdvancedFace a) {
      Lib.Check (a.Contours.Length > 0, "Contours.Length > 0");
      Lib.Check (D[a.Contours[0]]!.GetType ().Name == "FaceOuterBound", "First contour is FaceOuterBound");
      if (a.Id != 235) return;

      List<Contour3> contours = [];
      foreach (var n in a.Contours) {
         Contour3 c = D[n] switch {
            FaceBound fb => MakeContour (fb.EdgeLoop, fb.Dir, false),
            FaceOuterBound fob => MakeContour (fob.EdgeLoop, fob.Dir, true),
            _ => throw new BadCaseException (n)
         };
         contours.Add (c);
      }
      Ent3? ent = D[a.Face] switch {
         Plane plane => MakePlane (a.Id, plane, contours, a.Dir),
         Cylinder cylinder => MakeCylinder (a.Id, cylinder, contours, a.Dir),
         _ => throw new BadCaseException (a.Face)
      };
      if (ent != null) mModel.Ents.Add (ent);
   }
}
