// ────── ╔╗
// ╔═╦╦═╦╦╬╣ STPEntity.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.STEP;

class Entity {
   public int Id;
}

class AdvancedFace (int[] contours, int face, bool dir) : Entity {
   public readonly int[] Contours = contours;
   public readonly int Face = face;
   public readonly bool Dir = dir;
}

class AdvancedBRepShapeRepr (int[] items, int context) : ShapeRepr (items, context) { }

class BSplineCurve (int degree, int[] pts, string curveform, bool closed, bool intersect) : Entity {
   public readonly int Degree = degree;
   public readonly int[] Pts = pts;
   public readonly string CurveForm = curveform;
   public readonly bool Closed = closed;
   public readonly bool Intersect = intersect;
}

class BSplineCurveWithKnots (int degree, int[] pts, string curveform, bool closed, bool intersect, int[] multiplicities, double[] knots, string knottype)
    : BSplineCurve (degree, pts, curveform, closed, intersect) {
   public readonly int[] Multiplicities = multiplicities;
   public readonly double[] Knots = knots;
   public readonly string KnotType = knottype;
}

class BSplineSurface (int udegree, int vdegree, int[][] pts, string surfform, bool uclosed, bool vclosed, bool intersect) : Surface {
   public readonly int UDegree = udegree;
   public readonly int VDegree = vdegree;
   public readonly int[][] Pts = pts;
   public readonly string SurfForm = surfform;
   public bool UClosed = uclosed;
   public bool VClosed = vclosed;
   public bool Intersect = intersect;
}

class BSplineSurfaceWithKnots (int udegree, int vdegree, int[][] pts, string surfform, bool uclosed, bool vclosed, bool intersect, int[] umultiplicities, int[] vmultiplicities, double[] uknots, double[] vknots, string knottype)
    : BSplineSurface (udegree, vdegree, pts, surfform, uclosed, vclosed, intersect) {
   public readonly int[] UMultiplicities = umultiplicities;
   public readonly int[] VMultiplicities = vmultiplicities;
   public readonly double[] UKnots = uknots;
   public readonly double[] VKnots = vknots;
   public readonly string KnotType = knottype;
}

class Shell (int[] faces) : Entity {
   public readonly int[] Faces = faces;
}

class ExtrudedSurface (int curve, int vector) : Surface {
   public readonly int Curve = curve;
   public readonly int Vector = vector;
}

class CompositeCurve (int[] segments, bool intersect) : Entity {
   public readonly int[] Segments = segments;
   public readonly bool Intersect = intersect;
}

class Cartesian (Point3 pt) : Entity {
   public readonly Point3 Pt = pt;
}

class Circle (int coordsys, double radius) : Entity {
   public readonly int CoordSys = coordsys;
   public readonly double Radius = radius;
}

class Ellipse (int coordsys, double radius1, double radius2) : Entity {
   public readonly int CoordSys = coordsys;
   public readonly double Radius1 = radius1;
   public readonly double Radius2 = radius2;
}

class Cone (int coordsys, double radius, double halfAngle) : ElementarySurface (coordsys) {
   public readonly double Radius = radius;
   public readonly double HalfAngle = halfAngle;
}

class CoordSys (int origin, int xaxis, int yaxis) : Entity {
   public readonly int Origin = origin;
   public readonly int ZAxis = xaxis;
   public readonly int XAxis = yaxis;
}

class CoordSys2 (int origin, int xaxis) : Entity {
   public readonly int Origin = origin;
   public readonly int XAxis = xaxis;
}

class Cylinder (int coordsys, double radius) : ElementarySurface (coordsys) {
   public readonly double Radius = radius;
}

class DefinitionalRepr (int[] items, int context) : Representation (items, context) { }

class Direction (Vector3 vec) : Entity {
   public readonly Vector3 Vec = vec;
}

class EdgeLoop (int[] edges) : Entity {
   public readonly int[] Edges = edges;
}

class EdgeCurve (int start, int end, int basis, bool dir) : Entity {
   public readonly int Start = start;
   public readonly int End = end;
   public readonly int Basis = basis;
   public readonly bool SameSense = dir;
}

class FaceOuterBound (int edgeloop, bool dir) : Entity {
   public readonly int EdgeLoop = edgeloop;
   public readonly bool Dir = dir;
}

class FaceBound (int edgeloop, bool dir) : Entity {
   public readonly int EdgeLoop = edgeloop;
   public readonly bool Dir = dir;
}

class ItemDefinedXfm (int item1, int item2) : Entity {
   public readonly int Item1 = item1;
   public readonly int Item2 = item2;
}

class Line (int start, int axis) : Entity {
   public readonly int Start = start;
   public readonly int Ray = axis;
}

class Manifold (int outer) : Entity {
   public readonly int Outer = outer;
}

class ManifoldSurfaceShapeRepr (int[] items, int context) : ShapeRepr (items, context) { }

class OrientedEdge (int edge, bool dir) : Entity {
   public readonly int Edge = edge;
   public readonly bool Dir = dir;     // If false, the OrientedEdge is the flip of the underlying Edge
}

class Plane (int coordsys) : ElementarySurface (coordsys) { }

class PCurve (int curve, int definition) : Entity {
   public readonly int Curve = curve;
   public readonly int Definition = definition;
}

class Polyline (int[] points) : Entity {
   public readonly int[] Points = points;
}

class Representation (int[] items, int context) : Entity {
   public readonly int[] Items = items;
   public readonly int Context = context;
}

class ShapeRepr (int[] items, int context) : Representation (items, context) { }

class ShapeRepRelationship (int rep1, int rep2) : Entity {
   public readonly int Rep1 = rep1;
   public readonly int Rep2 = rep2;
}

class ShellBasedSurfaceModel (int[] shells) : Entity {
   public readonly int[] Shells = shells;
}

class Surface () : Entity { }

class Toroid (int coordsys, double major, double minor) : ElementarySurface (coordsys) {
   public readonly double MajorRadius = major;
   public readonly double MinorRadius = minor;
}

class ElementarySurface (int coordsys) : Entity {
   public readonly int CoordSys = coordsys;
}

class Sphere (int coordsys, double radius) : ElementarySurface (coordsys) {
   public readonly double Radius = radius;
}

class SurfaceCurve (int curve, int[] associated, string repr) : Entity {
   public readonly int Curve = curve;
   public readonly int[] Associated = associated;
   public readonly string Repr = repr;
}

class Vector (int direction, double length) : Entity {
   public readonly int Direction = direction;
   public readonly double Length = length;
}

class VertexPoint (int cartesian) : Entity {
   public readonly int Cartesian = cartesian;
}
