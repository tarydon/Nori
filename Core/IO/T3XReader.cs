// ────── ╔╗
// ╔═╦╦═╦╦╬╣ T3XReader.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
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
         string type = RWord ();
         E3Surface? ent = type switch {
            "CONE" => LoadCone (),
            "CYLINDER" => LoadCylinder (),
            "NURBSSURFACE" => LoadNurbsSurface (),
            "PLANE" => LoadPlane (),
            "SPUNSURFACE" => LoadSpunSurface (),
            "SWEPTSURFACE" => LoadSweptSurface (),
            "TORUS" => LoadTorus (),
            "*" => null,
            _ => throw new BadCaseException (type)
         };
         if (ent == null) break;
         ent.Mesh = LoadMesh (ent.Id);
         mModel.Ents.Add (ent);
      }
      return mModel;
   }

   // Implementation -----------------------------------------------------------
   Arc3 LoadArc () {
      int id = RInt ();
      double rad = RDouble (), span = RDouble ();
      return new Arc3 (id, RCS (), rad, span);
   }

   (List<Point3>, List<double>) LoadCtrlPts () {
      List<Point3> ctrl = [];
      List<double> weight = [];
      while (!RDone ()) {
         ctrl.Add (RPoint ()); weight.Add (RDouble ());
      }
      return (ctrl, weight);
   }

   E3Cone LoadCone () {
      var (uid, rad, hangle, cs) = (RInt (), RDouble (), RDouble (), RCS ());
      double height = rad / Math.Tan (hangle);
      cs += cs.VecZ * -height;
      return new E3Cone (uid, LoadContours (), cs, hangle);
   }

   E3Cylinder LoadCylinder () {
      var (uid, rad, cs) = (RInt (), RDouble (), RCS ());
      return new E3Cylinder (uid, LoadContours (), cs, rad);  // REMOVETHIS - infacing not set correctly
   }

   List<Contour3> LoadContours () {
      List<Contour3> contours = [];
      List<Curve3> edges = [];
      for (; ; ) {
         if (RWord () == "*") break;
         for (; ; ) {
            Curve3? edge = LoadEdge ();
            if (edge == null) break;
            edges.Add (edge);
         }
         contours.Add (new ([.. edges]));
         edges.Clear ();
      }
      return contours;
   }

   Curve3? LoadEdge () {
      string type = RWord ();
      return type switch {
         "ARC" => LoadArc (),
         "ELLIPSE" => LoadEllipse (),
         "LINE" => LoadLine (),
         "NURBSCURVE" => LoadNurbsCurve (),
         "POLYLINE" => LoadPolyline (),
         "*" => null,
         _ => throw new BadCaseException (type),
      };
   }

   Ellipse3 LoadEllipse () {
      var (id, rx, ry, a0, a1) = (RInt (), RDouble (), RDouble (), RDouble (), RDouble ());
      return new Ellipse3 (id, RCS (), rx, ry, a0, a1);
   }

   List<double> LoadKnots () {
      List<double> knot = [];
      while (!RDone ()) {
         double k = RDouble (); int rep = RInt ();
         for (int i = 0; i < rep; i++) knot.Add (k);
      }
      return knot;
   }

   Line3 LoadLine () 
      => new (RInt (), RPoint (), RPoint ());

   Mesh3 LoadMesh (int id) {
      using var ms = new MemoryStream (mZip.ReadAllBytes ($"{id}.meshx"));
      using var br = new BinaryReader (ms);
      if (br.ReadInt32 () != 0x1A48534D || br.ReadInt32 () != 1) Fatal ();
      var nodes = new Mesh3.Node[br.ReadInt32 ()];
      var tris = new int[br.ReadInt32 ()]; var wires = new int[br.ReadInt32 ()];
      for (int i = 0; i < nodes.Length; i++) {
         float x = br.ReadSingle (), y = br.ReadSingle (), z = br.ReadSingle ();
         Half p = br.ReadHalf (), q = br.ReadHalf (), r = br.ReadHalf ();
         nodes[i] = new Mesh3.Node (new Point3f (x, y, z), new Vec3H (p, q, r));
      }
      for (int i = 0; i < tris.Length; i++) tris[i] = br.ReadInt32 ();
      for (int i = 0; i < wires.Length; i++) wires[i] = br.ReadInt32 ();
      return new ([.. nodes], [.. tris], [.. wires]);
   }

   NurbsCurve3 LoadNurbsCurve () {
      var pairId = RInt ();
      var (ctrl, weight) = LoadCtrlPts ();
      var knots = LoadKnots ();
      return new (pairId, [.. ctrl], [.. knots], [.. weight]);
   }

   E3NurbsSurface LoadNurbsSurface () {
      var (uid, uctl) = (RInt (), RInt ());
      var (ctrl, weight) = LoadCtrlPts ();
      var uknots = LoadKnots ();
      var vknots = LoadKnots ();
      var contours = LoadContours ();
      return new (uid, [.. ctrl], [.. weight], uctl, [.. uknots], [.. vknots], contours);
   }

   E3Plane LoadPlane () {
      var (uid, cs) = (RInt (), RCS ());
      return new (uid, LoadContours (), cs);
   }

   Polyline3 LoadPolyline () {
      var uid = RInt ();
      List<Point3> ctrl = [];
      while (!RDone ()) ctrl.Add (RPoint ());
      return new Polyline3 (uid, [.. ctrl]);
   }

   E3SpunSurface LoadSpunSurface () {
      var (uid, cs) = (RInt (), RCS ());
      if (RWord () != "GENERATRIX") Fatal ();
      Curve3 genetrix = LoadEdge ()!;
      return new E3SpunSurface (uid, LoadContours (), cs, genetrix);
   }

   E3SweptSurface LoadSweptSurface () {
      var (uid, sweep) = (RInt (), RVector ());
      if (RWord () != "GENERATRIX") Fatal ();
      Curve3 genetrix = LoadEdge ()!;      
      var (x, y) = Geo.GetXYFromZ (sweep);
      var cs = new CoordSystem (genetrix.Start, x, y);
      return new E3SweptSurface (uid, LoadContours (), cs, genetrix.Xformed (Matrix3.From (cs)));
   }

   E3Torus LoadTorus () {
      var (uid, rmajor, rminor, cs) = (RInt (), RDouble (), RDouble (), RCS ());
      return new E3Torus (uid, LoadContours (), cs, rmajor, rminor);
   }

   // Low level routines -------------------------------------------------------
   public void Dispose () => mZip.Dispose ();
   void Fatal (string s) => throw new Exception (s);
   void Fatal () => Fatal ($"Error on line {N}");
   CoordSystem RCS () => new (RPoint (), RVector (), RVector ());
   double RDouble () { var (a, b) = Slice (); return double.Parse (T.AsSpan (a, b - a)); }
   double RDouble (char ch) { var (a, b) = SliceTo (ch); return double.Parse (T.AsSpan (a, b - a)); }
   int RInt () { var (a, b) = Slice (); return int.Parse (T.AsSpan (a, b - a)); }
   void RMatch (char ch) { SkipSpace (); if (T[N++] != ch) Fatal (); }
   string RWord () { var (a, b) = Slice (); return T[a..b]; }
   bool RDone () { SkipSpace (); if (T[N] == '*') { N++; return true; } else return false; }
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
