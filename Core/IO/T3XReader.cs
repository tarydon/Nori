// ────── ╔╗
// ╔═╦╦═╦╦╬╣ T3XReader.cs
// ║║║║╬║╔╣║ Code to load a Model3 from a T3X file
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO.Compression;
namespace Nori;

#region class T3XReader --------------------------------------------------------
/// <summary>Reader to load Model3 from T3X files (exported from Flux typically)</summary>
/// A T3X file is now sued to transfer 3D files from Flux to Nori, but is a general
/// format that could be written from other applications as well. A T3X file is 
/// basically a ZIP file. The ZIP file must contain one stream inside called "Data"
/// that contains a text representation of all the entities in the file. 
/// In addition, the T3X file can also contain pre-tesselated meshes for these
/// entities - these are stored in binary mesh format (meshx) streams with names
/// like 1.meshx, 2.meshx, where the integer is the Id of the entity.
/// 
/// For now, the T3XWriter in Flux is the only source of T3X files. Until there
/// are some other writers, both writer and reader will be maintained in lock-step
/// so only the 'latest' version of the T3X file will be supported by this reader.
/// 
/// Each open-ended list in a T3X file (like the top level list of entities, the list
/// of control points in a Nurb etc) are terminated with a line that contains only an
/// asterisk * in it (other than whitespace characters). 
public class T3XReader : IDisposable {
   // Constructors -------------------------------------------------------------
   /// <summary>Initialize a T3XReader, given the name of a T3X file</summary>
   public T3XReader (string file) {
      mZip = new (File.OpenRead (file), ZipArchiveMode.Read, false);
      T = mZip.ReadAllText ("Data");
   }

   // Methods ------------------------------------------------------------------
   /// <summary>Constructs the Model3 and returns it</summary>
   public Model3 Load () {
      if (mModel.Ents.Count > 0) return mModel;
      // Check this is a T3X file and the version
      if (RWord () != "T3X" || RInt () != 3) Fatal ("Not a T3X file");
      for (; ; ) {
         // Each entity starts with the type name, and the list below 
         // shows all the entity types that are supported
         string type = RWord ();
         E3Surface? ent = type switch {
            "CONE" => LoadCone (),
            "CYLINDER" => LoadCylinder (),
            "NURBSSURFACE" => LoadNurbsSurface (),
            "PLANE" => LoadPlane (),
            "RULEDSURFACE" => LoadRuledSurface (),
            "SPUNSURFACE" => LoadSpunSurface (),
            "SPHERE" => LoadSphere (),
            "SWEPTSURFACE" => LoadSweptSurface (),
            "TORUS" => LoadTorus (),
            "*" => null,   // This is the delimiter that terminates the file
            _ => throw new BadCaseException (type)
         };
         if (ent == null) break;
         // It's possible the mesh for this entity might also be stored in the
         // file, so attempt to load it and attach it to the entity
         ent.Mesh = LoadMesh (ent.Id);
         mModel.Ents.Add (ent);
      }
      mZip.Dispose (); 
      return mModel;
   }

   // Implementation -----------------------------------------------------------
   // Loads an Arc3 from an "ARC" entity (subtype of Curve3).
   // The schema is: 
   //   ID  Radius  AngSpan  CoordSys
   // The arc is defined in the world coordinate system and lofted up into position
   // using the CoordSystem. The canonical arc is centered on the origin, lies in the
   // XY plane and winds CCW starting with a point on the X axis (Radius,0,0). 
   // For all Curve3 objects, the ID is basically the PairID (or coedge ID), and there
   // are at most two Curve3 having the same PairID - these two are the touching edges from
   // two surfaces that are adjacent to each other, and are the way connectivity information
   // is maintained. 
   // A PairID of 0 means this is a free edge, unconnected to any other surface (an outer
   // edge or a hole edge of a non-manifold thin model, for example). A non-zero PairID that
   // is not matched by another Curve3 carrying the same one in the model is also a free edge. 
   Arc3 LoadArc () {
      int id = RInt ();
      double rad = RDouble (), span = RDouble ();
      return new Arc3 (id, RCS (), rad, span);
   }

   // Loads a set of control points, along with weights (this could be part of
   // a NurbsCurve or a NurbsSurface. Each line contains:
   //   Point3  Weight
   // The list is terminated with a line containing an asterisk *. 
   // Even if all the weights are 1.0, they are present and are never omitted.
   (List<Point3>, List<double>) LoadCtrlPts () {
      List<Point3> ctrl = [];
      List<double> weight = [];
      while (!RDone ()) { ctrl.Add (RPoint ()); weight.Add (RDouble ()); }
      return (ctrl, weight);
   }

