// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ LuxDraw.cs
// ║║║║╬║╔╣║ Draw-related properties and methods of the Lux class
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

[Flags]
public enum ELuxAttr {
   None = 0,
   Color = 1 << 0, LineWidth = 1 << 1, LineType = 1 << 2, LTScale = 1 << 3, 
   Xfm = 1 << 4, PointSize = 1 << 5, TypeFace = 1 << 6, 
}

#region class Lux ----------------------------------------------------------------------------------
/// <summary>The public interface to the Lux renderer</summary>
public static partial class Lux {
   // Properties ---------------------------------------------------------------
   /// <summary>The current drawing color (default = white)</summary>
   public static Color4 Color { 
      get => mColor;
      set {
         if (mColor.EQ (value)) return;
         if (Set (ELuxAttr.Color)) mColors.Push (mColor);
         mColor = value; Rung++;
      }
   }
   static Color4 mColor;
   static Stack<Color4> mColors = [];

   /// <summary>The current line-width, in device-independent pixels</summary>
   public static float LineWidth { 
      get => mLineWidth;
      set {
         if (mLineWidth.EQ (value)) return;
         if (Set (ELuxAttr.LineWidth)) mLineWidths.Push (mLineWidth); 
         mLineWidth = value; Rung++;
      } 
   }
   static float mLineWidth;
   static Stack<float> mLineWidths = [];

   /// <summary>The current line-type</summary>
   public static ELineType LineType {
      get => mLineType;
      set {
         if (mLineType == value) return;
         if (Set (ELuxAttr.LineType)) mLineTypes.Push (mLineType); 
         mLineType = value; Rung++;
      }
   }
   static ELineType mLineType;
   static Stack<ELineType> mLineTypes = [];

   /// <summary>The current line-type scaling</summary>
   public static float LTScale {
      get => mLTScale;
      set {
         if (mLTScale.EQ (value)) return;
         if (Set (ELuxAttr.LTScale)) mLTScales.Push (mLTScale);
         mLTScale = value; Rung++;
      }
   }
   static float mLTScale = 100;
   static Stack<float> mLTScales = [];

   /// <summary>The diameter of a point, in device-independent pixels</summary>
   public static float PointSize { 
      get => mPointSize;
      set {
         if (mPointSize.EQ (value)) return;
         if (Set (ELuxAttr.PointSize)) mPointSizes.Push (mPointSize);
         mPointSize = value; Rung++;
      }
   }
   static float mPointSize;
   static Stack<float> mPointSizes = [];

   /// <summary>The typeface to use for drawing</summary>
   public static TypeFace? TypeFace {
      get => mTypeface;
      set {
         if (ReferenceEquals (mTypeface, value)) return;
         if (Set (ELuxAttr.TypeFace)) mTypefaces.Push (mTypeface);
         mTypeface = value; Rung++;
      }
   }
   static TypeFace? mTypeface;
   static Stack<TypeFace?> mTypefaces = [];

   /// <summary>Viewport scale (convert viewport pixels to GL clip coordinates -1 .. +1)</summary>
   public static Vec2F VPScale { 
      get => mVPScale;
      set { if (Lib.Set (ref mVPScale, value)) Rung++; }
   }
   static Vec2F mVPScale;

   public static VNode? VNode => mVNode;
   static VNode? mVNode;

   /// <summary>The index of the current transform in use</summary>
   /// This is an index into Scene.Xfms[] 
   public static int IDXfm {
      get => mIDXfm;
      private set {
         if (mIDXfm == value) return;
         if (Set (ELuxAttr.Xfm)) mIDXfms.Push (mIDXfm);
         mIDXfm = value; Rung++;
      }
   }
   static int mIDXfm;
   static Stack<int> mIDXfms = [];

   public static Matrix3 Xfm {
      set {
         if (value.IsIdentity) return;
         var xe = new XfmEntry (Scene!.Xfms[IDXfm], value);
         IDXfm = Scene.Xfms.Count; 
         Scene.Xfms.Add (xe);
      }
   }

