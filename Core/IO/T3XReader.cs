using System.IO.Compression;
namespace Nori;

public class T3XReader : IDisposable {
   public T3XReader (string file) {
      mZip = new (File.OpenRead (file), ZipArchiveMode.Read, false);
      T = mZip.ReadAllLines ("Data");
   }

   public Model3 Load () {
      if (T[0] != "T3X" || T[1].Trim () != "1") Fatal ("Not a valid T3X file");
      for (; ; ) {
         Ent3? ent = LoadEnt (); if (ent == null) break;
         mModel.Ents.Add (ent);
      }
      return mModel;
   }

   // Implementation -----------------------------------------------------------
   public void Dispose () => mZip.Dispose ();
   void Fatal (string s) => throw new Exception (s);
   void Fatal () => Fatal ($"Error on line {N}");

   Ent3? LoadEnt () {
      while (N < T.Count) {
         string s = T[N++]; if (s.IsBlank ()) continue;
         if (s.EndsWith ('*')) break;
         return s switch {
            "PLANE" => LoadPlane (),
            _ => null
         };
      }
      return null;
   }

   Edge3? LoadEdge () {
      for (; ; ) {
         string s = T[N++].Trim (); if (s.IsBlank ()) continue;
         if (s.EndsWith ('*')) break;
         return s switch {
            "LINE" => LoadLine (),
            "ARC" => LoadArc (),
            _ => null
         }; ;
      }
      return null;
   }

   List<Contour3> LoadContours () {
      List<Contour3> contours = [];
      for (; ; ) {
         string s = T[N++]; if (s.EndsWith ('*')) break;
         List<Edge3> edges = [];
         for (; ; ) {
            Edge3? edge = LoadEdge ();
            if (edge != null) edges.Add (edge);
            else break;
         }
         contours.Add (new ([.. edges]));
      }
      return contours;
   }

   Arc3 LoadArc () {

   }

   Line3 LoadLine () {
      int uid = LoadInt ();
      Match m = mRxPt2.Match (T[N++]); if (!m.Success) Fatal ();
      Point3 pa = new (GetD (m, 1), GetD (m, 2), GetD (m, 3));
      Point3 pb = new (GetD (m, 4), GetD (m, 5), GetD (m, 6));
      return new (uid, pa, pb);
   }
   static Regex mRxPt2 = new (@"\(([^,]*),([^,]*),([^\)]*)\)\s*\(([^,]*),([^,]*),([^\)]*)\)", RegexOptions.Compiled);

   E3Plane LoadPlane () {
      var (uid, cs) = (LoadInt (), LoadCS ());
      return new (uid, LoadContours (), cs);
   }

   // Core routines ------------------------------------------------------------
   double GetD (Match m, int n) => double.Parse (m.Groups[n].ValueSpan);

   int LoadInt () => int.Parse (T[N++]);

   CoordSystem LoadCS () {
      Match m = mRxCS.Match (T[N++]); if (!m.Success) Fatal ();
      Point3 org = new (GetD (m, 1), GetD (m, 2), GetD (m, 3));
      Vector3 vecx = new (GetD (m, 4), GetD (m, 5), GetD (m, 6));
      Vector3 vecy = new (GetD (m, 7), GetD (m, 8), GetD (m, 9));
      return new (org, vecx, vecy);
   }
   static Regex mRxCS = new (@"\(([^,]*),([^,]*),([^\)]*)\)\s*<([^,]*),([^,]*),([^>]*)>\s*<([^,]*),([^,]*),([^>]*)>", RegexOptions.Compiled);

   // Private data -------------------------------------------------------------
   List<string> T = [];                // Text of the T3X file
   int N = 2;                          // Next line index in the file
   readonly Model3 mModel = new ();    // The model we're constructing
   readonly ZipArchive mZip;           // Zip file we're loading from 
}
