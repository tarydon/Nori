// ────── ╔╗
// ╔═╦╦═╦╦╬╣ STPReader.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori.STEP;
namespace Nori;

public partial class STEPReader {
   public STEPReader (string file) => (S, mFile) = (File.ReadAllText (file), file);
   readonly string S, mFile;
   int N;

   public void Parse () {
      N = S.IndexOf ("DATA;", StringComparison.Ordinal) + 5; Assert (N > 10);
      // The following loop loads all the entities one by one
      ReadOnlySpan<char> endsec = "ENDSEC;";
      for (; ; ) {
         if (RTryMatch ('#')) REntity ();
         else if (S.AsSpan (N, 7).Equals (endsec, StringComparison.Ordinal)) break;
         else Fatal ("Unexpected end of file");
      }
      if (D.OfType<Manifold> ().FirstOrDefault () is { } m) Check (m);
      else if (D.OfType<ShellBasedSurfaceModel> ().FirstOrDefault () is { } sb) Check (sb);
      else Console.WriteLine ("No top level entity found");
   }

   // Entity switch ------------------------------------------------------------
   void REntity () {
      Id = RInt (); RMatch ('=');
      if (RTryMatch ('(')) {
         RComplex ();
      } else {
         string kw = RName (); RMatch ('(');
         Entity? ent = kw switch {
            "ADVANCED_FACE" => RAdvancedFace (),
            "ADVANCED_BREP_SHAPE_REPRESENTATION" => RAdvancedBRepShapeRepr (),
            "AXIS2_PLACEMENT_3D" => RCoordSys (),
            "AXIS2_PLACEMENT_2D" => RCoordSys2 (),
            "B_SPLINE_CURVE_WITH_KNOTS" => RBSplineCurveWithKnots (),
            "B_SPLINE_SURFACE_WITH_KNOTS" => RBSplineSurfaceWithKnots (),
            "CARTESIAN_POINT" => RCartesian (),
            "CIRCLE" => RCircle (),
            "CLOSED_SHELL" or "OPEN_SHELL" => RShell (),
            "COMPOSITE_CURVE" => RCompositeCurve (),
            "CONICAL_SURFACE" => RConicalSurface (),
            "CYLINDRICAL_SURFACE" => RCylinder (),
            "DEFINITIONAL_REPRESENTATION" => RDefinitionalRepr (),
            "DIRECTION" => RDirection (),
            "EDGE_CURVE" => REdgeCurve (),
            "EDGE_LOOP" => REdgeLoop (),
            "ELLIPSE" => REllipse (),
            "FACE_BOUND" => RFaceBound (),
            "FACE_OUTER_BOUND" => RFaceOuterBound (),
            "ITEM_DEFINED_TRANSFORMATION" => RItemDefinedXfm (),
            "LINE" => RLine (),
            "MANIFOLD_SOLID_BREP" => RManifold (),
            "MANIFOLD_SURFACE_SHAPE_REPRESENTATION" => RManifoldSurfaceShapeRepr (),
            "ORIENTED_EDGE" => ROriengedEdge (),
            "PCURVE" => RPCurve (),
            "PLANE" => RPlane (),
            "POLYLINE" => RPolyline (),
            "SHELL_BASED_SURFACE_MODEL" => RShellBasedSurfaceModel (),
            "SPHERICAL_SURFACE" => RSphere (),
            "SURFACE_CURVE" => RSurfaceCurve (),
            "SURFACE_OF_LINEAR_EXTRUSION" => RExtrudedSurface (),
            "SHAPE_REPRESENTATION" => RShapeRepr (),
            "SHAPE_REPRESENTATION_RELATIONSHIP" => RRepRelationship (),
            "TOROIDAL_SURFACE" => RToroid (),
            "VECTOR" => RVector (),
            "VERTEX_POINT" => RVertexPoint (),
            _ => null
         };
         if (ent == null) {
            if (mUnsupported.Add (kw) && !Ignore.Contains (kw))
               throw new Exception ($"Unsupported: {kw}");
            Unread[Id] = kw;
         } else {
            while (D.Count <= Id) D.Add (null);
            ent.Id = Id; D[Id] = ent;
         }
      }
      RSkip (';');
   }
   List<Entity?> D = [];
   HashSet<string> mUnsupported = [];
   Dictionary<int, string> Unread = [];
   int Id;

   static HashSet<string> Ignore => sIgnore ??= [.. Lib.ReadLines ("nori:Core/STEPIgnore.txt")];
   static HashSet<string>? sIgnore;

   // Low level read rountines -------------------------------------------------
   // Reads a 'bool' of the form .T. or .F. (after skipping past a leading comma)
   bool RBool () {
      RMatch (','); RMatch ('.');
      char ch = S[N++]; bool value = true;
      if (ch is 'F' or 'U') value = false; else if (ch != 'T') Fatal ("Incorrect bool");
      RMatch ('.');
      return value;
   }