   // Loads an E3Cone surface (subtype of Surface3) from a "CONE" entry
   // The schema is:
   //   ID  HalfAngle  CoordSys  Trims
   // The cone tip is at the origin and it expands upwards along the +Z axis, with the
   // specified Half-Angle at the tip.
   // As with all surfaces, the surface finishes with a list of trimming curves. Since
   // Cones are cyclic surfaces, it is not guaranteed that Trims[0] is an 'outer' trimming
   // curve and the rest are inner (there may not even be a well defined 'outer' curve, 
   // for example when there are two trimming circles defining a band running the whole
   // way around the cone).
   E3Cone LoadCone () {
      var (id, hangle, cs) = (RInt (), RDouble (), RCS ());
      return new E3Cone (id, LoadContours (), cs, hangle);
   }

   // Loads an E3Cylinder surface (subtype of Surface3) from a "CYLINDER" entry
   // The schema is:
   //   ID  Radius  CoordSys  Trims
   // The cylinder is defined as centered at the origin, with axis aligned to +Z. 
   // Trimming curves follow the same rules as for the Cone (no guarantee that Trims[0]
   // is the outer trim)
   E3Cylinder LoadCylinder () {
      var (uid, rad, cs) = (RInt (), RDouble (), RCS ());
      return new E3Cylinder (uid, LoadContours (), cs, rad);
   }

   // Loads a list of contours (these are basically trimming curves in XYZ space
   // for any of the surfaces). The complete list of contours is terminated by a *, 
   // and the list of edges within each contour is also terminated by a *.
   ImmutableArray<Contour3> LoadContours () {
      List<Curve3> edges = [];
      List<Contour3> contours = [];
      for (; ;) {
         string wor = RWord (); if (wor == "*") break;
         for (; ; ) {
            Curve3? edge = LoadEdge ();
            if (edge == null) break;      // Null is returned when we see a * (marking end of contour)
            edges.Add (edge);
         }
         contours.Add (new ([.. edges]));
         edges.Clear ();
      }
      return [..contours];
   }

   // Loads one of the Curve3 (typically, this is part of a list of Curve3 making up a Contour),
   // but Curve3 also appear in other places, such as the Generatrix for a SpunSurface
   // or SweptSurface
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

   // Loads an Ellipse entity. 
   // The schema is:
   //   ID  RadiusX  RadiusY  StartAng  EndAng  CoordSys
   // The ellipse is always defined with center at the origin and axes aligned to +X and +Y,
   // and then lofted up into its final location. The ellipse winds CCW from StartAng to EndAng.
   Ellipse3 LoadEllipse () {
      var (id, rx, ry, a0, a1) = (RInt (), RDouble (), RDouble (), RDouble (), RDouble ());
      return new Ellipse3 (id, RCS (), rx, ry, a0, a1);
   }

   // Loads a Knot Vector (just a list of doubles)
   // The storage of a knot vector in the T3X file takes into account that knots are often
   // repeated. So the schema is:
   //   Knot1  Repeat1
   //   Knot2  Repeat2
   //   ...
   //   *
   // So each knot is followed by a repeat count that specifies how many times that knot should
   // be repeated in the knot vector. As with all open lists this is terminated with an asterisk *.
   List<double> LoadKnots () {
      List<double> knot = [];
      while (!RDone ()) {
         double k = RDouble (); int rep = RInt ();
         for (int i = 0; i < rep; i++) knot.Add (k);
      }
      return knot;
   }

   // Loads a Line3 (subtype of Curve3)
   // The schema is
   //   ID  StartPoint  EndPoint
   Line3 LoadLine () 
      => new (RInt (), RPoint (), RPoint ());

   // Loads a mesh, given an entity ID
   // Meshes are stored in separate streams in the ZIP file, and the stream name is derived from
   // the ID. Each stream itself is binary encoded and has the following schema:
   //   0x1A48534D  1                 <- signature, version number (int)
   //   CNode  CTri  CWire            <- count of nodes, triangles and wires (all int)
   //   X Y Z P Q R                   <- 3 floats & 3 half making the pos & normal of a Node
   //   ...                           <- repeated CNode times
   //   A B C ...                     <- CTri integers making up the triangles
   //   A B C ...                     <- CWire integers making up the wires
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

   // Loads a 3D Nurbs curve (subtype of Curve3)
   // The schema is
   //   ID                          <- PairId
   //     (X Y Z) W                 <- control point + weight
   //     (X Y Z) W                 
   //     ...
   //     *                         <- end of control points
   //     K1 R1                     <- knot value, repeat count
   //     K2 R2
   //     ...
   //     *                         <- end of knot vector  
   // The set of all control points is loaded in using LoadCtrlPts, and the knot
   // vector is loaded in using LoadKnots (this is because these sub-structures are 
   // used for NurbsSurfaces as well). Note that we always store this as a rational NURBs curve,
   // but the weights may all be equal to 1, making it non-rational (the NurbsCurve entity handles
   // this optimization internally)
   NurbsCurve3 LoadNurbsCurve () {
      var pairId = RInt ();
      var (ctrl, weight) = LoadCtrlPts ();
      var knots = LoadKnots ();
      return new (pairId, [.. ctrl], [.. knots], [.. weight]);
   }

