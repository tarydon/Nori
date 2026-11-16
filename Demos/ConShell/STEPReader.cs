using System.Diagnostics.CodeAnalysis;
using System.Text;
using Nori.STEP;
namespace Nori;
using static Nori.STEP.EKind;

class STEPException : Exception {
   public STEPException (int n) : base (sMessages.GetValueOrDefault (n, "Unknown Error")) { }

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

   public void Load () {
      Parse ();
      Check (CheckIt (), 1);
      Build ();
   }

   void Check (bool condition, int error) {
      if (!condition) throw new STEPException (error);
   }

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
      mData[id] = (COMPOUND, mR.Pos);
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
      if (mAssyUsage.Count > 0) return "Assembly";
      return "Unknown";
   }

   // Reads a standard entity of the given type
   void REntity (int id, EKind kind) {
      if (kind > _IGNORE) return;
      if (mR.TryMatch ('$')) mR.Match (',');
      else if (mR.TryMatch ('\'')) mR.SkipTo ('\'').Match (',');
      int slot;
      switch (kind) {
         case ADVANCED_FACE or FACE_SURFACE: slot = Add (mFace, new Face (RRefs (), RRef (), RBool ())); break;
         case ADVANCED_BREP_SHAPE_REPRESENTATION or SHAPE_REPRESENTATION or MANIFOLD_SURFACE_SHAPE_REPRESENTATION
            or DEFINITIONAL_REPRESENTATION:
            slot = Add (mShapeRepr, new (id, kind, RRefs (), RRef ())); 
            break;
         case AXIS2_PLACEMENT_3D: slot = Add (mAxis, new (RRef (), RRef (), RRef ())); break;
         case BREP_WITH_VOIDS: slot = Add (mBrepVoids, new (RRef (), RRefs ())); break;
         case B_SPLINE_CURVE_WITH_KNOTS:
            slot = Add (mSplineCurve, new (RInt (), RRefs (), REnum (), RBool (), RBool (),
               RInts (), RDoubles (), REnum ()));
            break;
         case B_SPLINE_SURFACE_WITH_KNOTS:
            slot = Add (mSplineSurface, new BSplineSurface (RInt (), RInt (), RRefsList (), REnum (),
               RBool (), RBool (), RBool (), RInts (), RInts (), RDoubles (), RDoubles (), REnum ()));
            break;
         case CARTESIAN_POINT: slot = Add (mPoint, RPoint3 ()); break;
         case EDGE_LOOP or FILL_AREA_STYLE or PRESENTATION_STYLE_ASSIGNMENT or SHELL_BASED_SURFACE_MODEL 
            or SURFACE_SIDE_STYLE or POLYLINE:
            slot = Add (mInts2, RRefs ()); 
            break;
         case CLOSED_SHELL or OPEN_SHELL: slot = Add (mInts, new Ints (id, kind, RRefs ()));  break;
         case CIRCLE: slot = Add (mCircle, new (RRef (), RDouble ())); break;
         case COLOUR_RGB: slot = Add (mColor, new (RDouble (), RDouble (), RDouble ())); break;
         case CONICAL_SURFACE: slot = Add (mCone, new (RRef (), RDouble (), RDouble ())); break;
         case CONTEXT_DEPENDENT_SHAPE_REPRESENTATION or MAPPED_ITEM or SHAPE_DEFINITION_REPRESENTATION
            or SURFACE_OF_LINEAR_EXTRUSION or REPRESENTATION_MAP or PCURVE or AXIS2_PLACEMENT_2D
            or AXIS1_PLACEMENT or SURFACE_OF_REVOLUTION:
            slot = Add (mTwoRefs, new (RRef (), RRef ())); 
            break;
         case CYLINDRICAL_SURFACE: slot = Add (mCylinder, new (RRef (), RDouble ())); break;
         case DEGENERATE_TOROIDAL_SURFACE: slot = Add (mTorus, new (RRef (), RDouble (), RDouble (), RBool ())); break;
         case DIRECTION: slot = Add (mDirection, RVector3 ()); break;
         case ELLIPSE: slot = Add (mEllipse, new (RRef (), RDouble (), RDouble ())); break;
         case EDGE_CURVE: slot = Add (mCurve, new Edge (RRef (), RRef (), RRef (), RBool ())); break;
         case FACE_BOUND: slot = Add (mFaceBound, new (RRef (), RBool (), false)); break;
         case FACE_OUTER_BOUND: slot = Add (mFaceBound, new (RRef (), RBool (), true)); break;
         case FILL_AREA_STYLE_COLOUR or PLANE or SURFACE_STYLE_FILL_AREA or VERTEX_POINT
            or VERTEX_LOOP:
            slot = Add (mInt, RRef ());
            break;
         case MANIFOLD_SOLID_BREP: slot = Add (mManifold, RRef ()); break;
         case ITEM_DEFINED_TRANSFORMATION: RString (); slot = Add (mTwoRefs, new (RRef (), RRef ())); break;
         case LINE: slot = Add (mLine, new (RRef (), RRef ())); break;
         case NEXT_ASSEMBLY_USAGE_OCCURRENCE:
            slot = Add (mAssyUsage, new (RString (), RString (), RRef (), RRef ()));
            break;
         case OFFSET_SURFACE: slot = Add (mOffsetSurf, new (RRef (), RDouble ())); break;
         case ORIENTED_EDGE:
            mR.Match ('*').Match (',').Match ('*').Match (',');
            slot = Add (mOEdge, new (RRef (), RBool ()));
            break;
         case ORIENTED_CLOSED_SHELL:
            mR.Match ('*').Match (',');
            slot = Add (mOShell, new (RRef (), RBool ()));
            break;
         case OVER_RIDING_STYLED_ITEM: slot = Add (mStyleOverride, new (RRefs (), RRef (), RRef ())); break;
         case PRODUCT: slot = Add (mProduct, new (RString (), RString (), RRefs ())); break;
         case PRODUCT_DEFINITION: RString (); slot = Add (mTwoRefs, new (RRef (), RRef ())); break;
         case PRODUCT_DEFINITION_SHAPE or PRODUCT_DEFINITION_FORMATION 
            or PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE:
            RString (); slot = Add (mInt, RRef ()); 
            break;
         case SHAPE_REPRESENTATION_RELATIONSHIP: RString (); slot = Add (mTwoRefs, new (RRef (), RRef ())); break;
         case SPHERICAL_SURFACE: slot = Add (mSphere, new (RRef (), RDouble ())); break;
         case SURFACE_CURVE or INTERSECTION_CURVE: 
            slot = Add (mSurfCurve, new SurfCurve (RRef (), RRefs (), REnum ())); 
            break;
         case SURFACE_STYLE_USAGE: slot = Add (mSurfStyleUsage, new (REnum (), RRef ())); break;
         case STYLED_ITEM: slot = Add (mStyledItem, new (RRefs (), RRef ())); break;
         case TOROIDAL_SURFACE: slot = Add (mTorus, new (RRef (), RDouble (), RDouble (), false)); break;
         case VECTOR: slot = Add (mVector, new (RRef (), RDouble ())); break;
         default: throw new BadCaseException ($"Unhandled entity: #{id} = {kind}");
      }
      mData[id] = (kind, slot);
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
               while (mData.Length <= id) Array.Resize (ref mData, mData.Length * 2);
               if (mR.Peek () == '(') { RCompound (id); } else { REntity (id, RType ()); }
            }
         } else {
            if (mR.TryMatch ("ENDSEC;"u8)) break;
         }
      }
   }

   // Data stores --------------------------------------------------------------
   List<AssyUsage> mAssyUsage = [];
   List<Axis3D> mAxis = [];
   List<BRepVoids> mBrepVoids = [];
   List<Circle> mCircle = [];
   List<Ellipse> mEllipse = [];
   List<ColorRGB> mColor = [];
   List<Cone> mCone = [];
   List<Cylinder> mCylinder = [];
   List<Edge> mCurve = [];
   List<Face> mFace = [];
   List<FaceBound> mFaceBound = [];
   List<int> mInt = [];
   List<Ints> mInts = [];
   List<int[]> mInts2 = [];
   List<Line> mLine = [];
   List<int> mManifold = [];
   List<TwoRef> mTwoRefs = [];
   List<BSplineCurve> mSplineCurve = [];
   List<BSplineSurface> mSplineSurface = [];
   List<OEdge> mOEdge = [];
   List<OShell> mOShell = [];
   List<OffsetSurf> mOffsetSurf = [];
   List<Point3> mPoint = [];
   List<Product> mProduct = [];
   List<ShapeRepr> mShapeRepr = [];
   List<Sphere> mSphere = [];
   List<StyledItem> mStyledItem = [];
   List<StyleOverride> mStyleOverride = [];
   List<SurfCurve> mSurfCurve = [];
   List<SurfStyleUsage> mSurfStyleUsage = [];
   List<Torus> mTorus = [];
   List<Vector> mVector = [];
   List<Vector3> mDirection = [];
   (EKind Kind, int Index)[] mData = new (EKind, int)[128];

   // Private data -------------------------------------------------------------
   // This converts keywords like LINE, CIRCLE, PRODUCT etc to EType enumerations
   static SymTable<EKind> sDict = new ();
   string mFile;
   byte[] mRaw;
   UTFReader mR;
   Model3 mModel = new ();
}