   // Methods ------------------------------------------------------------------
   /// <summary>Draws 2D lines in world coordinates, with Z = 0</summary>
   /// Every pair of Vec2F in the list creates one line, so with n points,
   /// n / 2 lines are drawn. The following Lux properties are used:
   /// - LineWidth : the width of the beziers, in device independent pixels
   /// - DrawColor : color of the lines being drawn
   public static void Beziers (ReadOnlySpan<Vec2F> pts)
      => Bezier2DShader.It.Draw (pts);

   /// <summary>This fills a set of closed paths (made of line segments) with the current Color</summary>
   /// The input to this function is a set of triangle-fans representing the closed paths. 
   /// pts[0] is, by convention, some arbitrary point in the 2D space that acts as the tip
   /// vertex of every triangle we are going to draw (the bases of these triangles being made
   /// up of each segment of each path contour). We recommend picking the midpoint of the 
   /// bounding rectangle of the paths (to minimize the number of pixels to be covered by 
   /// the algorithm).
   /// 
   /// Suppose we have a path with two contours a quad whose vertices are at [1,2,3,4] in the
   /// pts list, and a triangle whose vertices are at [5,6,7] in the pts list. We will create
   /// two triangle fans with pts[0] as the tip vertex and each segment of each of these contours
   /// as a base. Since we are using a restartable primitive (triangle-fan), we can use the 
   /// special value -1 to indicate that we are finished with one fan. So the indices list in 
   /// this example should be like: [0,1,2,3,4,1,-1, 0,5,6,7,5,-1]. Note that vertex 1 and 
   /// vertex 5 repeat to 'close' the contour and to draw the last triangle (between 0,4,1 and
   /// 0,7,5 respectively). 
   /// 
   /// The bound parameter is just the bounding rectangle of the pts list - we pass it in 
   /// as a parameter to avoid this function computing it (since it is very often known by the
   /// caller). We need this bound to figure out a minimal covering rectangle to apply the paint
   /// through, after the stencil is prepared. See the notes on TriFanStencilShader for more
   /// details on the algorithm. 
   public static void FillPath (ReadOnlySpan<Vec2F> pts, ReadOnlySpan<int> indices, Bound2 bound) {
      TriFanStencilShader.It.Draw (pts, indices);
      bound = bound.InflatedF (1.01);
      var (x0, x1) = bound.X; var (y0, y1) = bound.Y;
      TriFanCoverShader.It.Draw ([new (x0, y0), new (x1, y0), new (x1, y1), new (x0, y1)]);
   }

   /// <summary>Draws 2D lines in world coordinates, with Z = 0</summary>
   /// Every pair of Vec2F in the list creates one line, so with n points,
   /// n / 2 lines are drawn. The following Lux properties are used:
   /// - LineWidth : the width of the beziers, in device independent pixels
   /// - DrawColor : color of the lines being drawn
   public static void Lines (ReadOnlySpan<Vec2F> pts) {
      if (LineType == ELineType.Solid) Line2DShader.It.Draw (pts);
      else DashLine2DShader.It.Draw (pts);
   }

   /// <summary>Draws a CMesh using one of the shade-modes</summary>
   /// The shade modes are
   ///   0 - Flat shading
   ///   1 - Gourad shading
   ///   2 - Phong shading
   /// (This is primarily for learning purposes. Later we will remove the other shade
   /// modes, and use only Phong shading)
   public static void Mesh (CMesh mesh, EShadeMode shadeMode) {
      CMesh.Node[] nodes = mesh.Vertex.AsArray ();
      int[] tris = mesh.Triangle.AsArray (), wires = mesh.Wire.AsArray ();
      switch (shadeMode) {
         case EShadeMode.Flat: FlatFacetShader.It.Draw (nodes, tris); break;
         case EShadeMode.Gourad: GouradShader.It.Draw (nodes, tris); break;
         case EShadeMode.Glass: GlassShader.It.Draw (nodes, tris); break;
         default: PhongShader.It.Draw (nodes, tris); break;
      }
      switch (shadeMode) {
         case EShadeMode.Glass: GlassLineShader.It.Draw (nodes, wires); break;
         default: BlackLineShader.It.Draw (nodes, wires); break;
      }
   }

