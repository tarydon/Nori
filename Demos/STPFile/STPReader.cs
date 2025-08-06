using Nori;
using Nori.STEP;
namespace Nori;

public partial class STEPReader {
   public STEPReader (string file) => (S, mFile) = (File.ReadAllText (file), file);
   readonly string S, mFile;
   int N;

   public void Parse () {
      Console.WriteLine ($"Reading {mFile}");
      N = S.IndexOf ("DATA;") + 5; Assert (N > 10);
      // The following loop loads all the entities one by one
      ReadOnlySpan<char> endsec = "ENDSEC;";
      for (; ; ) {
         if (RTryMatch ('#')) REntity ();
         else if (MemoryExtensions.Equals (S.AsSpan (N, 7), endsec, StringComparison.Ordinal)) break;
         else Fatal ($"Unexpected end of file");
      }
      var manifold = D.OfType<Manifold> ().Single ();
      Check (manifold);
      Console.WriteLine ();
   }

   // Entity switch ------------------------------------------------------------
   void REntity () {
      Id = RInt (); RMatch ('=');
      if (RTryMatch ('(')) {
      } else {
         string kw = RName (); RMatch ('(');
         Entity? ent = kw switch {
            "ADVANCED_FACE" => RAdvancedFace (),
            "AXIS2_PLACEMENT_3D" => RCoordSys (),
            "CARTESIAN_POINT" => RCartesian (),
            "CIRCLE" => RCircle (),
            "CLOSED_SHELL" => RClosedShell (),
            "CYLINDRICAL_SURFACE" => RCylinder (),
            "DIRECTION" => RDirection (), 
            "EDGE_CURVE" => REdgeCurve (),
            "EDGE_LOOP" => REdgeLoop (),
            "FACE_BOUND" => RFaceBound (), 
            "FACE_OUTER_BOUND" => RFaceOuterBound (),
            "LINE" => RLine (),
            "MANIFOLD_SOLID_BREP" => RManifold (),
            "ORIENTED_EDGE" => ROriengedEdge (),
            "PLANE" => RPlane (),
            "VECTOR" => RVector (),
            "VERTEX_POINT" => RVertexPoint (),
            _ => null,
         };
         if (ent == null) {
            Unread[Id] = kw;
         } else {
            while (D.Count <= Id) D.Add (null);
            D[Id] = ent;
         }
      }
      RSkip (';');
   }
   List<Entity?> D = [];
   Dictionary<int, string> Unread = [];
   int Id;

   // Low level read rountines -------------------------------------------------
   // Reads a 'bool' of the form .T. or .F. (after skipping past a leading comma)
   bool RBool () {
      RMatch (','); RMatch ('.');
      char ch = S[N++]; bool value = false;
      if (ch == 'T') value = true; else if (ch != 'F') Fatal ("Incorrect bool");
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
   Cartesian RCartesian () { RString (); return new (RPoint3 ()); }
   ClosedShell RClosedShell () { RString (); return new (RRefs ()); }
   Circle RCircle () { RString (); return new (RRef (), RDouble ()); }
   CoordSys RCoordSys () { RString (); return new (RRef (), RRef (), RRef ()); }
   Cylinder RCylinder () { RString (); return new (RRef (), RDouble ()); }
   Direction RDirection () { RString (); return new (RVector3 ()); }
   EdgeCurve REdgeCurve () { RString (); return new (RRef (), RRef (), RRef (), RBool ()); }
   EdgeLoop REdgeLoop () { RString (); return new (RRefs ()); }
   FaceOuterBound RFaceOuterBound () { RString (); return new (RRef (), RBool ()); }
   FaceBound RFaceBound () { RString (); return new (RRef (), RBool ()); }
   Line RLine () { RString (); return new (RRef (), RRef ()); }
   Manifold RManifold () { RString (); return new (RRef ()); }
   OrientedEdge ROriengedEdge () { RString (); RMatch (",*,*"); return new (RRef (), RBool ()); }
   Plane RPlane () { RString (); return new (RRef ()); }
   Vector RVector () { RString (); return new (RRef (), RDouble ()); }
   VertexPoint RVertexPoint () { RString (); return new (RRef ()); }

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