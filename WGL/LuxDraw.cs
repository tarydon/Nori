// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ LuxDraw.cs
// ║║║║╬║╔╣║ Draw-related properties and methods of the Lux class
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Security.Cryptography;

namespace Nori;

[Flags]
public enum ELuxAttr {
   None = 0,
   Color = 1 << 0, LineWidth = 1 << 1, LineType = 1 << 2, LTScale = 1 << 3,
   Xfm = 1 << 4, PointSize = 1 << 5, TypeFace = 1 << 6, ZLevel = 1 << 7,
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

   /// <summary>The DPI scaling (how many pixels to one logical pixel)</summary>
   public static float DPIScale { get => mDPIScale; set => mDPIScale = value; }
   static float mDPIScale = 1;

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

   /// <summary>The current line-type scaling (this is in device-independent pixels)</summary>
   public static float LTScale {
      get => mLTScale;
      set {
         if (mLTScale.EQ (value)) return;
         if (Set (ELuxAttr.LTScale)) mLTScales.Push (mLTScale);
         mLTScale = value; Rung++;
      }
   }
   static float mLTScale;
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

   /// <summary>The Z-Level of rendering (small values are drawn earlier)</summary>
   public static int ZLevel {
      get => mZLevel;
      set {
         if (mZLevel.Equals (value)) return;
         if (Set (ELuxAttr.ZLevel)) mZLevels.Push (mZLevel);
         mZLevel = value; Rung++;
      }
   }
   static int mZLevel;
   static Stack<int> mZLevels = [];

