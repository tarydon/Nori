// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ GLTess.cs
// ║║║║╬║╔╣║ Implements GLU based 2D and 3D tessellators
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static Nori.GLU;
namespace Nori;

#region class Tess2D -------------------------------------------------------------------------------
/// <summary>Given a list of points as inner and outer polygon contours, generate a tesselation in 2D</summary>
/// This OpenGL based tessellator uses the GLU libraries to tessellate polygons.
/// (See Tessellators and Quadrics in "The Red Book" for more details on using tessellation sub-API)
/// <param name="pts">Set of all contour points</param>
/// <param name="splits">Contour indices in the points array. E.g: [0, 10, 14] has two contours [0, 10) and [10, 14)</param>
public class Tess2D (List<Point2> pts, IReadOnlyList<int> splits) {
   /// <summary>Error results after tessellation (if any)</summary>
   public string Error => mError;
   string mError = string.Empty;

   /// <summary>What is the minimum area of triangles below which they are rejected?</summary>
   public double MinArea { get; set; } = 1E-12;

   /// <summary>Tessellates the polygons with outer and inner contours into triangles</summary>
   /// <param name="pts">List of all contour points.</param>
   /// <param name="splits">List of indices defining the contour boundaries.</param>
   /// <returns>The triangle indices</returns>
   public static List<int> Process (List<Point2> pts, IReadOnlyList<int> splits)
      => new Tess2D (pts, splits).Process ();

   // Properties ---------------------------------------------------------------
   /// <summary>If this is turned on, then we need the tessellator to return edge flags.</summary>
   /// The tessellator normally return a list of integers. These integers, taken in sets of
   /// 3, provide the indices into the points array to define each triangle. If this flag
   /// is set, bit 30 (0x40000000) of each integer is set if the edge leading out from this
   /// vertex is a tagged with the 'edge flag'. That is, that edge was part of the original
   /// outer boundary of one of the polygons supplied, and not an 'inner' edge.
   public bool NeedEdgeFlags { protected get; set; }

   /// <summary>Set the winding rule to use for computing the tessellation.</summary>
   /// See the documentation for gluTessProperty to understand about the winding rule
   public EWindingRule WindingRule { protected get; set; } = EWindingRule.Odd;

   // Implementation -----------------------------------------------------------
   public unsafe List<int> Process () {
      // Initialize tessellator the first time we are called
      var tess = sTess == HTesselator.Zero ? (sTess = NewTess ()) : sTess;
      // Setup tessellation callbacks
      tess.SetCallback (TessBegin).SetCallback (TessVertex)
         .SetCallback (TessCombine).SetCallback (TessError)
         .SetCallback (NeedEdgeFlags ? TessEdgeFlag : null!)
         // Set up the tessellation properties
         .SetWinding (WindingRule);

      // Generate the array of doubles we use to provide input
      int n = mPts.Count;
      double[] vals = new double[n * 3];
      for (int i = 0; i < n; i++) (vals[i * 3], vals[i * 3 + 1]) = mPts[i];
      fixed (double* pf = vals) {
         tess.SetNormal (0, 0, 1);
         tess.BeginPolygon (nint.Zero);
         for (int i = 1; i < mSplit.Count; i++) {
            int a = mSplit[i - 1], b = mSplit[i];
            tess.BeginContour ();
            for (int j = a; j < b; j++)
               tess.AddVertex (pf + j * 3, j);
            tess.EndContour ();
         }
         tess.EndPolygon ();
      }
      return mResult;
   }

