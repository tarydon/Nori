using System.Diagnostics.CodeAnalysis;
using System.Text;
using Nori.STEP;
using static Nori.STEPReader.EType;
namespace Nori;

public class STEPReader {
   public STEPReader (string file) {
      mR = new (mRaw = File.ReadAllBytes (mFile = file));
      if (sDict.Count == 0) 
         for (var ed = _FIRST + 1; ed < _LAST; ed++) 
            sDict.Add (ed.ToString (), ed);
   }

   public void Load () {
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
               if (mR.Peek () == '(') { RCompound (id); }
               else { REntity (id, RType ()); }
            }
         } else {
            if (mR.TryMatch ("ENDSEC;"u8)) break;
         }
      }
   }

   // Read routines ------------------------------------------------------------
   // Reads a 'bool' of the form .T. or .F. (after skipping past a leading comma)
   bool RBool () {
      mR.TryMatch (','); 
      mR.Match ('.').Read (out byte b).Match ('.');
      return b == 'T';
   }

   // Reads a compound entity
   void RCompound (int id) {
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

   // Reads a standard entity of the given type
   void REntity (int id, EType type) {
      if (type > _IGNORE) return;
      if (mR.TryMatch ('$')) mR.Match (',');
      else if (mR.TryMatch ('\'')) mR.SkipTo ('\'').Match (',');
      int slot;
      switch (type) {
         case ADVANCED_FACE or FACE_SURFACE: slot = Add (mFace, new Face (RRefs (), RRef (), RBool ())); break;
         case ADVANCED_BREP_SHAPE_REPRESENTATION or SHAPE_REPRESENTATION or MANIFOLD_SURFACE_SHAPE_REPRESENTATION
            or DEFINITIONAL_REPRESENTATION:
            slot = Add (mShapeRepr, new (RRefs (), RRef ())); 
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
         case CLOSED_SHELL or EDGE_LOOP or FILL_AREA_STYLE or OPEN_SHELL or PRESENTATION_STYLE_ASSIGNMENT
            or SHELL_BASED_SURFACE_MODEL or SURFACE_SIDE_STYLE or POLYLINE:
            slot = Add (mInts, RRefs ()); 
            break;
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
         case FILL_AREA_STYLE_COLOUR or MANIFOLD_SOLID_BREP or PLANE or SURFACE_STYLE_FILL_AREA or VERTEX_POINT
            or VERTEX_LOOP:
            slot = Add (mInt, RRef ()); 
            break;
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
         default: throw new BadCaseException ($"Unhandled entity: #{id} = {type}");
      }
      while (mData.Length <= id) Array.Resize (ref mData, mData.Length * 2);
      mData[id] = (type, slot);
   }

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
   List<int[]> mInts = [];
   List<Line> mLine = [];
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
   (EType, int)[] mData = new (EType, int)[128];

   internal enum EType {
      _NIL,
      _FIRST,
      DIRECTION, VECTOR, CARTESIAN_POINT, LINE, VERTEX_POINT, AXIS2_PLACEMENT_3D, PLANE,
      ORIENTED_EDGE, EDGE_LOOP, FACE_OUTER_BOUND, ADVANCED_FACE, CLOSED_SHELL, MANIFOLD_SOLID_BREP,
      EDGE_CURVE,
      CYLINDRICAL_SURFACE, CIRCLE, FACE_BOUND,
      B_SPLINE_SURFACE_WITH_KNOTS,
      B_SPLINE_CURVE_WITH_KNOTS,
      TOROIDAL_SURFACE, SURFACE_OF_LINEAR_EXTRUSION, ELLIPSE, OVER_RIDING_STYLED_ITEM,
      CONICAL_SURFACE, ITEM_DEFINED_TRANSFORMATION, NEXT_ASSEMBLY_USAGE_OCCURRENCE, SPHERICAL_SURFACE,
      OPEN_SHELL, SHELL_BASED_SURFACE_MODEL,
      MANIFOLD_SURFACE_SHAPE_REPRESENTATION, MAPPED_ITEM, REPRESENTATION_MAP,
      SURFACE_CURVE, FACE_SURFACE, 
      POLYLINE,
      PCURVE, 
      AXIS2_PLACEMENT_2D, VERTEX_LOOP,
      ORIENTED_CLOSED_SHELL, BREP_WITH_VOIDS, AXIS1_PLACEMENT, SURFACE_OF_REVOLUTION,
      INTERSECTION_CURVE, OFFSET_SURFACE, DEGENERATE_TOROIDAL_SURFACE,

      PRODUCT, PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE, CONTEXT_DEPENDENT_SHAPE_REPRESENTATION,

      ADVANCED_BREP_SHAPE_REPRESENTATION, PRODUCT_DEFINITION, PRODUCT_DEFINITION_SHAPE,
      SHAPE_DEFINITION_REPRESENTATION,
      PRODUCT_DEFINITION_FORMATION, COLOUR_RGB,
      SHAPE_REPRESENTATION,
      PRESENTATION_STYLE_ASSIGNMENT, STYLED_ITEM, FILL_AREA_STYLE_COLOUR, FILL_AREA_STYLE,
      SURFACE_STYLE_FILL_AREA, SURFACE_SIDE_STYLE, SURFACE_STYLE_USAGE,
      SHAPE_REPRESENTATION_RELATIONSHIP, DEFINITIONAL_REPRESENTATION,

      _IGNORE,
      DIMENSIONAL_EXPONENTS, DESIGN_CONTEXT, MECHANICAL_CONTEXT, UNCERTAINTY_MEASURE_WITH_UNIT,
      APPLICATION_CONTEXT, APPLICATION_PROTOCOL_DEFINITION, PRODUCT_CATEGORY, PRODUCT_RELATED_PRODUCT_CATEGORY,
      PRODUCT_CATEGORY_RELATIONSHIP, PRODUCT_DEFINITION_CONTEXT, PRODUCT_CONTEXT,
      SECURITY_CLASSIFICATION_LEVEL, SECURITY_CLASSIFICATION, CC_DESIGN_SECURITY_CLASSIFICATION,
      APPROVAL_STATUS, APPROVAL, CC_DESIGN_APPROVAL, CALENDAR_DATE, COORDINATED_UNIVERSAL_TIME_OFFSET,
      LOCAL_TIME, DATE_AND_TIME, APPROVAL_DATE_TIME, DATE_TIME_ROLE, CC_DESIGN_DATE_AND_TIME_ASSIGNMENT,
      PERSON, ORGANIZATION, PERSON_AND_ORGANIZATION, APPROVAL_ROLE, APPROVAL_PERSON_ORGANIZATION,
      PERSON_AND_ORGANIZATION_ROLE, CC_DESIGN_PERSON_AND_ORGANIZATION_ASSIGNMENT, DERIVED_UNIT_ELEMENT,
      DERIVED_UNIT, MEASURE_REPRESENTATION_ITEM, DRAUGHTING_PRE_DEFINED_TEXT_FONT, TEXT_LITERAL,
      TEXT_STYLE_FOR_DEFINED_FONT, TEXT_STYLE_WITH_BOX_CHARACTERISTICS, 
      SHAPE_ASPECT, PROPERTY_DEFINITION, REPRESENTATION, PROPERTY_DEFINITION_REPRESENTATION,
      DRAUGHTING_PRE_DEFINED_CURVE_FONT, CURVE_STYLE, PRESENTATION_LAYER_ASSIGNMENT,
      MECHANICAL_DESIGN_GEOMETRIC_PRESENTATION_REPRESENTATION, 
      PERSONAL_ADDRESS, DRAUGHTING_MODEL, VALUE_REPRESENTATION_ITEM,
      DOCUMENT_TYPE, DOCUMENT, APPLIED_DATE_AND_TIME_ASSIGNMENT,
      PRODUCT_DEFINITION_WITH_ASSOCIATED_DOCUMENTS, GENERAL_PROPERTY, PRE_DEFINED_POINT_MARKER_SYMBOL,
      POINT_STYLE, GENERAL_PROPERTY_ASSOCIATION, DESCRIPTIVE_REPRESENTATION_ITEM,
      ANNOTATION_OCCURRENCE_ASSOCIATIVITY, SYMBOL_COLOUR, PRE_DEFINED_MARKER,
      SYMBOL_STYLE, PRE_DEFINED_TERMINATOR_SYMBOL, SYMBOL_TARGET, DEFINED_SYMBOL,
      LEADER_DIRECTED_CALLOUT, SHAPE_ASPECT_ASSOCIATIVITY, PRODUCT_TYPE, APPLIED_PERSON_AND_ORGANIZATION_ASSIGNMENT,
      APPLIED_APPROVAL_ASSIGNMENT, APPLIED_SECURITY_CLASSIFICATION_ASSIGNMENT, GROUP,
      APPLIED_GROUP_ASSIGNMENT,

      _LATER,
      PLANE_ANGLE_MEASURE_WITH_UNIT, LENGTH_MEASURE_WITH_UNIT, TRIMMED_CURVE, GEOMETRIC_SET,
      COMPOSITE_CURVE, COMPOSITE_CURVE_SEGMENT, DRAUGHTING_PRE_DEFINED_COLOUR,
      GEOMETRICALLY_BOUNDED_SURFACE_SHAPE_REPRESENTATION, GEOMETRIC_CURVE_SET, INVISIBILITY,
      GEOMETRICALLY_BOUNDED_WIREFRAME_SHAPE_REPRESENTATION, QUASI_UNIFORM_CURVE,
      QUASI_UNIFORM_SURFACE,

      _LAST,
   }

   // Returns an enumeration of the form ".UNSPECIFIED."
   string REnum () {
      mR.TryMatch (','); mR.Match ('.');
      int start = mR.Pos - 1;
      mR.SkipTo ('.');
      int end = mR.Pos;
      string s =  Encoding.ASCII.GetString (mRaw.AsSpan (start, end - start));
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
   EType RType () {
      var span = mR.TakeUntil ('(');
      EType type = sDict.GetValueOrDefault (span);
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

   // Nested types -------------------------------------------------------------

   // Data stores --------------------------------------------------------------

   // Private data -------------------------------------------------------------
   // This converts keywords like LINE, CIRCLE, PRODUCT etc to EType enumerations
   static SymTable<EType> sDict = new ();
   string mFile;
   byte[] mRaw;
   UTFReader mR;
}
