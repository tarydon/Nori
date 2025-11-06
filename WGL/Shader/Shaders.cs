// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Shaders.cs
// ║║║║╬║╔╣║ Final Shader classes, all inherited from Shader<Vertex, UBlock>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Bezier2DShader -----------------------------------------------------------------------
/// <summary>A specialization of Seg2DShader, used to draw curved segs (using beziers)</summary>
[Singleton]
partial class Bezier2DShader () : Seg2DShader (ShaderImp.Bezier2D) { }
#endregion

#region class BlackLineShader ----------------------------------------------------------------------
/// <summary>Variant of StencilLineShader that draws solid black lines in 3D (anti-aliased)</summary>
[Singleton]
partial class BlackLineShader () : StencilLineShader (ShaderImp.BlackLine) { }
#endregion

#region class DashLine2DShader ---------------------------------------------------------------------
/// <summary>Shader used to draw lines with a dash pattern (dashed / dotted / centerline etc)</summary>
[Singleton]
partial class DashLine2DShader : Shader<Vec2F, DashLine2DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public DashLine2DShader () : base (ShaderImp.DashLine2D) => Bind ();
   int muVPScale = 0, muXfm = 0, muLineWidth = 0, muLineType = 0, muLTScale = 0, muDrawColor = 0, muLTypeTexture = 0;

   protected override void ApplyUniformsImp (ref readonly Settings a) {
      Pgm.Set (muXfm, ref Lux.Scene!.Xfms[a.IDXfm].Xfm);
      float fLType = ((int)a.LineType + 0.5f) / 10.0f;
      Pgm.Set (muLineWidth, a.LineWidth * Lux.DPIScale).Set (muLineType, fLType);
      Pgm.Set (muDrawColor, a.Color).Set (muLTScale, a.LTScale * Lux.DPIScale);
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm.CompareTo (b.IDXfm); if (n != 0) return n;
      n = a.LineType.CompareTo (b.LineType); if (n != 0) return n;
      n = (int)(a.Color.Value - b.Color.Value); if (n != 0) return n;
      n = a.LTScale.CompareTo (b.LTScale); if (n != 0) return n;
      return a.LineWidth.CompareTo (b.LineWidth);
   }

   protected override void SetConstantsImp () {
      Pgm.Set (muVPScale, Lux.VPScale).Set (muLTypeTexture, 1);
   }

   protected override Settings SnapUniformsImp ()
      => new (Lux.IDXfm, Lux.LineWidth, Lux.LineType, Lux.LTScale, Lux.Color);

   public readonly record struct Settings (int IDXfm, float LineWidth, ELineType LineType, float LTScale, Color4 Color);
}
#endregion

#region class FacetShader --------------------------------------------------------------------------
/// <summary>Base class for various types of 3D shader (Flat / Gourad / Phong)</summary>
abstract class FacetShader : Shader<Mesh3.Node, FacetShader.Settings> {
   // Constructors -------------------------------------------------------------
   protected FacetShader (ShaderImp imp) : base (imp) => Bind ();

   // Overrides ----------------------------------------------------------------
   protected unsafe override void ApplyUniformsImp (ref readonly Settings a) {
      //ref Mat4F xfm = ref Lux.Scene!.Xfms[a.IDXfm].Xfm; TODO check if this works
      //fixed (float* f = &xfm.M11) Pgm.Set (muXfm, f);
      Pgm.Set (muXfm, ref Lux.Scene!.Xfms[a.IDXfm].Xfm);
      Pgm.Set (muNormalXfm, ref Lux.Scene!.Xfms[a.IDXfm].NormalXfm);
      Pgm.Set (muDrawColor, a.Color);
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () { }
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.Color);

   // Private data -------------------------------------------------------------
   protected int muXfm = 0, muNormalXfm = 0, muDrawColor = 0;

   public readonly record struct Settings (int IDXfm, Color4 Color);
}
#endregion

#region class FlatFacetShader ----------------------------------------------------------------------
/// <summary>3D shader using flat shading (no interpolation)</summary>
[Singleton]
partial class FlatFacetShader () : FacetShader (ShaderImp.FlatFacet) { }
#endregion

