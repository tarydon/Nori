// ────── ╔╗
// ╔═╦╦═╦╦╬╣ STPBuild.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
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

   Arc3 MakeCircle (Circle circle, Point3 start, Point3 end, bool ccw) {
      CoordSystem cs = GetCoordSys (circle.CoordSys);
      Debug.Assert (cs.Org.DistTo (start).EQ (circle.Radius));
      Debug.Assert (cs.Org.DistTo (end).EQ (circle.Radius));

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
      var a3 = new Arc3 (csFinal, circle.Radius, angSpan);
      Debug.Assert (a3.Start.EQ (start));
      Debug.Assert (a3.End.EQ (end));
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
            Line line => new Line3 (start, end),
            Circle circle => MakeCircle (circle, start, end, ec.SameSense),
            _ => throw new BadCaseException (ec.Basis)
         };
         mEdges.Add (edge);
      }
      for (int i = 0; i < mEdges.Count; i++)
         Debug.Assert (mEdges[i].End.EQ (mEdges[(i + 1) % mEdges.Count].Start));
      return new Contour3 ([..mEdges]);
   }
   List<Edge3> mEdges = [];

   Ent3? MakePlane (Plane plane, List<Contour3> contours) {
      var cs = GetCoordSys (plane.CoordSys);

      foreach (var con in contours) {  // REMOVETHIS
         foreach (var edge in con.Edges) {
            Console.WriteLine ($"{edge.GetType ().Name}  {edge.Start.R6 ()}  {edge.End.R6 ()}");
         }
         Console.WriteLine ("---");
      }
      Console.WriteLine ("======");
      var dwg = new Dwg2 ();
      foreach (var con in contours)
         dwg.Add (con.Flatten (cs));
      DXFWriter.Save (dwg, $"C:/Etc/Dump/{++n}.dxf");

      return new E3Plane (cs, contours.Select (a => a.Flatten (cs)));
   }
   static int n = 0;

   Ent3? MakeCylinder (Cylinder cylinder, List<Contour3> contours) {
      return null;
   }

   void Process (Manifold m)
      => Process ((Shell)D[m.Outer]!);

   void Process (Shell s)
      => s.Faces.ForEach (f => Process ((AdvancedFace)D[f]!));

   void Process (AdvancedFace a) {
      Debug.Assert (a.Contours.Length > 0);
      Debug.Assert (D[a.Contours[0]]!.GetType ().Name == "FaceOuterBound");

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
         Plane plane => MakePlane (plane, contours),
         Cylinder cylinder => MakeCylinder (cylinder, contours),
         _ => throw new BadCaseException (a.Face)
      };
      if (ent != null) mModel.Ents.Add (ent);
   }
}
