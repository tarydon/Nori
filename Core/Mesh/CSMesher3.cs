namespace Nori;

public class CSMesher3 {
   // Constructors -------------------------------------------------------------
   public CSMesher3 (IEnumerable<Poly> front, IEnumerable<Poly> side) {
      mFront = [.. front]; mSide = [.. side];
   }
   List<Poly> mFront, mSide;

   // Properties ---------------------------------------------------------------
   /// <summary>
   /// Tessellation accuracy
   /// </summary>
   public ETess Tess = ETess.Medium;

   // Methods ------------------------------------------------------------------
   public IEnumerable<string> IncBuild () {
      Discretize (); 

      yield return "";
   }

   // Implementation -----------------------------------------------------------
   void Discretize () {
      // First, find all the unique values of Y among all the discretized poly
      List<Point2> pts = [];
      HashSet<float> yUnique = [];
      foreach (var poly in mFront.Concat (mSide)) {
         pts.Clear (); poly.Discretize (pts, Tess);
         foreach (var pt in pts) {
            Point2f ptf = new (pt.X.R3 (), pt.Y.R3 ()); 
            yUnique.Add (ptf.Y);
         }
      }
      foreach (var y in yUnique.Order ()) { mYDict.Add (y, mYList.Count); mYList.Add (y); }

      Console.Write ("X");
   }

   List<float> mYList = [];               // List of unique Y values
   Dictionary<float, int> mYDict = [];    // Map of Y values into unique indices
   List<Point2f> mNodes = [];    // All the nodes for all the polys, discretized
   List<int> mSplits = [];       // Splits those nodes into unique polys
}
