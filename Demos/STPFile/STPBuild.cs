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
            _ => throw new BadCaseException (ec.Basis)
         };
      }
      return new Contour3 ([..mEdges]);
   }
   List<Edge3> mEdges = [];

   void Process (Manifold m) => Process ((Shell)D[m.Outer]!);
   void Process (Shell s) => s.Faces.ForEach (f => Process ((AdvancedFace)D[f]!));

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
   }
}