   // Loads a 3D Nurbs Surface (subtype of Surface3)
   // The schema is:
   //   ID  UCtl                    <- PairId, number of U control points (column count)
   //   { ControlPoints }           <- N control points, and VCtl can be computed as N / UCtl
   //   { U-Knots }                 <- Knot vector for U direction
   //   { V-Knots }                 <- Knot vector for V direction
   //   Trims                       <- Trimming curves
   // Note that the ControlPoints are stored as a single list, but effectively make up a 2D
   // array (UCtl is the number of colums in that array, and the number of rows is implicitly
   // N / UCtl). The knot vectors are stored in the same format as defined in the LoadNurbsCurve
   // above. Note that we always store this as a rational NURBs surface, but the weights on all
   // the control points may be equal to 1, making it non-rational (the NurbsSurface entity handles
   // this optimization internally)
   E3NurbsSurface LoadNurbsSurface () {
      var (uid, uctl) = (RInt (), RInt ());
      var (ctrl, weight) = LoadCtrlPts ();
      var uknots = LoadKnots ();
      var vknots = LoadKnots ();
      var contours = LoadContours ();
      return new (uid, [.. ctrl], [.. weight], uctl, [.. uknots], [.. vknots], contours);
   }

   // Loads a Plane (subtype of Surface3)
   // The schema is:
   //   ID  CoordSys  Trims
   // The plane is canonically defined as lying the XY plane, and then lofted up into
   // final position using a CoordSys. 
   E3Plane LoadPlane () {
      var (uid, cs) = (RInt (), RCS ());
      return new (uid, LoadContours (), cs);
   }

   // Loads a Polyline3 (subtype of Curve3) - a piecewise linear curve
   // The schema is 
   //   ID
   //   Point1
   //   Point2
   //   ...
   //   *
   Polyline3 LoadPolyline () {
      var uid = RInt ();
      List<Point3> ctrl = [];
      while (!RDone ()) ctrl.Add (RPoint ());
      return new Polyline3 (uid, [.. ctrl]);
   }

   // Loads a RuledSurface (subtype of Surface3)
   // The scheme is 
   //    ID  Bottom  Top  Trims
   // The ruled surface is defined by drawing lines between equi-parametric points
   // on the bottom and top generator curves
   E3RuledSurface LoadRuledSurface () {
      var uid = RInt ();
      if (RWord () != "BOTTOM") Fatal ();
      Curve3 bottom = LoadEdge ()!;
      if (RWord () != "TOP") Fatal ();
      Curve3 top = LoadEdge ()!;
      return new E3RuledSurface (uid, LoadContours (), bottom, top); 
   }

   /// <summary>Loads an E3Sphere surface (subtype of Surface3) from a "SPHERE" entry</summary>
   /// The schema is:
   ///   ID  Radius CoordSys  Trims
   /// The sphere is canonically centered at the origin and lofted into final position
   /// and orientation with a CoordSys
   E3Sphere LoadSphere () {
      var (id, radius, cs) = (RInt (), RDouble (), RCS ());
      return new E3Sphere (id, LoadContours (), cs, radius);
   }

   // Loads a SpunSurface (subtype of Surface3) - basically a surface-of-revolution
   // The schema is
   //   ID  CoordSys  Curve3  Trims
   // The spun surface is defined canonically with a spin axis along Z, and the generatrix
   // curve lying on the XZ plane. Since the generatrix curve is loaded using LoadEdge, it 
   // is polymorphic and could be any Curve3 type. (The PairID of this Curve3 is always set to
   // 0, since it is not a boundary curve in the BRep). 
   E3SpunSurface LoadSpunSurface () {
      var (uid, cs) = (RInt (), RCS ());
      if (RWord () != "GENERATRIX") Fatal ();
      Curve3 genetrix = LoadEdge ()!;
      return new E3SpunSurface (uid, LoadContours (), cs, genetrix);
   }

   // Loads a SweptSurface (subtype of Surface)
   // The schema is
   //   ID  CoordSys  Curve3  Trims
   // The swept surface is defined canonically with a sweep vector along +Y and the 
   // generatrix curve lying in the XZ plane. 
   E3SweptSurface LoadSweptSurface () {
      var (uid, sweep) = (RInt (), RVector ());
      if (RWord () != "GENERATRIX") Fatal ();
      Curve3 genetrix = LoadEdge ()!;      
      var (x, y) = Geo.GetXYFromZ (sweep);
      var cs = new CoordSystem (genetrix.Start, x, y);
      return new E3SweptSurface (uid, LoadContours (), cs, genetrix * Matrix3.From (cs));
   }

   // Loads a Torus (subtype of Surface)
   // The schema is
   //   ID  CoordSys  RMajor  RMinor  Trims
   E3Torus LoadTorus () {
      var (uid, cs, rmajor, rminor) = (RInt (), RCS (), RDouble (), RDouble ());
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
#endregion
