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
         case Plane p: Check (p); break;
         case Cylinder c: Check (c); break;
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

   void Check (ClosedShell a) {
      foreach (var n in a.Faces) 
         Check ((AdvancedFace)D[n]!);
   }

   void Check (Cylinder c) { Check ((CoordSys)D[c.CoordSys]!); }

   void Check (Direction d) { }

   void Check (EdgeCurve a) {
      Check ((VertexPoint)D[a.Start]!); 
      Check ((VertexPoint)D[a.End]!);
      switch (D[a.Basis]!) {
         case Line l: Check (l); break;
         case Circle c: Check (c); break;
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

   void Check (Manifold a) { Check ((ClosedShell)D[a.Outer]!); }

   void Check (OrientedEdge a) { Check ((EdgeCurve)D[a.Edge]!); }

   void Check (Plane p) { Check ((CoordSys)D[p.CoordSys]!); }

   void Check (Vector v) { Check ((Direction)D[v.Direction]!); }

   void Check (VertexPoint v) { Check ((Cartesian)D[v.Cartesian]!); }

   void Check (int n) {
      if (n >= D.Count || D[n] == null) Console.WriteLine ($"Unread: {Unread[n]}");
      else Console.WriteLine ($"Implement check for {D[n]!.GetType ().Name}");
   }
}