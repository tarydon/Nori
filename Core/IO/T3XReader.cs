using System.IO.Compression;
namespace Nori;

public class T3XReader : IDisposable {
   public T3XReader (string file) {
      mZip = new (File.OpenRead (file), ZipArchiveMode.Read, false);
      T = mZip.ReadAllText ("Data");
   }

   public Model3 Load () {
      if (RWord () != "T3X" || RInt () != 1) Fatal ("Not a T3X file");
      for (; ; ) {
         Ent3? ent = RWord () switch {
            "PLANE" => LoadPlane (),
            "*" => null,
            _ => null,
         };
         if (ent == null) break;
         mModel.Ents.Add (ent);
      }
      return mModel;
   }

   // Implementation -----------------------------------------------------------
   public void Dispose () => mZip.Dispose ();
   void Fatal (string s) => throw new Exception (s);
   void Fatal () => Fatal ($"Error on line {N}");

   E3Plane LoadPlane () {
      var (uid, cs) = (RInt (), RCS ());
      return new (uid, LoadContours (), cs);
   }

   Edge3? LoadEdge () 
      => RWord () switch {
         "LINE" => LoadLine (),
         "ARC" => LoadArc (),
         "*" => null,
         _ => null
      };


   List<Contour3> LoadContours () {
      List<Contour3> contours = [];
      List<Edge3> edges = [];
      for (; ; ) {
         if (RWord () == "*") break;
         for (; ; ) {
            Edge3? edge = LoadEdge ();
            if (edge == null) break;
            edges.Add (edge);
         }
         contours.Add (new ([.. edges]));
         edges.Clear ();
      }
      return contours;
   }

   Arc3 LoadArc () {
      int id = RInt ();
      double rad = RDouble (), span = RDouble ();
      return new Arc3 (id, RCS (), rad, span);
   }

   Line3 LoadLine () => new (RInt (), RPoint (), RPoint ());

   // Low level routines -------------------------------------------------------
   CoordSystem RCS () => new (RPoint (), RVector (), RVector ());
   double RDouble () { var (a, b) = Slice (); return double.Parse (T.AsSpan (a, b - a)); }
   double RDouble (char ch) { var (a, b) = SliceTo (ch); return double.Parse (T.AsSpan (a, b - a)); }
   int RInt () { var (a, b) = Slice (); return int.Parse (T.AsSpan (a, b - a)); }
   void RMatch (char ch) { SkipSpace (); if (T[N++] != ch) Fatal (); }
   string RWord () { var (a, b) = Slice (); return T[a..b]; }
   (int A, int B) Slice () { SkipSpace (); int a = N; ToSpace (); return (a, N); }
   (int A, int B) SliceTo (char ch) { int a = N; while (T[N++] != ch) { }; return (a, N - 1); } 
   void SkipSpace () { while (char.IsWhiteSpace (T[N])) N++; }
   void ToSpace () { while (!char.IsWhiteSpace (T[N])) N++; }

   Point3 RPoint () {
      RMatch ('(');
      double x = RDouble (','), y = RDouble (','), z = RDouble (')');
      return new (x, y, z);
   }

   Vector3 RVector () {
      RMatch ('<');
      double x = RDouble (','), y = RDouble (','), z = RDouble ('>');
      return new (x, y, z);
   }

   // Private data -------------------------------------------------------------
   string T = "";                      // Text of the T3X file
   int N = 0;                          // Character position within the file
   readonly Model3 mModel = new ();    // The model we're constructing
   readonly ZipArchive mZip;           // Zip file we're loading from 
}
