namespace Nori.STEP;

enum EKind {
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
   QUASI_UNIFORM_SURFACE, COMPOUND,

   _LAST,
}

readonly struct Ints (int id, EKind kind, int[] data) {
   public override string ToString () => $"#{Id} | {Kind} | {Items.ToCSV ()}";
   public readonly int Id = id;
   public readonly EKind Kind = kind;
   public readonly int[] Items = data;
}

readonly struct ShapeRepr (int id, EKind kind, int[] items, int context) {
   public override string ToString () => $"#{Id} | {Kind} | Ctx={Context} | {Items.ToCSV ()}";
   public readonly int Id = id, Context = context;
   public readonly EKind Kind = kind;
   public readonly int[] Items = items;
}

// -----------------------------------------------------------------------------
readonly struct Axis3D (int origin, int xaxis, int yaxis) {
   public readonly int Origin = origin, XAxis = xaxis, YAxis = yaxis;
}

readonly struct BRepVoids (int outer, int[] inner) {
   public readonly int Outer = outer;
   public readonly int[] Inner = inner;
}

readonly struct BSplineCurve (int degree, int[] pts, string curveform, bool closed, bool intersect, int[] multiplicities, double[] knots, string knottype) {
   public readonly int Degree = degree;
   public readonly int[] Pts = pts;
   public readonly string CurveForm = curveform;
   public readonly bool Closed = closed, Intersect = intersect;
   public readonly int[] Multiplicities = multiplicities;
   public readonly double[] Knots = knots;
   public readonly string KnotType = knottype;
}

readonly struct BSplineSurface (int udegree, int vdegree, int[][] pts, string surfform, bool uclosed, bool vclosed, bool intersect, int[] umultiplicities, int[] vmultiplicities, double[] uknots, double[] vknots, string knottype) {
   public readonly int UDegree = udegree, VDegree = vdegree;
   public readonly int[][] Pts = pts;
   public readonly string SurfForm = surfform;
   public readonly bool UClosed = uclosed, VClosed = vclosed, Inersect = intersect;
   public readonly int[] UMultiplicities = umultiplicities, VMultiplicities = vmultiplicities;
   public readonly double[] UKnots = uknots, VKnots = vknots;
   public readonly string KnotType = knottype;
}

readonly struct Circle (int coordSys, double radius) {
   public readonly int CoordSys = coordSys;
   public readonly double Radius = radius;
}

readonly struct ColorRGB (double r, double g, double b) {
   public readonly double R = r, G = g, B = b;
}

readonly struct Cone (int coordSys, double radius, double halfAngle) {
   public readonly int CoordSys = coordSys;
   public readonly double Radius = radius, HalfAngle = halfAngle;
}

readonly struct Cylinder (int coordSys, double radius) {
   public readonly int CoordSys = coordSys;
   public readonly double Radius = radius;
}

readonly struct Edge (int start, int end, int curve, bool dir) {
   public readonly int Start = start, End = end, Curve = curve;
   public readonly bool Dir = dir;
}

readonly struct Ellipse (int coordSys, double major, double minor) {
   public readonly int CoordSys = coordSys;
   public readonly double RMajor = major, RMinor = minor;
}

readonly struct Face (int[] contours, int face, bool dir) {
   public readonly int[] Contours = contours;
   public readonly int Surface = face;
   public readonly bool Dir = dir;
}

readonly struct FaceBound (int edgeloop, bool dir, bool outer) {
   public readonly int EdgeLoop = edgeloop;
   public readonly bool Dir = dir, Outer = outer;
}

readonly struct Line (int rOrigin, int rVector) {
   public readonly int ROrigin = rOrigin, RVector = rVector;
}

readonly struct AssyUsage (string name, string desc, int parent, int child) {
   public readonly string Name = name, Desc = desc;
   public readonly int Parent = parent, Child = child;
}

readonly struct OEdge (int nEdge, bool dir) {
   public readonly int Edge = nEdge;
   public readonly bool Dir = dir; 
}

readonly struct OShell (int nShell, bool dir) {
   public readonly int Shell = nShell;
   public readonly bool Dir = dir;
}

readonly struct OffsetSurf (int nSurf, double dist) {
   public readonly int Basis = nSurf;
   public readonly double Offset = dist;
}

readonly struct Product (string name, string desc, int[] frames) {
   public readonly string Name = name, Description = desc;
   public readonly int[] Frame = frames;
}

readonly struct Sphere (int coordSys, double radius) {
   public readonly int CoordSys = coordSys;
   public readonly double Radius = radius;
}

readonly struct StyledItem (int[] styles, int target) {
   public readonly int[] Styles = styles;
   public readonly int Target = target;
}

readonly struct StyleOverride (int[] styles, int target, int oldStyle) {
   public readonly int[] Styles = styles;
   public readonly int Target = target, OldStyle = oldStyle;
}

readonly struct SurfCurve (int curve3D, int[] pcurves, string preferred) {
   public readonly int Curve3D = curve3D;
   public readonly int[] PCurves = pcurves;
   public readonly string Preferred = preferred;
}

readonly struct SurfStyleUsage (string side, int style) {
   public readonly string Side = side;
   public readonly int Style = style;
}

readonly struct Torus (int coordsys, double major, double minor, bool apple) {
   public readonly int CoordSys = coordsys;
   public readonly double RMajor = major, RMinor = minor;
   public readonly bool Apple = apple;
}

readonly struct Vector (int r, double len) {
   public readonly int RDir = r;
   public readonly double Length = len;
}

readonly struct TwoRef (int item1, int item2) {
   public readonly int Item1 = item1, Item2 = item2;
}
