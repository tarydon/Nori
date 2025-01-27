// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ LuxDraw.cs
// ║║║║╬║╔╣║ Draw-related properties and methods of the Lux class
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Lux ----------------------------------------------------------------------------------
/// <summary>The public interface to the Lux renderer</summary>
public static partial class Lux {
   // Properties ---------------------------------------------------------------
   /// <summary>The current drawing color (default = white)</summary>
   public static Color4 DrawColor { 
      get => mDrawColor;
      set { if (Lib.Set (ref mDrawColor, value)) Rung++; }
   }
   static Color4 mDrawColor;

   /// <summary>The current line-width, in device-independent pixels</summary>
   public static float LineWidth { 
      get => mLineWidth;
      set { if (Lib.Set (ref mLineWidth, value)) Rung++; } 
   }
   static float mLineWidth;

   /// <summary>The diameter of a point, in device-independent pixels</summary>
   public static float PointSize { 
      get => mPointSize;
      set { if (Lib.Set (ref mPointSize, value)) Rung++; }
   }
   static float mPointSize;

   /// <summary>Viewport scale (convert viewport pixels to GL clip coordinates -1 .. +1)</summary>
   public static Vec2F VPScale { 
      get => mVPScale;
      set { if (Lib.Set (ref mVPScale, value)) Rung++; }
   }
   static Vec2F mVPScale;

   /// <summary>The current transform to use for the rendering</summary>
   public static Mat4F Xfm { get => mXfm; set { mXfm = value; Rung++; } }
   static Mat4F mXfm;

   // Methods ------------------------------------------------------------------
   /// <summary>Draws 2D lines in world coordinates, with Z = 0</summary>
   /// Every pair of Vec2F in the list creates one line, so with n points,
   /// n / 2 lines are drawn. The following Lux properties are used:
   /// - LineWidth : the width of the beziers, in device independent pixels
   /// - DrawColor : color of the lines being drawn
   public static void Beziers (ReadOnlySpan<Vec2F> pts)
      => Bezier2DShader.It.Draw (pts);

   /// <summary>Draws 2D lines in world coordinates, with Z = 0</summary>
   /// Every pair of Vec2F in the list creates one line, so with n points,
   /// n / 2 lines are drawn. The following Lux properties are used:
   /// - LineWidth : the width of the beziers, in device independent pixels
   /// - DrawColor : color of the lines being drawn
   public static void Lines (ReadOnlySpan<Vec2F> pts)
      => Line2DShader.It.Draw (pts);

   /// <summary>Draws 2D points in world coordinates, with Z = 0</summary>
   /// The following Lux properties are used:
   /// - PointSize : the diameter of the points, in device independent pixels
   /// - DrawColor : color of the points being drawn
   public static void Points (ReadOnlySpan<Vec2F> pts)
      => Point2DShader.It.Draw (pts);

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
}
#endregion
