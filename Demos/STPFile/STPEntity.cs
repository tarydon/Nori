namespace Nori.STEP;

class Entity { }

class AdvancedFace (int[] contours, int face, bool dir) : Entity {
   public readonly int[] Contours = contours;
   public readonly int Face = face;
   public readonly bool Dir = dir;
}

class BSplineCurve (int degree, bool closed, int[] pts, int[] cknots, int[] knots, double[] weights) : Entity {
   public readonly int Degree = degree;
   public readonly bool Closed = closed;
   public readonly int[] Pts = pts;
   public readonly int[] CKnots = cknots;
   public readonly int[] Knots = knots;
   public readonly double[] Weights = weights;
}

class ClosedShell (int[] faces) : Entity {
   public readonly int[] Faces = faces;
}

class Cartesian (Point3 pt) : Entity {
   public readonly Point3 Pt = pt;
}

class Circle (int coordsys, double radius) : Entity {
   public readonly int CoordSys = coordsys;
   public readonly double Radius = radius;
}

class CoordSys (int origin, int xaxis, int yaxis) : Entity {
   public readonly int Origin = origin;
   public readonly int XAxis = xaxis;
   public readonly int YAxis = yaxis;
}

class Cylinder (int coordsys, double radius) : Entity {
   public readonly int CoordSys = coordsys;
   public readonly double Radius = radius;
}

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
   public readonly bool Dir = dir;
}

class FaceOuterBound (int edgeloop, bool dir) : Entity {
   public readonly int EdgeLoop = edgeloop;
   public readonly bool Dir = dir;
}

class FaceBound (int edgeloop, bool dir) : Entity {
   public readonly int EdgeLoop = edgeloop;
   public readonly bool Dir = dir;
}

class Line (int start, int axis) : Entity {
   public readonly int Start = start;
   public readonly int Ray = axis;
}

class Manifold (int outer) : Entity {
   public readonly int Outer = outer;
}

class OrientedEdge (int edge, bool dir) : Entity {
   public readonly int Edge = edge;
   public readonly bool Dir = dir;
}

class Plane (int coordsys) : Surface {
   public readonly int CoordSys = coordsys;
}

class Surface : Entity { }

class Vector (int direction, double length) : Entity {
   public readonly int Direction = direction;
   public readonly double Length = length;
}

class VertexPoint (int cartesian) : Entity {
   public readonly int Cartesian = cartesian;
}