   // GLU callbacks ------------------------------------------------------------
   // Called whenever a new triangle primitive begins
   GLUtessBeginProc TessBegin => type => (mPrimType, mnVerts) = (type, mnTriangles = 0);
   // Called to set the edge flag before outputting a vertex
   GLUtessEdgeFlagProc TessEdgeFlag => flag => miNextEdge = flag != 0;
   // Callback used to report errors during tessellation (this is very very rare)
   GLUtessErrorProc TessError => error => mError = $"Tesselation error: {error}";
   // Called to record triangle indices
   GLUtessVertexDataProc TessVertex => (data, _) => {
      miEdge[mnVerts] = miNextEdge ? EdgeBit : 0;
      mIdx[mnVerts] = (int)data;
      if (++mnVerts == 3) {
         // If we got 3 vertices, we can output a triangle
         if (NeedEdgeFlags)
            AddTriangle (mIdx[0] | miEdge[0], mIdx[1] | miEdge[1], mIdx[2] | miEdge[2]);
         else
            AddTriangle (mIdx[0], mIdx[1], mIdx[2]);
         switch (mPrimType) {
            case EPrimitive.Triangles:
               mnVerts = 0;
               break;
            case EPrimitive.TriangleStrip:
               mnTriangles++; mnVerts = 2;
               if ((mnTriangles & 1) != 0) mIdx[0] = mIdx[2];
               else mIdx[1] = mIdx[2];
               break;
            case EPrimitive.TriangleFan:
               mnVerts = 2;
               mIdx[1] = mIdx[2];
               break;
            default:
               throw new NotImplementedException ();
         }
      }

      // Called from to add a triangle to the output
      void AddTriangle (int n1, int n2, int n3) {
         if ((n1 & EdgeBit) == 0 && (n2 & EdgeBit) == 0 && (n3 & EdgeBit) == 0) {
            var vec1 = (Vector3)(mPts[n1 & 0xffffff] - mPts[n2 & 0xffffff]);
            var vec2 = (Vector3)(mPts[n2 & 0xffffff] - mPts[n3 & 0xffffff]);
            if ((vec1 * vec2).LengthSq < MinArea) return;
         }
         mResult.AddRange (n1, n2, n3);
      }
   };
   // Called when a new vertex needs to be generated at an intersection point.
   // The paramter coords contains the location of the new point to be added to the list
   // of points. We must return the index of the newly added point into *pout.
   unsafe GLUtessCombineProc TessCombine => (coords, _, _, pout) => {
      *pout = NewVertex (coords[0], coords[1], coords[2]);

      // This is called by the combine-callback when it needs to generate new points
      int NewVertex (double x, double y, double _) {
         mPts.Add (new (x, y));
         return mPts.Count - 1;
      }
   };

   // Private data -------------------------------------------------------------
   // Polygon to tessellate.
   readonly List<Point2> mPts = pts;
   // Polygon outer/inner contour boundaries.
   readonly IReadOnlyList<int> mSplit = splits;

   // Values shared between TessBegin and TessVertex callbacks
   int mnVerts, mnTriangles;
   // The current primitive type the tessellator is outputing.
   EPrimitive mPrimType;
   // Is the next edge (to be output to TessVertex) a boundary edge?
   bool miNextEdge;
   // The list of edge flags; each value here is 0 or 0x40000000
   readonly int[] miEdge = new int[3];
   // The list of indices for the triangle
   readonly int[] mIdx = new int[3];
   /// <summary>This stores the resultant tessellation</summary>
   readonly List<int> mResult = [];

   // The GLU tessellator object
   [ThreadStatic]
   static HTesselator sTess;

   // Nested types and constants -----------------------------------------------
   // This is the bit that is set when we return edge flags
   internal const int EdgeBit = 0x40000000;
}
#endregion

#region class BooleanOps ---------------------------------------------------------------------------
/// <summary>BooleanOps is used to do fast boolean operations on polys with no curves</summary>
public static class BooleanOps {
   /// <summary>This performs a union of two given poly objects</summary>
   public static List<Poly> Union (this Poly a, Poly b) => Union ([a, b]);
   /// <summary>This performs a union of a number of polys</summary>
   public static List<Poly> Union (this ReadOnlySpan<Poly> input)
      => new Boolean (input).Process ();

   /// <summary>Computes the intersection of two polys</summary>
   public static List<Poly> Intersect (this Poly a, Poly b) => new Boolean ([a, b]).Process (EWindingRule.AbsGeqTwo);

   /// <summary>Computes the intersection of given polys.</summary>
   public static List<Poly> Intersect (this ReadOnlySpan<Poly> input) {
      if (input.Length < 2) return [..input];
      List<Poly> result = [input[^1]];
      for (int i = input.Length - 2; i >= 0; i--) {
         var b = input[i];
         result = [.. result.SelectMany (a => Intersect (a, b))];
      }
      return result;
   }

   /// <summary>Subtracts negative poly from the positive one.</summary>
   /// This routine assumes that both positive and negative have same winding.
   /// <param name="positive">The poly object to be subtracted from.</param>
   /// <param name="negative">The poly object being subtracted.</param>
   /// <returns></returns>
   public static List<Poly> Subtract (this Poly positive, Poly negative) => Subtract ([positive], [negative]);

   /// <summary>Subtract one set of polys from another.</summary>
   /// <param name="positive">The polys from which other polys are subtracted.</param>
   /// <param name="negative">The polys which are subtracted.</param>
   /// Subtraction operation requires the negative polys to be reversed, which is an additional
   /// operation. If the input polys are already reversed, call Union to do the subtraction instead.
   public static List<Poly> Subtract (this ReadOnlySpan<Poly> positive, ReadOnlySpan<Poly> negative) {
      List<Poly> input = [.. positive];
      foreach (var poly in negative) input.Add (poly.Reversed ());
      return Union (input.AsSpan ());
   }