#region class GlassShader --------------------------------------------------------------------------
/// <summary>3D shader that simulates translucency using stippling</summary>
[Singleton]
partial class GlassShader () : FacetShader (ShaderImp.Glass) { }
#endregion

#region class GlassLineShader ----------------------------------------------------------------------
/// <summary>Variant of StencilLineShader that draws stippled lines (50% transparency)</summary>
[Singleton]
partial class GlassLineShader () : StencilLineShader (ShaderImp.GlassLine) { }
#endregion

#region class GouradShader -------------------------------------------------------------------------
/// <summary>3D shader using the Gourad shader model (color interpolation)</summary>
[Singleton]
partial class GouradShader () : FacetShader (ShaderImp.Gourad) { }
#endregion

#region class Line2DShader -------------------------------------------------------------------------
/// <summary>A specialization of Seg2DShader, used to draw linear segs</summary>
[Singleton]
partial class Line2DShader () : Seg2DShader (ShaderImp.Line2D) { }
#endregion

#region class Line3DShader -------------------------------------------------------------------------
/// <summary>Draw lines in 3D space</summary>
[Singleton]
partial class Line3DShader : Shader<Vec3F, Seg2DShader.Settings> {
   Line3DShader () : base (ShaderImp.Line3D) => Bind ();
   int muVPScale = 0, muXfm = 0, muLineWidth = 0, muDrawColor = 0;

   protected override void ApplyUniformsImp (ref readonly Seg2DShader.Settings a) {
      Pgm.Set (muXfm, ref Lux.Scene!.Xfms[a.IDXfm].Xfm);
      Pgm.Set (muLineWidth, a.LineWidth * Lux.DPIScale).Set (muDrawColor, a.Color);
   }

   protected override int OrderUniformsImp (ref readonly Seg2DShader.Settings a, ref readonly Seg2DShader.Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      n = a.LineWidth.CompareTo (b.LineWidth); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => Pgm.Set (muVPScale, Lux.VPScale);
   protected override Seg2DShader.Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.LineWidth, Lux.Color);
}
#endregion

#region class PhongShader --------------------------------------------------------------------------
/// <summary>3D shader using the Phong shading model (normal vector interpolation)</summary>
[Singleton]
partial class PhongShader () : FacetShader (ShaderImp.Phong) { }
#endregion

#region class PickShader ---------------------------------------------------------------------------
/// <summary>3D shader used during picking - replaces actual colors with VNode Ids</summary>
[Singleton]
partial class PickShader () : FacetShader (ShaderImp.Pick) {
   public void ApplyUniforms (int idXfm, Color4 color) {
      Pgm.Set (muXfm, ref Lux.Scene!.Xfms[idXfm].Xfm);
      Pgm.Set (muNormalXfm, ref Lux.Scene!.Xfms[idXfm].NormalXfm);
      Pgm.Set (muDrawColor, color);
   }
}
#endregion

#region class Point2DShader ------------------------------------------------------------------------
/// <summary>Shader used to draw points</summary>
[Singleton]
partial class Point2DShader : Shader<Vec2F, Point2DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public Point2DShader () : base (ShaderImp.Point2D) => Bind ();
   int muVPScale = 0, muXfm = 0, muPointSize = 0, muDrawColor = 0;

   // Overrides ----------------------------------------------------------------
   protected override void ApplyUniformsImp (ref readonly Settings a) {
      Pgm.Set (muXfm, ref Lux.Scene!.Xfms[a.IDXfm].Xfm);
      Pgm.Set (muPointSize, a.PointSize * Lux.DPIScale).Set (muDrawColor, a.Color);
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      n = a.PointSize.CompareTo (b.PointSize); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => Pgm.Set (muVPScale, Lux.VPScale);
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.PointSize, Lux.Color);

   // Nested types -------------------------------------------------------------
   public readonly record struct Settings (int IDXfm, float PointSize, Color4 Color);
}
#endregion

#region class Quad2DShader -------------------------------------------------------------------------
/// <summary>Shader to draw simple quads in 2D (specified in world space, no anti-aliasing)</summary>
[Singleton]
partial class Quad2DShader () : TriQuad2DShader (ShaderImp.Quad2D) { }
#endregion

