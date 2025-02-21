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

#region class FacetShader --------------------------------------------------------------------------
/// <summary>Base class for various types of 3D shader (Flat / Gourad / Phong)</summary>
abstract class FacetShader : Shader<CMesh.Node, Color4> {
   // Constructors -------------------------------------------------------------
   protected FacetShader (ShaderImp imp) : base (imp) => Bind ();

   // Overrides ----------------------------------------------------------------
   protected override void ApplyUniformsImp (ref readonly Color4 settings) => Pgm.Set (muDrawColor, settings);
   protected override int OrderUniformsImp (ref readonly Color4 a, ref readonly Color4 b) => (int)(a.Value - b.Value);
   protected override void SetConstantsImp () => Pgm.Set (muXfm, Lux.Xfm).Set (muNormalXfm, Lux.NormalXfm);
   protected override Color4 SnapUniformsImp () => Lux.DrawColor;

   // Private data -------------------------------------------------------------
   int muXfm = 0, muNormalXfm = 0, muDrawColor = 0;
}
#endregion

#region class FlatFacetShader ----------------------------------------------------------------------
/// <summary>3D shader using flat shading (no interpolation)</summary>
[Singleton]
partial class FlatFacetShader () : FacetShader (ShaderImp.FlatFacet) { }
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

#region class PhongShader --------------------------------------------------------------------------
/// <summary>3D shader using the Phong shading model (normal vector interpolation)</summary>
[Singleton]
partial class PhongShader () : FacetShader (ShaderImp.Phong) { }
#endregion

#region class Point2DShader ------------------------------------------------------------------------
/// <summary>Shader used to draw points</summary>
[Singleton]
partial class Point2DShader : Shader<Vec2F, Point2DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public Point2DShader () : base (ShaderImp.Point2D) => Bind ();
   int muVPScale = 0, muXfm = 0, muPointSize = 0, muDrawColor = 0;

   // Overrides ----------------------------------------------------------------
   protected override void ApplyUniformsImp (ref readonly Settings a)
      => Pgm.Set (muPointSize, a.PointSize).Set (muDrawColor, a.Color);

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.PointSize.CompareTo (b.PointSize); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => Pgm.Set (muVPScale, Lux.VPScale).Set (muXfm, Lux.Xfm);
   protected override Settings SnapUniformsImp () => new (Lux.PointSize, Lux.DrawColor);

   // Nested types -------------------------------------------------------------
   public readonly record struct Settings (float PointSize, Color4 Color);
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
   protected override void ApplyUniformsImp (ref readonly Settings a)
      => Pgm.Set (muLineWidth, a.LineWidth).Set (muDrawColor, a.Color);

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.LineWidth.CompareTo (b.LineWidth); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => Pgm.Set (muVPScale, Lux.VPScale).Set (muXfm, Lux.Xfm);
   protected override Settings SnapUniformsImp () => new (Lux.LineWidth, Lux.DrawColor);

   // Nested types -------------------------------------------------------------
   public readonly record struct Settings (float LineWidth, Color4 Color);
}
#endregion

#region class DashLine2DShader ---------------------------------------------------------------------
/// <summary>Shader used to draw lines with a dash pattern (dashed / dotted / centerline etc)</summary>
[Singleton]
partial class DashLine2DShader : Shader<Vec2F, DashLine2DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public DashLine2DShader () : base (ShaderImp.DashLine2D) => Bind ();
   int muVPScale = 0, muXfm = 0, muLineWidth = 0, muLineType = 0, muLTScale = 0, muDrawColor = 0, muLTypeTexture = 0;

   protected override void ApplyUniformsImp (ref readonly Settings a) {
      float fLType = ((int)a.LineType + 0.5f) / 10.0f;
      Pgm.Set (muLineWidth, a.LineWidth).Set (muLineType, fLType).Set (muDrawColor, a.Color);
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.LineWidth.CompareTo (b.LineWidth); if (n != 0) return n;
      n = a.LineType.CompareTo (b.LineType); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () {
      Pgm.Set (muVPScale, Lux.VPScale).Set (muXfm, Lux.Xfm).Set (muLTypeTexture, 1).Set (muLTScale, Lux.LTScale);
   }

   protected override Settings SnapUniformsImp ()
      => new (Lux.LineWidth, Lux.LineType, Lux.DrawColor);

   public readonly record struct Settings (float LineWidth, ELineType LineType, Color4 Color);
}
#endregion

#region class StencilLineShader --------------------------------------------------------------------
/// <summary>Shader used to draw the black stencil lines for a mesh</summary>
[Singleton]
partial class StencilLineShader : Shader<CMesh.Node, StencilLineShader.Settings> {
   // Constructor --------------------------------------------------------------
   public StencilLineShader () : base (ShaderImp.StencilLine) => Bind ();
   int muXfm = 0, muVPScale = 0, muLineWidth = 0, muDrawColor = 0;

   // Overrides ----------------------------------------------------------------
   protected override void ApplyUniformsImp (ref readonly Settings settings)
      => Pgm.Set (muLineWidth, settings.LineWidth).Set (muDrawColor, settings.Color);

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.LineWidth.CompareTo (b.LineWidth); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => Pgm.Set (muXfm, Lux.Xfm).Set (muVPScale, Lux.VPScale);
   protected override Settings SnapUniformsImp () => new (3, Color4.Black);

   // Nested types -------------------------------------------------------------
   public readonly record struct Settings (float LineWidth, Color4 Color);
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
   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.Face.UID - b.Face.UID; if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void ApplyUniformsImp (ref readonly Settings settings) {
      GLState.TypeFace = settings.Face;
      Pgm.Set (muDrawColor, settings.Color);
   }

   protected override void SetConstantsImp () => Pgm.Set (muVPScale, Lux.VPScale).Set (muFontTexture, 0);
   protected override Settings SnapUniformsImp () => new (Lux.DrawColor, Lux.TypeFace ?? TypeFace.Default);

   // Nested types -------------------------------------------------------------
   [StructLayout (LayoutKind.Sequential)]
   public readonly record struct Args (Vec4S Cell, int TexOffset);
   public readonly record struct Settings (Color4 Color, TypeFace Face);
}
#endregion

#region class Triangle2DShader ---------------------------------------------------------------------
/// <summary>Shader to draw simple triangles in 2D (specified in world space, no anti-aliasing)</summary>
[Singleton]
partial class Triangle2DShader () : TriQuad2DShader (ShaderImp.Triangle2D) { }
#endregion

#region class TriQuad2DShader ----------------------------------------------------------------------
/// <summary>TriQuad2DShader is the base class for Triangle2DShader and Quad2DShader</summary>
abstract class TriQuad2DShader : Shader<Vec2F, Color4> {
   // Constructors -------------------------------------------------------------
   protected TriQuad2DShader (ShaderImp imp) : base (imp) => Bind ();
   int muXfm = 0, muDrawColor = 0;

   // Overrides ----------------------------------------------------------------
   protected override void ApplyUniformsImp (ref readonly Color4 color) => Pgm.Set (muDrawColor, color);
   protected override int OrderUniformsImp (ref readonly Color4 a, ref readonly Color4 b) => (int)(a.Value - b.Value);
   protected override void SetConstantsImp () => Pgm.Set (muXfm, Lux.Xfm);
   protected override Color4 SnapUniformsImp () => Lux.DrawColor;
}
#endregion