   // Methods ------------------------------------------------------------------
   /// <summary>Draws beziers in world coordinates, with Z = 0</summary>
   /// Every set of 4 points in the list creates one bezier curve so n / 4
   /// beziers are drawn. The following Lux properties are used:
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
      // We do this hacking of ZLevels because we have some very specific sequencing requirements
      // for the stencil RBatch and the cover RBatch. They should be drawn one immediately after the
      // other. So we use unique ZLevels for each stencil+cover RBatch pair starting with
      // -19999,-19998, etc. This is because the stencil that is created by the first batch must
      // be used immediately by the cover step, and definitely before any other stencil batch is
      // drawn. Note that the cover shader not only uses the stencil, but also zeroes it out so it
      // is ready for the next application of the stencil shader
      ZLevel = ++mcFillPaths - 20000;
      TriFanStencilShader.It.Draw (pts, indices);
      bound = bound.InflatedF (1.01);
      var (x0, x1) = bound.X; var (y0, y1) = bound.Y;
      ZLevel = ++mcFillPaths - 20000;
      TriFanCoverShader.It.Draw ([new (x0, y0), new (x1, y0), new (x1, y1), new (x0, y1)]);
   }
   // This gets reset to 0 at the start of every frame
   static int mcFillPaths = 0;

   /// <summary>Draws 2D lines in world coordinates, with Z = 0</summary>
   /// Every pair of Vec2F in the list creates one line, so with n points,
   /// n / 2 lines are drawn. The following Lux properties are used:
   /// - LineWidth : the width of the beziers, in device independent pixels
   /// - DrawColor : color of the lines being drawn
   public static void Lines (ReadOnlySpan<Vec2F> pts) {
      if (LineType == ELineType.Continuous) Line2DShader.It.Draw (pts);
      else DashLine2DShader.It.Draw (pts);
   }

   /// <summary>Draws a 2D line-strip (an open polyline made up of the given set of points)</summary>
   public static void LineStrip (IReadOnlyList<Point2> pts) {
      mBuf.Clear (); mBuf.Add (pts[0]);
      for (int i = 1; i < pts.Count- 1; i++) { mBuf.Add (pts[i]); mBuf.Add (pts[i]); }
      mBuf.Add (pts[^1]);
      Lines (mBuf.AsSpan ());
   }
   static List<Vec2F> mBuf = [];

   /// <summary>Draws 3D lines</summary>
   /// The following Lux properties are used:
   /// - Xfm: current transformation matrix
   /// - LineWidth: the width of the line
   /// - Color: draw color
   public static void Lines (ReadOnlySpan<Vec3F> pts)
      => Line3DShader.It.Draw (pts);

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

      /*
      mPoints.Clear ();
      foreach (var p in polys)
         mPoints.AddRange (p.Pts.Select (a => (Vec2F)a));
      Points (mPoints.AsSpan ()); */
   }
   static List<Vec2F> mPoints = [];

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

   /// <summary>Draws text positioned at a given point in world coordinates</summary>
   /// The position _pos_ specifies a point in world coordinates (with Z = 0) where
   /// the text is positioned. Based on the alignment parameter, there are 12 different
   /// 'reference points' on the text which get mapped to this position pos. See
   /// file://n:/tdata/lux/text2d.png for an example of all the 12 alignments.
   ///
   /// The following Lux properties are used by this shader:
   /// - Xfm       : current transformation matrix
   /// - DrawColor : color of the text being drawn
   /// - TypeFace  : font, style, size of the text being drawn
   public static void Text2D (ReadOnlySpan<char> text, Vec2F pos, ETextAlign align, Vec2S offset) {
      if (text.IsWhiteSpace ()) return;
      // First, get the basic cells as we would for a TextPx shader, assuming the text
      // is starting at a position of (0,0)
      var face = TypeFace ?? TypeFace.Default;
      Span<TextPxShader.Args> cells = stackalloc TextPxShader.Args[text.Length];
      int x = GetTextCells (text, offset, cells);

      // If we are going to draw the text with a 'BaseLeft' alignment, then the cells
      // we obtained are already correct (since the transformed coordinates of the _pos_
      // parameter from above will get added to each cell position). However, if we want
      // other alignments like TopRight or MidCenter, we need to adjust all these cells.
      // Let's compute the dx and dy for that adjustment here:
      var cellM = face.Measure ("M", true);
      short dx = 0, dy = 0, nAlign = (short)(align - 1);
      Span<Text2DShader.Args> output = stackalloc Text2DShader.Args[text.Length];

      // First compute the dx needed based on the horizontal alignment (left alignment
      // is the default value of 0 in the case below)
      switch (nAlign % 3) {
         case 1: dx = (short)(-x / 2); break;   // 'Mid' alignment (shift left by half the width)
         case 2: dx = (short)-x; break;         // 'Right' alignment (shift left by the width)
      }
      // Then, compute the dy needed based on the vertical alignment (base alignment is the
      // default value of 3 in the case below)
      switch (nAlign / 3) {
         case 0: dy = (short)(-cellM.Top); break;        // Top alignment
         case 1: dy = (short)(-cellM.Top / 2); break;    // Center alignment
         case 2: dy = (short)face.Descender; break;      // Bottom alignment (based on face bounding box)
      }
      for (int i = 0; i < cells.Length; i++) {
         ref TextPxShader.Args input = ref cells[i];
         var v = input.Cell;
         output[i] = new (pos, new (v.X + dx, v.Y + dy, v.Z + dx, v.W + dy), input.TexOffset);
      }
      Text2DShader.It.Draw (output);
   }

   /// <summary>Draws text at specified pixel-coordinates (uses the current TypeFace and DrawColor)</summary>
   /// The pixel (0,0) is the bottom left of the screen, and pixel coordinates increase going
   /// to the right, or upwards. The following Lux properties are used by this shader:
   ///
   /// The following Lux properties are used by this shader:
   /// - DrawColor : color of the text being drawn
   /// - TypeFace  : font, style, size of the text being drawn
   public static void TextPx (ReadOnlySpan<char> text, Vec2S pos) {
      if (text.IsWhiteSpace ()) return;
      Span<TextPxShader.Args> cells = stackalloc TextPxShader.Args[text.Length];
      GetTextCells (text, pos, cells);
      TextPxShader.It.Draw (cells);
   }

   // Implementation -----------------------------------------------------------
   // Given some text, this converts it into a series of TextPxShader.Args (cells) that
   // each contain one 'vertex' to draw one character. The first cell starts with its bottom
   // left corner at the specified pos, and subsequent cells advance left to right (based on the
   // width of each character, and the kerning adjustment between successive characters).
   // The output from this can be directly used by the TextPx shader, or it can be used to
   // compute the cells for the Text2D shader (which have some additional information about
   // the position in world coordinates where the text should start).
   //
   // This routine fills up the cells output array with the vertices we generate. It also returns
   // the final X position after all the rendering (which is useful if we want to do a right-aligned
   // text positioning)
   static int GetTextCells (ReadOnlySpan<char> text, Vec2S pos, Span<TextPxShader.Args> cells) {
      var face = TypeFace ?? TypeFace.Default;
      int x = pos.X, y = pos.Y, n = 0;  uint idx0 = 0;
      foreach (var ch in text) {
         uint idx1 = face.GetGlyphIndex (ch);         // Get glyph index for the character
         var metric = face.GetMetrics (idx1);         // Get the metrics for this character
         int kern = face.GetKerning (idx0, idx1);     // Then, the kerning adjustment between the previous character and this one
         short xChar = (short)(x + metric.LeftBearing + kern), yChar = (short)(y + metric.TopBearing);
         // We are using a Vec4S to store a 'cell' in pixels where the character is to be drawn.
         // It has lower left corner at (X,Y) of the Vec4S and the top right corner at (Z,W) of the
         // Vec4S. The other bit of data is the offset into the font texture for this glyph
         // (a single number suffices since we store the texture in a linear-unpacked format,
         // not as a 2D bitmap).
         var vec = new Vec4S (xChar, yChar - metric.Rows, xChar + metric.Columns, yChar);
         cells[n++] = new (vec, metric.TexOffset);
         x += metric.Advance + kern;
         idx0 = idx1;
      }
      return x;
   }
}
#endregion
