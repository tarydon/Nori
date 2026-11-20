using System.Diagnostics.CodeAnalysis;
using System.Text;
using Nori.STEP;
namespace Nori;
using static Nori.STEP.EKind;

class STEPException : Exception {
   public STEPException (int n) : base (sMessages.GetValueOrDefault (n, "Unknown Error")) { }
   public STEPException (int n, params object[] args) : base (string.Format (sMessages.GetValueOrDefault (n, "Unknown error"), args)) { }

   static STEPException () {
      sMessages = new ();
      foreach (var line in Lib.ReadLines ("nori:Core/STEPError.txt")) {
         var w = line.Split ('|'); if (w.Length != 2) continue;
         int n = w[0].ToInt (); sMessages.Add (n, w[1].Trim ());
      }
   }
   static Dictionary<int, string> sMessages;

}

public partial class STEPReader {
   public STEPReader (string file) {
      mR = new (mRaw = File.ReadAllBytes (mFile = file));
      if (sDict.Count == 0) 
         for (var ed = _FIRST + 1; ed < _LAST; ed++) 
            sDict.Add (ed.ToString (), ed);
   }

   public Model3 Load () {
      Parse ();
      Build ();
      return mModel;
   }

   void Check (bool condition, int error) {
      if (!condition) throw new STEPException (error);
   }

   void OptCheck (bool condition, int error) => Check (condition, error);

   [DoesNotReturn]
   void Fail (int error) => throw new STEPException (error);

   // Read routines ------------------------------------------------------------
   // Reads a 'bool' of the form .T. or .F. (after skipping past a leading comma)
   bool RBool () {
      mR.TryMatch (','); 
      mR.Match ('.').Read (out byte b).Match ('.');
      return b == 'T';
   }

   // Reads a compound entity
   void RCompound (int id) {
      Data[id] = (COMPOUND, mR.Pos);
   }

   // Reads a double, skipping past a possible prefix comma
   double RDouble () { mR.TryMatch (','); mR.Read (out double f); return f; }

   // Reads a list of doubles of the form "(1.2,2.5,3.5,4.8)" prefixed by a
   // possible comma
   double[] RDoubles () {
      mTmpF.Clear ();
      mR.TryMatch (','); mR.Match ('(');
      for (; ; ) {
         if (mR.TryMatch (')')) break;
         mTmpF.Add (RDouble ());
      }
      return [.. mTmpF];
   }
   List<double> mTmpF = [];

   public string Classify () {
      if (AssyUsage.Count > 0) return "Assembly";
      return "Unknown";
   }

