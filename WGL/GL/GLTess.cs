// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ GLTess.cs
// ║║║║╬║╔╣║ Implements GLU based 2D and 3D tessellators
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static Nori.GLU;

namespace Nori;

/// <summary>A helper class used to inject the GLU based 2D tessllator implementations.</summary>
public class GLTess2D : Tess2D {
   public override List<int> Do (List<Point2> pts, IReadOnlyList<int> splits) {
      var tess = new GLTess2DImp (pts, splits); 
      var res = tess.Process ();
      mError = tess.Error;
      return res;
   }
}

/// <summary>A base class for all GLU based tessellators.</summary>
abstract class GLTessImp : Tessellator {
   public GLTessImp () => WindingRule = EWindingRule.Odd;

   #region Properties ------------------------------------------------
   /// <summary>If this is turned on, then we need the tessellator to return edge flags.</summary>
   /// The tessellator normally return a list of integers. These integers, taken in sets of
   /// 3, provide the indices into the points array to define each triangle. If this flag
   /// is set, bit 30 (0x40000000) of each integer is set if the edge leading out from this
   /// vertex is a tagged with the 'edge flag'. That is, that edge was part of the original
   /// outer boundary of one of the polygons supplied, and not an 'inner' edge.
   public bool NeedEdgeFlags { protected get; set; }

   /// <summary>Set the winding rule to use for computing the tessellation.</summary>
   /// See the documentation for gluTessProperty to understand about the winding rule
   public EWindingRule WindingRule { protected get; set; }
   #endregion

   #region GLU callbacks ---------------------------------------------
   /// <summary>Called whenever a new triangle begins</summary>
   protected GLUtessBeginProc TessBegin => type => (mPrimType, mnVerts) = (type, mnTriangles = 0);
   /// <summary>Called to set the edge flag before outputting a vertex</summary>
   protected GLUtessEdgeFlagProc TessEdgeFlag => (byte flag) => miNextEdge = flag != 0;
   /// <summary>Callback used to report errors during tessellation (this is very very rare)</summary>
   protected GLUtessErrorProc TessError => (int error) => mError = $"Tesselation error: {error}";
   #endregion

   #region Private/Protected data ------------------------------------
   /// <summary>If set, then we are generating edge-flag bits</summary>
   protected bool miUseEdgeFlags;
   /// <summary>Values shared between TessBegin and TessVertex callbacks</summary>
   protected int mnVerts, mnTriangles;
   /// <summary>The current primitive type the tessellator is outputing.</summary>
   protected EPrimitive mPrimType;
   /// <summary>Is the next edge (to be output to TessVertex) a boundary edge?</summary>
   protected bool miNextEdge;
   /// <summary>The list of edge flags; each value here is 0 or 0x40000000</summary>
   protected readonly int[] miEdge = new int[3];
   /// <summary>The list of indices for the triangle</summary>
   protected readonly int[] mIdx = new int[3];
   #endregion

   #region Nested types and constants --------------------------------
   /// <summary>This is the bit that is set when we return edge flags</summary>
   public const int EdgeBit = 0x40000000;
   #endregion
}

/// <summary>Given a list of points as inner and outer polygon contours, generate a tesselation in 2D</summary>
class GLTess2DImp (List<Point2> pts, IReadOnlyList<int> splits) : GLTessImp {
   #region Implementation --------------------------------------------
   public unsafe override List<int> Process () {
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
   #endregion

   #region GLU callbacks ---------------------------------------------
   GLUtessVertexDataProc TessVertex => (nint data, nint another) => {
      miEdge[mnVerts] = miNextEdge ? EdgeBit : 0;
      mIdx[mnVerts] = (int)data;
      if (++mnVerts == 3) {
         // If we got 3 vertices, we can output a triangle
         if (miUseEdgeFlags)
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
         mResult.AddRange (stackalloc[] { n1, n2, n3 });
      }
   };

   // Called when a new vertex needs to be generated at an intersection point.
   // The paramter coords contains the location of the new point to be added to the list
   // of points. We must return the index of the newly added point into *pout. 
   unsafe GLUtessCombineProc TessCombine => (double* coords, void** d2, float* d3, int* pout) => {
      *pout = NewVertex (coords[0], coords[1], coords[2]);

      // This is called by the combine-callback when it needs to generate new points
      int NewVertex (double x, double y, double _) {
         mPts.Add (new (x, y));
         return mPts.Count - 1;
      }
   };
   #endregion

   #region Data ------------------------------------------------------
   // Polygon to tessellate.
   readonly List<Point2> mPts = pts;
   // Polygon outer/inner contours
   readonly IReadOnlyList<int> mSplit = splits;
   // The GLU tessellator object
   [ThreadStatic]
   static HTesselator sTess;
   #endregion
}