#region class Seg2DShader --------------------------------------------------------------------------
/// <summary>Base class for the Line2DShader and Bezier2DShader</summary>
class Seg2DShader : Shader<Vec2F, Seg2DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public Seg2DShader (ShaderImp shader) : base (shader) => Bind ();
   int muVPScale = 0, muXfm = 0, muLineWidth = 0, muDrawColor = 0;

   // Overrides ----------------------------------------------------------------
   protected unsafe override void ApplyUniformsImp (ref readonly Settings a) {
      Pgm.Set (muXfm, ref Lux.Scene!.Xfms[a.IDXfm].Xfm);
      Pgm.Set (muLineWidth, a.LineWidth * Lux.DPIScale).Set (muDrawColor, a.Color);
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      n = a.LineWidth.CompareTo (b.LineWidth); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => Pgm.Set (muVPScale, Lux.VPScale);
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.LineWidth, Lux.Color);

   // Nested types -------------------------------------------------------------
   public readonly record struct Settings (int IDXfm, float LineWidth, Color4 Color);
}
#endregion

#region class StencilLineShader --------------------------------------------------------------------
/// <summary>Shader used to draw the black stencil lines for a mesh</summary>
abstract class StencilLineShader : Shader<Mesh3.Node, StencilLineShader.Settings> {
   // Constructor --------------------------------------------------------------
   public StencilLineShader (ShaderImp imp) : base (imp) => Bind ();
   int muXfm = 0, muVPScale = 0, muLineWidth = 0, muDrawColor = 0;

   // Overrides ----------------------------------------------------------------
   protected override void ApplyUniformsImp (ref readonly Settings a) {
      Pgm.Set (muXfm, ref Lux.Scene!.Xfms[a.IDXfm].Xfm);
      Pgm.Set (muLineWidth, a.LineWidth * Lux.DPIScale).Set (muDrawColor, a.Color);
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      n = a.LineWidth.CompareTo (b.LineWidth); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => Pgm.Set (muVPScale, Lux.VPScale);
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.LineWidth, Color4.Black);

   // Nested types -------------------------------------------------------------
   public readonly record struct Settings (int IDXfm, float LineWidth, Color4 Color);
}
#endregion

#region class TextPxShader -------------------------------------------------------------------------
/// <summary>Draws text defined in pixel coordinates</summary>
[Singleton]
partial class TextPxShader : Shader<TextPxShader.Args, TextPxShader.Settings> {
   // Constructor --------------------------------------------------------------
   public TextPxShader () : base (ShaderImp.TextPx) => Bind ();
   int muVPScale = 0, muDrawColor = 0, muFontTexture = 0;

   // Overrides ----------------------------------------------------------------
   protected override void ApplyUniformsImp (ref readonly Settings a) {
      GLState.TypeFace = a.Face;
      Pgm.Set (muDrawColor, a.Color);
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.Face.UID - b.Face.UID; if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => Pgm.Set (muVPScale, Lux.VPScale).Set (muFontTexture, 0);
   protected override Settings SnapUniformsImp () => new (Lux.Color, Lux.TypeFace ?? TypeFace.Default);

   // Nested types -------------------------------------------------------------
   [StructLayout (LayoutKind.Sequential)]
   public readonly record struct Args (Vec4S Cell, int TexOffset);
   public readonly record struct Settings (Color4 Color, TypeFace Face);
}
#endregion

#region class Text2DShader -------------------------------------------------------------------------
/// <summary>Draws the text defined in world coordinates</summary>
[Singleton]
partial class Text2DShader : Shader<Text2DShader.Args, Text2DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public Text2DShader () : base (ShaderImp.Text2D) => Bind ();
   int muXfm = 0, muVPScale = 0, muDrawColor = 0, muFontTexture = 0;

   // Overrides ----------------------------------------------------------------
   protected override void ApplyUniformsImp (ref readonly Settings a) {
      GLState.TypeFace = a.Face;
      Pgm.Set (muXfm, ref Lux.Scene!.Xfms[a.IDXfm].Xfm);
      Pgm.Set (muDrawColor, a.Color);
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.Face.UID - b.Face.UID; if (n != 0) return n;
      n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => Pgm.Set (muVPScale, Lux.VPScale).Set (muFontTexture, 0);
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.Color, Lux.TypeFace ?? TypeFace.Default);