   // Reads a standard entity of the given type
   void REntity (int id, EKind kind) {
      if (kind > _IGNORE) return;
      if (mR.TryMatch ('$')) mR.Match (',');
      else if (mR.TryMatch ('\'')) mR.SkipTo ('\'').Match (',');
      int slot;
      switch (kind) {
         case ADVANCED_FACE or FACE_SURFACE: slot = Add (Face, new Face (RRefs (), RRef (), RBool ())); break;
         case ADVANCED_BREP_SHAPE_REPRESENTATION or SHAPE_REPRESENTATION or MANIFOLD_SURFACE_SHAPE_REPRESENTATION
            or DEFINITIONAL_REPRESENTATION:
            slot = Add (ShapeRepr, new (id, kind, RRefs (), RRef ())); 
            break;
         case AXIS2_PLACEMENT_3D: slot = Add (Axis3D, new (RRef (), RRef (), RRef ())); break;
         case BREP_WITH_VOIDS: slot = Add (BRepVoids, new (RRef (), RRefs ())); break;
         case B_SPLINE_CURVE_WITH_KNOTS:
            slot = Add (SplineCurve, new (RInt (), RRefs (), REnum (), RBool (), RBool (),
               RInts (), RDoubles (), REnum ()));
            break;
         case B_SPLINE_SURFACE_WITH_KNOTS:
            slot = Add (SplineSurface, new BSplineSurface (RInt (), RInt (), RRefsList (), REnum (),
               RBool (), RBool (), RBool (), RInts (), RInts (), RDoubles (), RDoubles (), REnum ()));
            break;
         case CARTESIAN_POINT: slot = Add (Point3, RPoint3 ()); break;
         case EDGE_LOOP or FILL_AREA_STYLE or PRESENTATION_STYLE_ASSIGNMENT or SHELL_BASED_SURFACE_MODEL 
            or SURFACE_SIDE_STYLE or POLYLINE:
            slot = Add (Ints2, RRefs ()); 
            break;
         case CLOSED_SHELL or OPEN_SHELL: slot = Add (Ints, new IntSet (id, kind, RRefs ()));  break;
         case CIRCLE: slot = Add (Circle, new (RRef (), RDouble ())); break;
         case COLOUR_RGB: slot = Add (ColorRGB, new (RDouble (), RDouble (), RDouble ())); break;
         case CONICAL_SURFACE: slot = Add (Cone, new (RRef (), RDouble (), RDouble ())); break;
         case CONTEXT_DEPENDENT_SHAPE_REPRESENTATION or MAPPED_ITEM or SHAPE_DEFINITION_REPRESENTATION
            or SURFACE_OF_LINEAR_EXTRUSION or REPRESENTATION_MAP or PCURVE or AXIS2_PLACEMENT_2D
            or AXIS1_PLACEMENT or SURFACE_OF_REVOLUTION:
            slot = Add (TwoRef, new (RRef (), RRef ())); 
            break;
         case CYLINDRICAL_SURFACE: slot = Add (Cylinder, new (RRef (), RDouble ())); break;
         case DEGENERATE_TOROIDAL_SURFACE: slot = Add (Torus, new (RRef (), RDouble (), RDouble (), RBool ())); break;
         case DIRECTION: slot = Add (Vector3, RVector3 ()); break;
         case ELLIPSE: slot = Add (Ellipse, new (RRef (), RDouble (), RDouble ())); break;
         case EDGE_CURVE: slot = Add (Edge, new Edge (RRef (), RRef (), RRef (), RBool ())); break;
         case FACE_BOUND: slot = Add (FaceBound, new (RRef (), RBool (), false)); break;
         case FACE_OUTER_BOUND: slot = Add (FaceBound, new (RRef (), RBool (), true)); break;
         case FILL_AREA_STYLE_COLOUR or PLANE or SURFACE_STYLE_FILL_AREA or VERTEX_POINT
            or VERTEX_LOOP:
            slot = Add (Int, RRef ());
            break;
         case MANIFOLD_SOLID_BREP: slot = Add (Manifold, RRef ()); break;
         case ITEM_DEFINED_TRANSFORMATION: RString (); slot = Add (TwoRef, new (RRef (), RRef ())); break;
         case LINE: slot = Add (Line, new (RRef (), RRef ())); break;
         case NEXT_ASSEMBLY_USAGE_OCCURRENCE:
            slot = Add (AssyUsage, new (RString (), RString (), RRef (), RRef ()));
            break;
         case OFFSET_SURFACE: slot = Add (OffsetSurf, new (RRef (), RDouble ())); break;
         case ORIENTED_EDGE:
            mR.Match ('*').Match (',').Match ('*').Match (',');
            slot = Add (OEdge, new (RRef (), RBool ()));
            break;
         case ORIENTED_CLOSED_SHELL:
            mR.Match ('*').Match (',');
            slot = Add (OShell, new (RRef (), RBool ()));
            break;
         case OVER_RIDING_STYLED_ITEM: slot = Add (StyleOverride, new (RRefs (), RRef (), RRef ())); break;
         case PRODUCT: slot = Add (Product, new (RString (), RString (), RRefs ())); break;
         case PRODUCT_DEFINITION: RString (); slot = Add (TwoRef, new (RRef (), RRef ())); break;
         case PRODUCT_DEFINITION_SHAPE or PRODUCT_DEFINITION_FORMATION 
            or PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE:
            RString (); slot = Add (Int, RRef ()); 
            break;
         case SHAPE_REPRESENTATION_RELATIONSHIP: RString (); slot = Add (TwoRef, new (RRef (), RRef ())); break;
         case SPHERICAL_SURFACE: slot = Add (Sphere, new (RRef (), RDouble ())); break;
         case SURFACE_CURVE or INTERSECTION_CURVE: 
            slot = Add (SurfCurve, new SurfCurve (RRef (), RRefs (), REnum ())); 
            break;
         case SURFACE_STYLE_USAGE: slot = Add (SurfStyleUsage, new (REnum (), RRef ())); break;
         case STYLED_ITEM: slot = Add (StyledItem, new (RRefs (), RRef ())); break;
         case TOROIDAL_SURFACE: slot = Add (Torus, new (RRef (), RDouble (), RDouble (), false)); break;
         case VECTOR: slot = Add (Vector, new (RRef (), RDouble ())); break;
         default: throw new BadCaseException ($"Unhandled entity: #{id} = {kind}");
      }
      Data[id] = (kind, slot);
   }