   double RDouble () {
      RTryMatch (','); RSpace ();
      int start = N;
      while (sDouble.Contains (S[N])) N++;
      if (!double.TryParse (S.AsSpan (start, N - start), out double d)) Fatal ("Invalid double");
      return d;
   }
   const string sDouble = "0123456789eE.-+";

   // Reads an integer from the current position
   int RInt () {
      RTryMatch (','); RSpace ();
      int start = N;
      while (char.IsAsciiDigit (S[N])) N++;
      return int.Parse (S.AsSpan (start, N - start));
   }

   // Reads an array of references delimited by ()
   int[] RInts () {
      mInts.Clear ();
      RTryMatch (','); RMatch ('(');
      for (; ; ) {
         if (RTryMatch (')')) break;
         mInts.Add (RInt ());
      }
      return [.. mInts];
   }

   // Matches a particular character (after possibly skipping whitespace)
   // If the given character is not found, this throws an exception
   void RMatch (char ch) {
      RSpace ();
      if (S[N++] != ch) Fatal ($"Expecting {ch}");
   }
   void RMatch (string s) {
      RSpace ();
      foreach (var ch in s) if (S[N++] != ch) Fatal ($"Expecting {s}");
   }

   // Reads an identifier from the current position
   string RName () {
      RSpace ();
      int start = N;
      for (; ; ) {
         char ch = S[N];
         if (char.IsAsciiLetterOrDigit (ch) || ch == '_') N++;
         else return S[start..N];
      }
   }

   // Reads a Point3 in the form "(12.5, 13.5, 14.5)", after skipping past a leading comma
   Point3 RPoint3 () {
      RMatch (','); RMatch ('(');
      double x = RDouble (); RMatch (',');
      double y = RDouble (), z = 0;
      if (RTryMatch (',')) z = RDouble ();
      RMatch (')');
      return new (x, y, z);
   }

   // Reads an array of double delimited by ()
   double[] RDoubles () {
      mDoubles.Clear ();
      RTryMatch (','); RMatch ('(');
      for (; ; ) {
         if (RTryMatch (')')) break;
         mDoubles.Add (RDouble ());
      }
      return [.. mDoubles];
   }
   List<double> mDoubles = [];

   // Reads a 'reference' (an integer prefixed with a hash), after skipping past a leading comma
   int RRef () {
      RTryMatch (',');
      if (RTryMatch ('$')) return 0;
      RMatch ('#'); return RInt ();
   }

   // Reads an array of references delimited by ()
   int[] RRefs () {
      mInts.Clear ();
      RTryMatch (','); RMatch ('(');
      for (; ; ) {
         if (RTryMatch (')')) break;
         mInts.Add (RRef ());
      }
      return [.. mInts];
   }
   List<int> mInts = [];

   // Reads a two-dimensionsl array of references
   int[][] RRefsList () {
      mRefsList.Clear ();
      RTryMatch (','); RMatch ('(');
      for (; ; ) {
         if (RTryMatch (')')) break;
         mRefsList.Add (RRefs ());
      }
      return [.. mRefsList];
   }
   List<int[]> mRefsList = [];

   // Search forward until the given character is found (this steps past any strings.
   // The given character is consumed, and N points to the next character after that
   void RSkip (char ch) {
      bool inQuote = false;
      for (; ; ) {
         char c = S[N++];
         if (c == ch && !inQuote) return;
         if (c == '\'') inQuote = !inQuote;
      }
   }

   // Read a string
   void RString () {
      RTryMatch (',');
      if (RTryMatch ('$')) return;
      RMatch ('\'');
      while (S[N++] != '\'') { }
   }

   string REnum () {
      RTryMatch (','); RMatch ('.');
      int start = N - 1;
      while (S[N++] != '.') { }
      return S[start..N];
   }

   // Skip past whitespace
   void RSpace () { while (char.IsWhiteSpace (S[N])) N++; }

   // Tries to match a given character, and if any other character is found, returns false
   bool RTryMatch (char ch) {
      RSpace ();
      if (S[N] == ch) { N++; return true; }
      return false;
   }

   Vector3 RVector3 () { var pt = RPoint3 (); return new (pt.X, pt.Y, pt.Z); }