   // Nested types -------------------------------------------------------------
   [StructLayout (LayoutKind.Sequential)]
   public readonly record struct Args (Vec2F Pos, Vec4S Cell, int TexOffset);
   public readonly record struct Settings (int IDXfm, Color4 Color, TypeFace Face);
}
#endregion

#region class Triangle2DShader ---------------------------------------------------------------------
/// <summary>Shader to draw simple triangles in 2D (specified in world space, no anti-aliasing)</summary>
[Singleton]
partial class Triangle2DShader () : TriQuad2DShader (ShaderImp.Triangle2D) { }
#endregion

#region class TriFanStencilShader ------------------------------------------------------------------
/// <summary>Shader used to implement stage 1 of the stencil-then-cover algorithm</summary>
/// This is a simple algorithm that uses the stencil buffer to fill the interior of a set of
/// closed paths. The paths are defined as contours, each consisting of a number of line segments.
/// The orientation (winding) of these contours is not important, nor are there any constraints on
/// whether the can self-intersect or cross each other.
///
/// This animation https://www.ekioh.com/devblog/gpu-filling-vector-paths/ shows very clearly
/// how this algorithm works. basically we pick an arbitrary point on the scene and draw a triangle
/// from that point using each 'segment' on the path as a base. For all the pixels lying within
/// that triangle, we invert bit 0 of the stencil buffer. When we are finished, all the points lying
/// within a contour (that is, an odd number of 'crossings' from the arbitrary point) will have
/// their stencil bit 0 set, and the rest will not.
///
/// This is done by the TriFanStencilShader. It requires as input a triangle fan, and uses
/// indexed drawing to minimize the amount of data transfered. For example, suppose the path to
/// be filled is one quad (with indices [1,2,3,4]) and one triangle (with indices [5,6,7]). We
/// use a convention that Pts[0] is the 'arbitray point' on the screen. Then, the Indices input
/// to this shader will look like this. Remember we are drawing triangle fans, and that -1 is the
/// 'start new primitive' marker: [0,1,2,3,4,1,-1, 0,5,6,7,5,-1]. As you can see, that will draw
/// two triangle fans, one with 4 triangles and one with 3. Every triangle drawn has Pts[0] as
/// the tip vertex. The caller must prepare the data in this format, and we use this so that the
/// entire stencil can be drawn with a single draw call.
///
/// During the running of this shader, we set the StencilFunc to GL_NEVER to avoid updating
/// any of the color-buffer pixels - we are only drawing into the stencil buffer at this point.
/// Subsequently, the TriFanCoverShader uses this stencil to fill in the pixels where stencil
/// bit 0 is set
[Singleton]
partial class TriFanStencilShader () : TriQuad2DShader (ShaderImp.TriFanStencil) { }
#endregion

#region class TriFanCoverShader --------------------------------------------------------------------
/// <summary>Shader used to implement stage 2 of the stencil-then-cover algorithm</summary>
/// This works after the TriFanStencilShader has updated the stencil buffer. This shader uses
/// bit 0 of the stencil buffer, and wherever that is set, it simply applies the Lux.Color to that
/// pixel. For this to work, the shader needs as input a triangle fan that fully covers the
/// paths in question. The implementation in Lux.FillPoly uses the bounding box of the set of
/// paths and creates a triangle fan with 2 triangles that apply paint into this bounding box.
[Singleton]
partial class TriFanCoverShader () : TriQuad2DShader (ShaderImp.TriFanCover) { }
#endregion

#region class TriQuad2DShader ----------------------------------------------------------------------
/// <summary>TriQuad2DShader is the base class for Triangle2DShader and Quad2DShader</summary>
abstract class TriQuad2DShader : Shader<Vec2F, TriQuad2DShader.Settings> {
   // Constructors -------------------------------------------------------------
   protected TriQuad2DShader (ShaderImp imp) : base (imp) => Bind ();
   int muXfm = 0, muDrawColor = 0;

   // Overrides ----------------------------------------------------------------
   protected override void ApplyUniformsImp (ref readonly Settings a) {
      Pgm.Set (muXfm, ref Lux.Scene!.Xfms[a.IDXfm].Xfm);
      Pgm.Set (muDrawColor, a.Color);
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () { }
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.Color);

   public readonly record struct Settings (int IDXfm, Color4 Color);
}
#endregion
