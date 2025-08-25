using System.Security.Cryptography;
using Nori.STEP;
namespace Nori;

partial class STEPReader {
   void Check (AdvancedFace a) {
      foreach (var n in a.Contours) {
         switch (D[n]) {
            case FaceOuterBound f1: Check (f1); break;
            case FaceBound f2: Check (f2); break;
            default: Check (n); break;
         }
      }
      switch (D[a.Face]) {
         case ElementarySurface e: Check (e); break;
         case BSplineSurfaceWithKnots b: Check (b); break;
         case ExtrudedSurface e: Check (e); break;
         default: Check (a.Face); break;
      }
   }

   void Check (Cartesian c) { }

   void Check (Circle c) { Check ((CoordSys)D[c.CoordSys]!); }

   void Check (CoordSys cs) {
      Check ((Cartesian)D[cs.Origin]!);
      Check ((Direction)D[cs.XAxis]!);
      Check ((Direction)D[cs.YAxis]!);
   }

   void Check (Shell a) {
      foreach (var n in a.Faces)
         Check ((AdvancedFace)D[n]!);
   }

   void Check (Direction d) { }

   void Check (Ellipse e) { Check ((CoordSys)D[e.CoordSys]!); }

   void Check (EdgeCurve a) {
      Check ((VertexPoint)D[a.Start]!);
      Check ((VertexPoint)D[a.End]!);
      switch (D[a.Basis]!) {
         case Line l: Check (l); break;
         case Circle c: Check (c); break;
         case Ellipse e: Check (e); break;
         case BSplineCurveWithKnots b: Check (b); break;
         case SurfaceCurve s: Check (s); break;
         default: Check (a.Basis); break;
      }
   }

   void Check (EdgeLoop a) {
      foreach (var n in a.Edges)
         Check ((OrientedEdge)D[n]!);
   }

   void Check (FaceOuterBound a) { Check ((EdgeLoop)D[a.EdgeLoop]!); }
   void Check (FaceBound a) { Check ((EdgeLoop)D[a.EdgeLoop]!); }

   void Check (Line a) {
      Check ((Cartesian)D[a.Start]!);
      Check ((Vector)D[a.Ray]!);
   }

   void Check (BSplineCurveWithKnots b) {
      foreach (var n in b.Pts) Check ((Cartesian)D[n]!);
   }

   void Check (BSplineSurfaceWithKnots b) {
      foreach (var al in b.Pts)
         foreach (var n in al) Check ((Cartesian)D[n]!);
   }

   void Check (ExtrudedSurface e) {
      Check ((EdgeCurve)D[e.Curve]!);
      Check ((Direction)D[e.Vector]!);
   }

   void Check (Manifold a) { Check ((Shell)D[a.Outer]!); }

   void Check (ShellBasedSurfaceModel s) {
      foreach (var n in s.Shells)
         Check ((Shell)D[n]!);
   }

   void Check (OrientedEdge a) { Check ((EdgeCurve)D[a.Edge]!); }

   void Check (ElementarySurface s) { Check ((CoordSys)D[s.CoordSys]!); }

   void Check (SurfaceCurve s) {
      // TODO
   } 

   void Check (Vector v) { Check ((Direction)D[v.Direction]!); }

   void Check (VertexPoint v) { Check ((Cartesian)D[v.Cartesian]!); }

   void Check (int n) {
      if (n >= D.Count || D[n] == null) {
         if (mReported.Add (Unread[n])) throw new Exception ($"Unread: {Unread[n]}");
      } else
         throw new Exception ($"Implement check for {D[n]!.GetType ().Name}");
   }
   HashSet<string> mReported = [];
}