   // Returns an enumeration of the form ".UNSPECIFIED."
   string REnum () {
      mR.TryMatch (',');
      if (mR.TryMatch ('$')) return "";
      mR.Match ('.');
      int start = mR.Pos - 1;
      mR.SkipTo ('.');
      int end = mR.Pos;
      string s =  Encoding.ASCII.GetString (mRaw.AsSpan (start + 1, end - start - 2));
      return s;
   }

   // Reads an integer (prefixed by an optional comma)
   int RInt () { mR.TryMatch (','); mR.Read (out int n); return n; }

   // Reads a set of integers 
   int[] RInts () {
      mTmpN.Clear ();
      mR.TryMatch (','); mR.Match ('(');
      for (; ; ) {
         if (mR.TryMatch (')')) break;
         mTmpN.Add (RInt ());
      }
      return [.. mTmpN];
   }

   // Reads an array of integers of the form "(#1,#2,#3)"
   int[] RRefs () {
      mTmpN.Clear ();
      if (mR.TryMatch ('$')) return [];
      mR.TryMatch (','); mR.Match ('(');
      for (; ; ) {
         if (mR.TryMatch (')')) break;
         mTmpN.Add (RRef ());
      }
      return [.. mTmpN];
   }
   readonly List<int> mTmpN = [];

   // Reads a two-dimensional array of doubles
   double[][] RDoublesList () {
      mDoublesList.Clear ();
      mR.TryMatch (','); mR.Match ('(');
      for (; ; ) {
         if (mR.TryMatch (')')) break;
         mDoublesList.Add (RDoubles ());
      }
      return [.. mDoublesList];
   }
   readonly List<double[]> mDoublesList = [];

   // Reads a two-dimensionsl array of references
   int[][] RRefsList () {
      mRefsList.Clear ();
      mR.TryMatch (','); mR.Match ('(');
      for (; ; ) {
         if (mR.TryMatch (')')) break;
         mRefsList.Add (RRefs ());
      }
      return [.. mRefsList];
   }
   readonly List<int[]> mRefsList = [];

   // Reads in a Point3 in the form "(12,13.5,18.2)"
   Point3 RPoint3 () {
      double x, y, z = 0;
      mR.Match ('(').Read (out x).Match (',').Read (out y);
      if (mR.TryMatch (',')) mR.Read (out z);
      mR.Match (')');
      return new (x, y, z);
   }

   // Reads a 'reference' (an integer prefixed with a hash), after skipping past
   // an optional leading comma
   int RRef () {
      mR.TryMatch (',');
      if (mR.TryMatch ('$')) return 0;
      mR.Match ('#').Read (out int n);
      return n;
   }

