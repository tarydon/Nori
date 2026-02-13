// ────── ╔╗
// ╔═╦╦═╦╦╬╣ STPEntity.cs
// ║║║║╬║╔╣║ Nori.STEP.Entity and derived types (used when loading in from STEP files)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.STEP;

// Base class for all entities
class Entity {
   public int Id;
}

// Implements the ADVANCED_FACE entity
class AdvancedFace (int[] contours, int face, bool dir) : Entity {   
   public readonly int[] Contours = contours;
   public readonly int Face = face;
   public readonly bool Dir = dir;
}

// Implements the ADVANCED_BREP_SHAPE_REPRESENTATION entity
class AdvancedBRepShapeRepr (int[] items, int context) : ShapeRepr (items, context);

class Axis (int origin, int direction) : Entity {
   public readonly int Origin = origin;
   public readonly int Direction = direction;
}

// Implements the B_SPLINE_CURVE_WITH_KNOTS entity
class BSplineCurveWithKnots (int degree, int[] pts, string curveform, bool closed, bool intersect, int[] multiplicities, double[] knots, string knottype) : Entity {
   public readonly int Degree = degree;
   public readonly int[] Pts = pts;
   public readonly string CurveForm = curveform;
   public readonly bool Closed = closed;
   public readonly bool Intersect = intersect;
   public readonly int[] Multiplicities = multiplicities;
   public readonly double[] Knots = knots;
   public readonly string KnotType = knottype;
}

// Implements the B_SPLINE_SURFACE_WITH_KNOTS entity
class BSplineSurfaceWithKnots (int udegree, int vdegree, int[][] pts, string surfform, bool uclosed, bool vclosed, bool intersect, int[] umultiplicities, int[] vmultiplicities, double[] uknots, double[] vknots, string knottype) : Entity {
   public readonly int UDegree = udegree;
   public readonly int VDegree = vdegree;
   public readonly int[][] Pts = pts;
   public readonly string SurfForm = surfform;
   public bool UClosed = uclosed;
   public bool VClosed = vclosed;
   public bool Intersect = intersect;
   public readonly int[] UMultiplicities = umultiplicities;
   public readonly int[] VMultiplicities = vmultiplicities;
   public readonly double[] UKnots = uknots;
   public readonly double[] VKnots = vknots;
   public readonly string KnotType = knottype;
}

// Implements the CARTESIAN_POINT entity
class Cartesian (Point3 pt) : Entity {
   public readonly Point3 Pt = pt;
}

// Implements the CIRCLE entity
class Circle (int coordsys, double radius) : Entity {
   public readonly int CoordSys = coordsys;
   public readonly double Radius = radius;
}

// Implements the COMPOSITE_CURVE entity
class CompositeCurve (int[] segments, bool intersect) : Entity {
   public readonly int[] Segments = segments;
   public readonly bool Intersect = intersect;
}

class CompositeCurveSegment (bool sameDirection, int segment) : Entity {
   public readonly bool SameDirection = sameDirection; // If false, the segment is reversed from the underlying curve
   public readonly int Segment = segment;
}

// Implements the AXIS2_PLACEMENT_3D entity
class CoordSys (int origin, int zaxis, int xaxis) : Entity {
   public readonly int Origin = origin;
   public readonly int ZAxis = zaxis;
   public readonly int XAxis = xaxis;
}

// Implements the AXIS2_PLACEMENT_2D entity
class CoordSys2 (int origin, int xaxis) : Entity {
   public readonly int Origin = origin;
   public readonly int XAxis = xaxis;
}

// Implements the CONICAL_SURFACE entity
class Cone (int coordsys, double radius, double halfAngle) : ElementarySurface (coordsys) {
   public readonly double Radius = radius;
   public readonly double HalfAngle = halfAngle;
}

// Implements the CYLINDRICAL_SURFACE entity
class Cylinder (int coordsys, double radius) : ElementarySurface (coordsys) {
   public readonly double Radius = radius;
}

// Implements the DEFINITIONAL_REPRESENTATION entity
class DefinitionalRepr (int[] items, int context) : Representation (items, context);

// Implements the DIRECTION entity
class Direction (Vector3 vec) : Entity {
   public readonly Vector3 Vec = vec;
}

// Implements the EDGE_CURVE entity
class EdgeCurve (int start, int end, int basis, bool dir) : Entity {
   public readonly int Start = start;
   public readonly int End = end;
   public readonly int Basis = basis;
   public readonly bool SameSense = dir;
}

// Implements the EDGE_LOOP entity
class EdgeLoop (int[] edges) : Entity {
   public readonly int[] Edges = edges;
}

// Base class for various types of elementary-surfaces
class ElementarySurface (int coordsys) : Entity {
   public readonly int CoordSys = coordsys;
}

