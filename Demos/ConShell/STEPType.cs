namespace Nori.STEP;

readonly struct Axis3D (int origin, int xaxis, int yaxis) {
   public readonly int ROrigin = origin, XAxis = xaxis, YAxis = yaxis;
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

readonly struct Cylinder (int coordSys, double radius) {
   public readonly int CoordSys = coordSys;
   public readonly double Radius = radius;
}

readonly struct Edge (int start, int end, int curve, bool dir) {
   public readonly int Start = start, End = end, Curve = curve;
   public readonly bool Dir = dir;
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

readonly struct OEdge (int nEdge, bool dir) {
   public readonly int Edge = nEdge;
   public readonly bool Dir = dir; 
}

readonly struct Product (string name, string desc, int[] frames) {
   public readonly string Name = name, Description = desc;
   public readonly int[] Frame = frames;
}

readonly struct ShapeRepr (int[] items, int context) {
   public readonly int[] Items = items;
   public readonly int Context = context;
}

readonly struct StyledItem (int[] styles, int target) {
   public readonly int[] Styles = styles;
   public readonly int Target = target;
}

readonly struct Vector (int r, double len) {
   public readonly int RDir = r;
   public readonly double Length = len;
}

readonly struct Link (int item1, int item2) {
   public readonly int Item1 = item1, Item2 = item2;
}