   // Read a string delimited by single quotes (possible optional comma before)
   string RString () {
      mR.TryMatch (',');
      if (mR.TryMatch ('$')) return "";
      mR.Match ('\''); int start = mR.Pos;
      mR.SkipTo ('\''); int end = mR.Pos - 1;
      string s = Encoding.ASCII.GetString (mRaw.AsSpan (start, end - start));
      return s;
   }

   // Reads an EType enumeration from the file (this is accelerated by using
   // a SymTable)
   EKind RType () {
      var span = mR.TakeUntil ('(');
      EKind type = sDict.GetValueOrDefault (span);
      if (type == _NIL && span[^1] == ' ') 
         type = sDict.GetValueOrDefault (span[..^1]);
      if (type == _NIL) { 
         string s = $"Unknown: {Encoding.ASCII.GetString (span)}, line {mR.LineNo} of {mFile}";
         throw new Exception (s);
      }
      return type;
   }

   // Reads in a Vector3 in the form "(12,13.5,18.2)";
   Vector3 RVector3 () => (Vector3)RPoint3 ();

   // Helpers ------------------------------------------------------------------
   int Add<T> (List<T> list, T item) {
      int n = list.Count; list.Add (item); return n; 
   }

   // Root routine to parse all the entities in the file
   void Parse () {
      ReadOnlySpan<byte> seq = "DATA;"u8;
      if (!mR.Find (seq)) throw new Exception ("DATA section not found");
      int idMax = 0;
      for (; ; ) {
         mR.SkipToNextLine ();
         if (mR.TryMatch ('#')) {
            mR.Read (out int id);
            if (mR.SkipSpace ().TryMatch ('=')) {
               idMax = Math.Max (idMax, id);
               mR.SkipSpace ();
               while (Data.Length <= id) Array.Resize (ref Data, Data.Length * 2);
               if (mR.Peek () == '(') { RCompound (id); } else { REntity (id, RType ()); }
            }
         } else {
            if (mR.TryMatch ("ENDSEC;"u8)) break;
         }
      }
   }

   // Data stores --------------------------------------------------------------
   List<AssyUsage> AssyUsage = [];
   List<Axis3D> Axis3D = [];
   List<BRepVoids> BRepVoids = [];
   List<Circle> Circle = [];
   List<Ellipse> Ellipse = [];
   List<ColorRGB> ColorRGB = [];
   List<Cone> Cone = [];
   List<Cylinder> Cylinder = [];
   List<Edge> Edge = [];
   List<Face> Face = [];
   List<FaceBound> FaceBound = [];
   List<int> Int = [];
   List<IntSet> Ints = [];
   List<int[]> Ints2 = [];
   List<Line> Line = [];
   List<int> Manifold = [];
   List<TwoRef> TwoRef = [];
   List<BSplineCurve> SplineCurve = [];
   List<BSplineSurface> SplineSurface = [];
   List<OEdge> OEdge = [];
   List<OShell> OShell = [];
   List<OffsetSurf> OffsetSurf = [];
   List<Point3> Point3 = [];
   List<Product> Product = [];
   List<ShapeRepr> ShapeRepr = [];
   List<Sphere> Sphere = [];
   List<StyledItem> StyledItem = [];
   List<StyleOverride> StyleOverride = [];
   List<SurfCurve> SurfCurve = [];
   List<SurfStyleUsage> SurfStyleUsage = [];
   List<Torus> Torus = [];
   List<Vector> Vector = [];
   List<Vector3> Vector3 = [];
   (EKind Kind, int Index)[] Data = new (EKind, int)[128];

   // Private data -------------------------------------------------------------
   // This converts keywords like LINE, CIRCLE, PRODUCT etc to EType enumerations
   static SymTable<EKind> sDict = new ();
   string mFile;
   byte[] mRaw;
   UTFReader mR;
   Model3 mModel = new ();
}