   // Entity readers -----------------------------------------------------------
   AdvancedFace RAdvancedFace () { RString (); return new (RRefs (), RRef (), RBool ()); }
   AdvancedBRepShapeRepr RAdvancedBRepShapeRepr () { RString (); return new (RRefs (), RRef ()); }
   BSplineCurveWithKnots RBSplineCurveWithKnots () { RString (); return new (RInt(), RRefs(), REnum (), RBool(), RBool(), RInts(), RDoubles(), REnum ()); }
   BSplineSurfaceWithKnots RBSplineSurfaceWithKnots () { RString (); return new BSplineSurfaceWithKnots (RInt (), RInt (), RRefsList (), REnum (), RBool (), RBool (), RBool (), RInts (), RInts (), RDoubles (), RDoubles (), REnum ()); }
   CompositeCurve RCompositeCurve () { RString (); return new (RRefs (), RBool ()); }
   Cartesian RCartesian () { RString (); return new (RPoint3 ()); }
   Circle RCircle () { RString (); return new (RRef (), RDouble ()); }
   Cone RConicalSurface () { RString (); return new Cone (RRef (), RDouble (), RDouble ()); }
   CoordSys RCoordSys () { RString (); return new (RRef (), RRef (), RRef ()); }
   CoordSys2 RCoordSys2 () { RString (); return new (RRef (), RRef ()); }
   Cylinder RCylinder () { RString (); return new (RRef (), RDouble ()); }
   DefinitionalRepr RDefinitionalRepr () { RString (); return new (RRefs (), RRef ()); }
   Direction RDirection () { RString (); return new (RVector3 ()); }
   EdgeCurve REdgeCurve () { RString (); return new (RRef (), RRef (), RRef (), RBool ()); }
   EdgeLoop REdgeLoop () { RString (); return new (RRefs ()); }
   Ellipse REllipse () { RString (); return new (RRef (), RDouble (), RDouble ()); }
   ExtrudedSurface RExtrudedSurface () { RString (); return new (RRef(), RRef()); }
   FaceOuterBound RFaceOuterBound () { RString (); return new (RRef (), RBool ()); }
   FaceBound RFaceBound () { RString (); return new (RRef (), RBool ()); }
   ItemDefinedXfm RItemDefinedXfm () { RString (); RString (); return new ItemDefinedXfm (RRef (), RRef ()); }
   Line RLine () { RString (); return new (RRef (), RRef ()); }
   Manifold RManifold () { RString (); return new (RRef ()); }
   ManifoldSurfaceShapeRepr RManifoldSurfaceShapeRepr () { RString (); return new (RRefs (), RRef ()); }
   OrientedEdge ROriengedEdge () { RString (); RMatch (','); RMatch ('*'); RMatch (','); RMatch ('*'); return new (RRef (), RBool ()); }
   Plane RPlane () { RString (); return new (RRef ()); }
   PCurve RPCurve () { RString (); return new (RRef (), RRef ()); }
   Polyline RPolyline () { RString (); return new (RRefs ()); }
   ShapeRepRelationship RRepRelationship () { RString (); RString (); return new (RRef (), RRef ()); }
   ShapeRepr RShapeRepr () { RString (); return new (RRefs (), RRef ()); }
   Shell RShell () { RString (); return new (RRefs ()); }
   ShellBasedSurfaceModel RShellBasedSurfaceModel () { RString (); return new (RRefs ()); }
   Sphere RSphere () { RString (); return new (RRef (), RDouble ()); }
   SurfaceCurve RSurfaceCurve () { RString (); return new (RRef (), RRefs (), REnum ()); }
   Toroid RToroid () { RString (); return new Toroid (RRef (), RDouble (), RDouble ()); }
   Vector RVector () { RString (); return new (RRef (), RDouble ()); }
   VertexPoint RVertexPoint () { RString (); return new (RRef ()); }

   void RComplex () {
      int n = N - 1;
      while (S[N++] != ';') { }
      var sub = S[n..--N];
      if (sub.Contains ("SOLID_ANGLE_UNIT") || sub.Contains ("MASS_UNIT")) return;
      if (sub.Contains ("PLANE_ANGLE_UNIT") || sub.Contains ("LENGTH_UNIT")) return;
      if (sub.Contains ("GEOMETRIC_REPRESENTATION_CONTEXT")) return;
      if (sub.Contains ("REPRESENTATION_RELATIONSHIP_WITH_TRANSFORM")) return;
      //      if (sub.Contains ("BOUNDED_SURFACE") || sub.Contains ("BOUNDED_CURVE")) return;
      if (sub.Contains ("RATIONAL_B_SPLINE_SURFACE") || sub.Contains ("RATIONAL_B_SPLINE_CURVE")) return;
      if (sub.Contains ("ANNOTATION_CURVE_OCCURRENCE") || sub.Contains ("ANNOTATION_OCCURRENCE")) return;
      sub = sub.Replace ('\n', ' ').Replace ('\r', ' ');
      Console.WriteLine ($"#{Id}");
      Console.WriteLine (sub);
      Console.ReadLine ();
   }

   // Helpers ------------------------------------------------------------------
   partial void Assert (bool condition);
   partial void Assert (bool condition) {
      if (!condition) throw new Exception ("Condition failed");
   }

   void Fatal (string s) {
      s = $"File = {mFile}, ID = {Id}: {s}";
      throw new Exception (s);
   }
}
