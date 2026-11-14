namespace Nori.STEP;

readonly struct Axis3D (int origin, int xaxis, int yaxis) {
   public readonly int ROrigin = origin;
   public readonly int XAxis = xaxis;
   public readonly int YAxis = yaxis;
}

readonly struct BSplineSurfaceWithKnots (int udegree, int vdegree, int[][] pts, string surfform, bool uclosed, bool vclosed, bool intersect, int[] umultiplicities, int[] vmultiplicities, double[] uknots, double[] vknots, string knottype) {
   public readonly int UDegree = udegree;
   public readonly int VDegree = vdegree;
   public readonly int[][] Pts = pts;
   public readonly string SurfForm = surfform;
   public readonly bool UClosed = uclosed;
   public readonly bool VClosed = vclosed;
   public readonly bool Intersect = intersect;
   public readonly int[] UMultiplicities = umultiplicities;
   public readonly int[] VMultiplicities = vmultiplicities;
   public readonly double[] UKnots = uknots;
   public readonly double[] VKnots = vknots;
   public readonly string KnotType = knottype;
}

readonly struct Circle (int coordSys, double radius) {
   public readonly int CoordSys = coordSys;
   public readonly double Radius = radius;
}

readonly struct Cylinder (int coordSys, double radius) {
   public readonly int CoordSys = coordSys;
   public readonly double Radius = radius;
}

readonly struct Edge (int start, int end, int curve, bool dir) {
   public readonly int Start = start;
   public readonly int End = end;
   public readonly int Curve = curve;
   public readonly bool Dir = dir;
}

readonly struct Face (int[] contours, int face, bool dir) {
   public readonly int[] Contours = contours;
   public readonly int Surface = face;
   public readonly bool Dir = dir;
}

readonly struct FaceBound (int edgeloop, bool dir, bool outer) {
   public readonly int EdgeLoop = edgeloop;
   public readonly bool Dir = dir;
   public readonly bool Outer = outer;
}

readonly struct Line (int rOrigin, int rVector) {
   public readonly int ROrigin = rOrigin;
   public readonly int RVector = rVector;
}

readonly struct OEdge (int nEdge, bool dir) {
   public readonly int Edge = nEdge;
   public readonly bool Dir = dir; 
}

readonly struct Vector (int r, double len) {
   public readonly int RDir = r;
   public readonly double Length = len;
}