// Implements the ELLIPSE entity
class Ellipse (int coordsys, double radius1, double radius2) : Entity {
   public readonly int CoordSys = coordsys;
   public readonly double Radius1 = radius1;
   public readonly double Radius2 = radius2;
}

// Implements the SURFACE_OF_LINEAR_EXTRUSION entity
class ExtrudedSurface (int curve, int vector) : Surface {
   public readonly int Curve = curve;
   public readonly int Vector = vector;
}

// Implements the FACE_BOUND and FACE_OUTER_BOUND entity
class FaceBound (int edgeloop, bool dir, bool outer) : Entity {
   public readonly int EdgeLoop = edgeloop;
   public readonly bool Dir = dir;
   public readonly bool Outer = outer;
}

// Implements the GEOMETRIC_SET entity
class GeometricSet (int[] items) : Entity {
   public readonly int[] Items = items;
}

// Implements the ITEM_DEFINED_TRANSFORMATION entity
class ItemDefinedXfm (int item1, int item2) : Entity {
   public readonly int Item1 = item1;
   public readonly int Item2 = item2;
}

// Implements the LINE entity
class Line (int start, int axis) : Entity {
   public readonly int Start = start;
   public readonly int Ray = axis;
}

// Implements the MANIFOLD_SOLID_BREP entity
class Manifold (int outer) : Entity {
   public readonly int Outer = outer;
}

// Implements the MANIFOLD_SURFACE_SHAPE_REPRESENTATION entity
class ManifoldSurfaceShapeRepr (int[] items, int context) : ShapeRepr (items, context);

// Implements the ORIENTED_EDGE entity
class OrientedEdge (int edge, bool dir) : Entity {
   public readonly int Edge = edge;
   public readonly bool Dir = dir;     // If false, the OrientedEdge is the flip of the underlying Edge
}

// Implements the PLANE entity
class Plane (int coordsys) : ElementarySurface (coordsys);

// Implements the PCURVE entity
class PCurve (int curve, int definition) : Entity {
   public readonly int Curve = curve;
   public readonly int Definition = definition;
}

// Implements the POLYLINE entity
class Polyline (int[] points) : Entity {
   public readonly int[] Points = points;
}

// Base class for various types of representation
class Representation (int[] items, int context) : Entity {
   public readonly int[] Items = items;
   public readonly int Context = context;
}

// Implements the SHAPE_REPRESENTATION entity
class ShapeRepr (int[] items, int context) : Representation (items, context);

// Implements the SHAPE_REPRESENTATION_RELATIONSHIP entity
class ShapeRepRelationship (int rep1, int rep2) : Entity {
   public readonly int Rep1 = rep1;
   public readonly int Rep2 = rep2;
}

// Implements the CLOSED_SHELL and OPEN_SHELL entities
class Shell (int[] faces) : Entity {
   public readonly int[] Faces = faces;
}

// Implements the SHELL_BASED_SURFACE_MODEL entity
class ShellBasedSurfaceModel (int[] shells) : Entity {
   public readonly int[] Shells = shells;
}

// Implements the SPHERICAL_SURFACE entity
class Sphere (int coordsys, double radius) : ElementarySurface (coordsys) {
   public readonly double Radius = radius;
}

// Implements the SURFACE_OF_REVOLUTION entity
class SpunSurface (int curve, int axis) : Surface {
   public readonly int Curve = curve;
   public readonly int Axis = axis;
}

// Base class for various types of surfaces
class Surface : Entity;

// Implements the SURFACE_CURVE entity
class SurfaceCurve (int curve, int[] associated, string repr) : Entity {
   public readonly int Curve = curve;
   public readonly int[] Associated = associated;
   public readonly string Repr = repr;
}

// Implements the TOROIDAL_SURFACE entity
class Toroid (int coordsys, double major, double minor) : ElementarySurface (coordsys) {
   public readonly double MajorRadius = major;
   public readonly double MinorRadius = minor;
}

// Implementes the TRIMMED_CURVE entity
class TrimmedCurve (int curve, TrimSelect trimstart, TrimSelect trimend, bool samesense, string masterRepresentation) : Entity {
   public readonly int Curve = curve;
   public readonly TrimSelect TrimStart = trimstart;
   public readonly TrimSelect TrimEnd = trimend;
   public readonly bool SameSense = samesense; // If false, the TrimmedCurve is the flip of the underlying curve
   public readonly bool PreferCartesianTrim = masterRepresentation == ".CARTESIAN.";
}

class TrimSelect (int cartesian, double parameter) {
   public readonly int Cartesian = cartesian;
   public readonly double Parameter = parameter;
}

// Implements the VECTOR entity
class Vector (int direction, double length) : Entity {
   public readonly int Direction = direction;
   public readonly double Length = length;
}

// Implements the VERTEX_POINT entity
class VertexPoint (int cartesian) : Entity {
   public readonly int Cartesian = cartesian;
}
