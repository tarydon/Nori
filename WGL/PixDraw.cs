// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ PixDraw.cs
// ║║║║╬║╔╣║ Draw-related properties and methods of the Pix class
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Pix ----------------------------------------------------------------------------------
/// <summary>The public interface to the Pix renderer</summary>
public static partial class Pix {
   // Properties ---------------------------------------------------------------
   /// <summary>The current drawing color (default = white)</summary>
   public static Color4 DrawColor { get => mDrawColor; set => mDrawColor = value; }
   static Color4 mDrawColor;

   /// <summary>The current line-width, in device-independent pixels</summary>
   public static float LineWidth { get => mLineWidth; set => mLineWidth = value; }
   static float mLineWidth;

   /// <summary>The diameter of a point, in device-independent pixels</summary>
   public static float PointSize { get => mPointSize; set => mPointSize = value; }
   static float mPointSize;

   /// <summary>Viewport scale (convert viewport pixels to GL clip coordinates -1 .. +1)</summary>
   public static Vec2F VPScale { get => mVPScale; set => mVPScale = value; }
   static Vec2F mVPScale;

   /// <summary>The current transform to use for the rendering</summary>
   public static Mat4F Xfm { get => mXfm; set => mXfm = value; }
   static Mat4F mXfm;

   // Methods ------------------------------------------------------------------
   /// <summary>Draws 2D lines in world coordinates, with Z = 0</summary>
   /// Every pair of Vec2F in the list creates one line, so with n points,
   /// n / 2 lines are drawn.
   public static void Lines (ReadOnlySpan<Vec2F> pts) {  // POI.
      var pgm = ShaderImp.Line2D;
      pgm.Use ();
      pgm.Uniform ("Xfm", Xfm);
      pgm.Uniform ("VPScale", VPScale);
      pgm.Uniform ("DrawColor", (Vec4F)DrawColor);
      pgm.Uniform ("LineWidth", LineWidth);
      RBuffer.AddData (pgm, pts);
   }

   /// <summary>Draws 2D lines in world coordinates, with Z = 0</summary>
   /// Every pair of Vec2F in the list creates one line, so with n points,
   /// n / 2 lines are drawn.
   public static void Beziers (ReadOnlySpan<Vec2F> pts) {
      var pgm = ShaderImp.Bezier2D;
      pgm.Use ();
      pgm.Uniform ("Xfm", Xfm);
      pgm.Uniform ("VPScale", VPScale);
      pgm.Uniform ("DrawColor", (Vec4F)DrawColor);
      pgm.Uniform ("LineWidth", LineWidth);
      RBuffer.AddData (pgm, pts);
   }
}
#endregion