   // Implementation -----------------------------------------------------------
   class Boolean {
      // Constructs a Boolean object with a number of polys
      public Boolean (ReadOnlySpan<Poly> input) {
         mSplit.Add (0);
         List<Point2> pts = [];
         foreach (var poly in input) {
            // Snap the polys to a micron grid.
            if (poly.HasArcs) {
               pts.Clear ();
               poly.Discretize (pts, 0.05, 0.5411);   // 0.5411 ~ 30 degrees
               mPts.AddRange (pts.Select (p => p.R6 ()));
            } else mPts.AddRange (poly.Pts.Select (x => x.R6 ()));
            mSplit.Add (mPts.Count);
         }
         mcStdPts = mPts.Count;
      }

      // Does the actual processing
      internal unsafe List<Poly> Process (EWindingRule winding = EWindingRule.Positive) {
         var tess = sTesselator;
         if (tess == HTesselator.Zero) tess = sTesselator = NewTess ();
         // Set up the callbacks
         tess.SetCallback (TessBegin).SetCallback (TessVertex)
            .SetCallback (TessCombine).SetCallback (TessEnd)
            .SetCallback (TessError)
            // Set up the winding rule property, and the 'boundary-only' flag
            .SetWinding (winding).SetOnlyBoundary (true);
         // Submit tessellation input.
         double[] vals = new double[mPts.Count * 3];
         for (int i = 0; i < mPts.Count; i++)
            (vals[i * 3], vals[i * 3 + 1]) = mPts[i];
         fixed (double* pf = vals) {
            tess.SetNormal (0, 0, 1);
            tess.BeginPolygon (nint.Zero);
            for (int i = 1; i < mSplit.Count; i++) {
               int a = mSplit[i - 1], b = mSplit[i];
               tess.BeginContour ();
               for (int j = a; j < b; j++)
                  tess.AddVertex (pf + j * 3, j);
               tess.EndContour ();
            }
            // Invoke tessellation
            tess.EndPolygon ();
         }
         return mOutput;
      }
      // The GLU tessellator object.
      [ThreadStatic]
      static HTesselator sTesselator;

      // Callbacks ----------------------------------------------------------------
      // Called whenever a new triangle begins. The first call alone creates a new PolyBuilder
      // which is reset automatically after PolyBuilder.Build () in TessEnd call.
      // So subsequent TessBegin calls are no-ops.
      GLUtessBeginProc TessBegin => _ => mBuilder ??= new ();
      PolyBuilder mBuilder = null!;
      // Called when a new vertex needs to be generated at an intersection point.
      // The paramter coords contains the location of the new point to be added to the list
      // of points. We must return the index of the newly added point into *pout.
      unsafe GLUtessCombineProc TessCombine => (coords, _, _, pout) => {
         *pout = NewVertex (coords[0], coords[1]);

         // Generates a new vertex or returns an existing (added in a previous Combine call) and
         // returns the vertex index.
         int NewVertex (double x, double y) {
            Point2 pt = new (x, y);
            for (int i = mPts.Count - 1; i >= mcStdPts; i--)
               if (mPts[i].EQ (pt)) return i;
            mPts.Add (pt);
            return mPts.Count - 1;
         }
      };
      // This is called when a poly ends; we add the poly we're constructing in
      GLUtessEndProc TessEnd => () => {
         var poly = mBuilder.Close ().Build ();
         if (poly.Count > 2 || poly.GetBound ().Area > 0.1) mOutput.Add (poly);
      };
      // Callback used to report errors during tesselation (this is very very rare)
      static GLUtessErrorProc TessError => error => Console.WriteLine ("TessError {0}", error);
      // Called to output a new vertex; we handle all cases of triangle, triangle-strip and triangle-fan
      GLUtessVertexDataProc TessVertex => (data, _) => mBuilder.Line (mPts[(int)data]);

      // Private data -------------------------------------------------------------
      // This is the list of points in all the polys
      readonly List<Point2> mPts = [];
      // These are the splits that slice up the list of pts into individual polygons
      readonly List<int> mSplit = [];
      // The number of points we added in the original input.
      // Points beyond this in the mPts array were added by the combine-callback
      readonly int mcStdPts;
      // The output list
      readonly List<Poly> mOutput = [];
   }
}
#endregion