   /// <summary>Draws 2D points in world coordinates, with Z = 0</summary>
   /// The following Lux properties are used:
   /// - PointSize : the diameter of the points, in device independent pixels
   /// - DrawColor : color of the points being drawn
   public static void Points (ReadOnlySpan<Vec2F> pts)
      => Point2DShader.It.Draw (pts);

   /// <summary>Draws a Poly object in world coordinates, with Z = 0</summary>
   public static void Poly (Poly p) {
      mPolys.Clear (); mPolys.Add (p);
      Polys (mPolys.AsSpan ());
   }
   static List<Poly> mPolys = [];
   static List<Vec2F> mLines = [], mBeziers = [];

   /// <summary>Draw multiple Polys in world coordinates, with Z = 0</summary>
   public static void Polys (ReadOnlySpan<Poly> polys) {
      mLines.Clear (); mBeziers.Clear ();
      foreach (var p in polys) {
         foreach (var seg in p.Segs) {
            if (seg.IsArc) seg.ToBeziers (mBeziers);
            else { mLines.Add (seg.A); mLines.Add (seg.B); }
         }
      }
      Lines (mLines.AsSpan ());
      Beziers (mBeziers.AsSpan ());
   }

   /// <summary>Draws 2D quads in world coordinates, with Z = 0</summary>
   /// The quads are drawn with smoothed (anti-aliased) edges. 
   /// The following Lux properties are used:
   /// - DrawColor : color of the triangles being drawn
   public static void Quads (ReadOnlySpan<Vec2F> a) {
      Quad2DShader.It.Draw (a);
      for (int i = 0; i < a.Length; i += 4)
         Line2DShader.It.Draw ([a[i], a[i + 1], a[i + 1], a[i + 2], a[i + 2], a[i + 3], a[i + 3], a[i]]);
   }

   /// <summary>Draws 2D triangles in world coordinates, with Z = 0</summary>
   /// The triangles are drawn with smoothed (anti-aliased) edges. 
   /// The following Lux properties are used:
   /// - DrawColor : color of the triangles being drawn
   public static void Triangles (ReadOnlySpan<Vec2F> a) {
      Triangle2DShader.It.Draw (a);
      for (int i = 0; i < a.Length; i += 3)
         Line2DShader.It.Draw ([a[i], a[i + 1], a[i + 1], a[i + 2], a[i + 2], a[i]]);
   }

   /// <summary>Draws text at specified pixel-coordinates (uses the current TypeFace and DrawColor)</summary>
   public static void Text (ReadOnlySpan<char> text, Vec2S pos) {
      if (text.IsWhiteSpace ()) return;
      Span<TextPxShader.Args> cells = stackalloc TextPxShader.Args[text.Length];
      int x = pos.X, y = pos.Y, n = 0; uint idx0 = 0;
      var face = TypeFace ?? TypeFace.Default;
      foreach (var ch in text) {
         uint idx1 = face.GetGlyphIndex (ch);
         var metric = face.GetMetrics (idx1);
         int kern = face.GetKerning (idx0, idx1);
         short xChar = (short)(x + metric.LeftBearing + kern), yChar = (short)(y + metric.TopBearing);
         var vec = new Vec4S (xChar, yChar - metric.Rows, xChar + metric.Columns, yChar);
         cells[n++] = new (vec, metric.TexOffset);
         x += metric.Advance + kern;
         idx0 = idx1;
      }
      TextPxShader.It.Draw (cells);
   }
}
#endregion
