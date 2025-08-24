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
   Contour3 MakeContour (int edgeLoop, bool dir, bool outer) {
      EdgeLoop el = (EdgeLoop)D[edgeLoop]!;
      foreach (var n in el.Edges) {
         OrientedEdge oe = (OrientedEdge)D[n]!;
         EdgeCurve ec = (EdgeCurve)D[oe.Edge]!;
         Console.Write ($"{D[ec.Basis]!.GetType ().Name} ");
      }
      Console.WriteLine ();
      Console.ResetColor ();
      return new ();
   }
   
   void Process (Manifold m) => Process ((Shell)D[m.Outer]!);
   void Process (Shell s) => s.Faces.ForEach (f => Process ((AdvancedFace)D[f]!));

   void Process (AdvancedFace a) {
      if (D[a.Face]!.GetType ().Name == "Plane") return;
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