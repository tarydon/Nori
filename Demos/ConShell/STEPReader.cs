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
               if (mR.Peek () == '(') RCompound (id);
               else REntity (id, RType ());
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

   // Reads a standard entity of the given type
   void REntity (int id, EType type) {
      if (type > _IGNORE) return;
      mR.Match ('\'').SkipTo ('\'').Match (',');
      int slot;
      switch (type) {
         case ADVANCED_FACE: slot = Add (mFace, new Face (RRefs (), RRef (), RBool ())); break;
         case AXIS2_PLACEMENT_3D: slot = Add (mAxis3D, new (RRef (), RRef (), RRef ())); break;
         case CARTESIAN_POINT: slot = Add (mPoint, RPoint3 ()); break;
         case CLOSED_SHELL: slot = Add (mInts, RRefs ()); break;
         case DIRECTION: slot = Add (mDirection, RVector3 ()); break;
         case EDGE_LOOP: slot = Add (mInts, RRefs ()); break;
         case EDGE_CURVE: slot = Add (mCurve, new Edge (RRef (), RRef (), RRef (), RBool ())); break;
         case FACE_BOUND: slot = Add (mFaceBound, new (RRef (), RBool (), false)); break;
         case FACE_OUTER_BOUND: slot = Add (mFaceBound, new (RRef (), RBool (), true)); break;
         case LINE: slot = Add (mLine, new (RRef (), RRef ())); break;
         case MANIFOLD_SOLID_BREP: slot = Add (mInt, RRef ()); break;
         case OPEN_SHELL: slot = Add (mInts, RRefs ()); break;
         case ORIENTED_EDGE:
            mR.Match ('*').Match (',').Match ('*').Match (',');
            slot = Add (mOEdge, new (RRef (), RBool ()));
            break;
         case PLANE: slot = Add (mInt, RRef ()); break;
         case VECTOR: slot = Add (mVector, new (RRef (), RDouble ())); break;
         case VERTEX_POINT: slot = Add (mInt, RRef ()); break;
         default: throw new BadCaseException ($"Unhandled entity: #{id} = {type}");
      }
      while (mData.Length <= id) Array.Resize (ref mData, mData.Length * 2);
      mData[id] = (type, slot);
   }

   // Reads an array of integers of the form "(#1,#2,#3)"
   int[] RRefs () {
      mTmp.Clear ();
      mR.TryMatch (','); mR.Match ('(');
      for (; ; ) {
         if (mR.TryMatch (')')) break;
         mTmp.Add (RRef ());
      }
      return [.. mTmp];
   }
   List<int> mTmp = [];

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
   internal enum EType {
      _NIL,
      _FIRST,
      DIRECTION, VECTOR, CARTESIAN_POINT, LINE, VERTEX_POINT, AXIS2_PLACEMENT_3D, PLANE,
      ORIENTED_EDGE, EDGE_LOOP, FACE_OUTER_BOUND, ADVANCED_FACE, CLOSED_SHELL, MANIFOLD_SOLID_BREP,
      EDGE_CURVE,
      CYLINDRICAL_SURFACE, CIRCLE, FACE_BOUND, 
      PRODUCT_DEFINITION_CONTEXT, PRODUCT_CONTEXT, PRODUCT_DEFINITION_FORMATION, DERIVED_UNIT_ELEMENT,
      DERIVED_UNIT, MEASURE_REPRESENTATION_ITEM, DRAUGHTING_PRE_DEFINED_COLOUR, COLOUR_RGB,
      SHAPE_ASPECT, PROPERTY_DEFINITION, SHAPE_REPRESENTATION, REPRESENTATION, 
      PROPERTY_DEFINITION_REPRESENTATION, DRAUGHTING_PRE_DEFINED_CURVE_FONT, CURVE_STYLE,
      PRESENTATION_STYLE_ASSIGNMENT, STYLED_ITEM, FILL_AREA_STYLE_COLOUR, FILL_AREA_STYLE,
      SURFACE_STYLE_FILL_AREA, SURFACE_SIDE_STYLE, SURFACE_STYLE_USAGE, LENGTH_MEASURE_WITH_UNIT,
      MECHANICAL_DESIGN_GEOMETRIC_PRESENTATION_REPRESENTATION, B_SPLINE_SURFACE_WITH_KNOTS,
      B_SPLINE_CURVE_WITH_KNOTS, PRESENTATION_LAYER_ASSIGNMENT, SHAPE_REPRESENTATION_RELATIONSHIP,
      GEOMETRIC_SET, TRIMMED_CURVE, GEOMETRICALLY_BOUNDED_SURFACE_SHAPE_REPRESENTATION,
      TOROIDAL_SURFACE, PERSONAL_ADDRESS, DRAUGHTING_MODEL, COMPOSITE_CURVE_SEGMENT,
      COMPOSITE_CURVE, SURFACE_OF_LINEAR_EXTRUSION, ELLIPSE, OVER_RIDING_STYLED_ITEM, 
      CONICAL_SURFACE, VALUE_REPRESENTATION_ITEM, CONTEXT_DEPENDENT_SHAPE_REPRESENTATION,
      ITEM_DEFINED_TRANSFORMATION, NEXT_ASSEMBLY_USAGE_OCCURRENCE, SPHERICAL_SURFACE,
      GEOMETRICALLY_BOUNDED_WIREFRAME_SHAPE_REPRESENTATION, GEOMETRIC_CURVE_SET, INVISIBILITY,
      POINT_STYLE, PRE_DEFINED_POINT_MARKER_SYMBOL, OPEN_SHELL, SHELL_BASED_SURFACE_MODEL,
      MANIFOLD_SURFACE_SHAPE_REPRESENTATION, DOCUMENT_TYPE, DOCUMENT, 
      PRODUCT_DEFINITION_WITH_ASSOCIATED_DOCUMENTS, MAPPED_ITEM, REPRESENTATION_MAP,
      APPLIED_DATE_AND_TIME_ASSIGNMENT, SURFACE_CURVE, FACE_SURFACE, GENERAL_PROPERTY,
      GENERAL_PROPERTY_ASSOCIATION, DESCRIPTIVE_REPRESENTATION_ITEM, POLYLINE,
      DRAUGHTING_PRE_DEFINED_TEXT_FONT, TEXT_LITERAL, TEXT_STYLE_FOR_DEFINED_FONT,
      TEXT_STYLE_WITH_BOX_CHARACTERISTICS, ANNOTATION_OCCURRENCE_ASSOCIATIVITY, SYMBOL_COLOUR,
      SYMBOL_STYLE, PRE_DEFINED_TERMINATOR_SYMBOL, SYMBOL_TARGET, DEFINED_SYMBOL,
      LEADER_DIRECTED_CALLOUT, SHAPE_ASPECT_ASSOCIATIVITY, PCURVE, DEFINITIONAL_REPRESENTATION,
      PRODUCT_TYPE, PRE_DEFINED_MARKER, QUASI_UNIFORM_CURVE, APPLIED_PERSON_AND_ORGANIZATION_ASSIGNMENT,
      APPLIED_APPROVAL_ASSIGNMENT, APPLIED_SECURITY_CLASSIFICATION_ASSIGNMENT, GROUP, 
      APPLIED_GROUP_ASSIGNMENT, AXIS2_PLACEMENT_2D, QUASI_UNIFORM_SURFACE, VERTEX_LOOP,
      ORIENTED_CLOSED_SHELL, BREP_WITH_VOIDS, AXIS1_PLACEMENT, SURFACE_OF_REVOLUTION,
      INTERSECTION_CURVE, OFFSET_SURFACE, DEGENERATE_TOROIDAL_SURFACE,

      _IGNORE,
      DIMENSIONAL_EXPONENTS, PLANE_ANGLE_MEASURE_WITH_UNIT, UNCERTAINTY_MEASURE_WITH_UNIT,
      APPLICATION_CONTEXT, APPLICATION_PROTOCOL_DEFINITION, DESIGN_CONTEXT, MECHANICAL_CONTEXT,
      PRODUCT, PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE, PRODUCT_CATEGORY,
      PRODUCT_RELATED_PRODUCT_CATEGORY, PRODUCT_CATEGORY_RELATIONSHIP, SECURITY_CLASSIFICATION_LEVEL,
      SECURITY_CLASSIFICATION, CC_DESIGN_SECURITY_CLASSIFICATION, APPROVAL_STATUS, APPROVAL,
      CC_DESIGN_APPROVAL, CALENDAR_DATE, COORDINATED_UNIVERSAL_TIME_OFFSET, LOCAL_TIME,
      DATE_AND_TIME, APPROVAL_DATE_TIME, DATE_TIME_ROLE, CC_DESIGN_DATE_AND_TIME_ASSIGNMENT,
      PERSON, ORGANIZATION, PERSON_AND_ORGANIZATION, APPROVAL_ROLE, APPROVAL_PERSON_ORGANIZATION,
      PERSON_AND_ORGANIZATION_ROLE, CC_DESIGN_PERSON_AND_ORGANIZATION_ASSIGNMENT,
      ADVANCED_BREP_SHAPE_REPRESENTATION, PRODUCT_DEFINITION, PRODUCT_DEFINITION_SHAPE,
      SHAPE_DEFINITION_REPRESENTATION,

      _LAST,
   }

   // Data stores --------------------------------------------------------------
   List<Axis3D> mAxis3D = [];
   List<Edge> mCurve = [];
   List<Face> mFace = [];
   List<FaceBound> mFaceBound = [];
   List<Vector3> mDirection = [];
   List<int> mInt = [];
   List<int[]> mInts = [];
   List<Line> mLine = [];
   List<OEdge> mOEdge = [];
   List<Point3> mPoint = [];
   List<Vector> mVector = [];
   (EType, int)[] mData = new (EType, int)[128];

   // Private data -------------------------------------------------------------
   // This converts keywords like LINE, CIRCLE, PRODUCT etc to EType enumerations
   static SymTable<EType> sDict = new ();
   string mFile;
   byte[] mRaw;
   UTFReader mR;
}
