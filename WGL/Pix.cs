// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Pix.cs
// ║║║║╬║╔╣║ The Pix class: public interface to the Pix rendering engine
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Pix ----------------------------------------------------------------------------------
/// <summary>The public interface to the Pix renderer</summary>
public static class Pix {
   /// <summary>Creates the Pix rendering panel</summary>
   public static UIElement CreatePanel ()
      => Panel.It;

   public static unsafe void DrawText (string text, Point2 position, Vector2 offset, float size, Mat4F mapping, Vec2F vpscale) {      
      RetainBuffer buf = new ();
      var pgm = Pipeline.Text2D; pgm.Use ();

      pgm.Uniform ("Xfm", mapping);
      pgm.Uniform ("VPScale", vpscale);
      pgm.Uniform ("DrawColor", (Vec4F)Color4.White);

      float scale = size / 16f, dx = -(text.Length * size) / 2 + (float)offset.X, dy = -size + (float)offset.Y;
      foreach (var ch in text) {
         Vec2F pt = (Vec2F)position, off = (Vec2F)offset;
         buf.AddData (&pt, 8); buf.AddData (&dx, 4); buf.AddData (&dy, 4);
         buf.AddData (&scale, 4); buf.AddData (&ch, 2);
         dx += size;
      }
      Attrib[] attribs = [Attrib.AVec2f, Attrib.AVec2f, Attrib.AFloat, Attrib.AShort];
      buf.Draw (EMode.Points, attribs, 22, 0, text.Length);

      buf.Release ();
   }

   public static unsafe void DrawPolys (Poly[] poly, Mat4F mapping, Vec2F vpscale) {
      List<Vec2F> lines = [], arcs = [];
      foreach (var seg in poly.SelectMany (a => a.Segs)) {
         if (seg.IsArc) seg.ToBeziers (arcs);
         else lines.AddRange ([(Vec2F)seg.A, (Vec2F)seg.B]);
      }

      RetainBuffer buf = new ();
      int lineStart = buf.AddData2 (lines.AsSpan ());
      int arcStart = buf.AddData2 (arcs.AsSpan ());

      var pgm = Pipeline.Line2D; pgm.Use ();
      pgm.Uniform ("DrawColor", (Vec4F)Color4.White);
      pgm.Uniform ("Xfm", mapping);
      pgm.Uniform ("VPScale", vpscale);
      pgm.Uniform ("LineWidth", 3f);
      buf.Draw (EMode.Lines, [Attrib.AVec2f], 8, lineStart, lines.Count);

      pgm = Pipeline.Bezier2D; pgm.Use (); 
      pgm.Uniform ("DrawColor", (Vec4F)Color4.White);
      pgm.Uniform ("Xfm", mapping);
      pgm.Uniform ("VPScale", vpscale);
      pgm.Uniform ("LineWidth", 3f);
      buf.Draw (EMode.Patches, [Attrib.AVec2f], 8, arcStart, arcs.Count);

      buf.Release ();
   }

   /// <summary>Stub for the Render method that is called when each frame has to be painted</summary>
   public static void Render () {
      var panel = Panel.It;
      panel.BeginRender (panel.Size, ETarget.Screen);

      var clr = Color4.Black;
      GL.ClearColor (clr.R / 255f, clr.G / 255f, clr.B / 255f, clr.A / 255f); 
      GL.Clear (EBuffer.Depth | EBuffer.Color);
      GL.Enable (ECap.Blend);
      GL.BlendFunc (EBlendFactor.SrcAlpha, EBlendFactor.OneMinusSrcAlpha);
      GL.PatchParameter (EPatchParam.PatchVertices, 4);

      var mapping = (Mat4F)Matrix3.Map (new Bound2 (-10, -10, 110, 70), panel.Size);
      var vpscale = new Vec2F (2.0 / panel.Size.X, 2.0 / panel.Size.Y);

      Poly[] polys = [
         Poly.Rectangle (0, 0, 100, 60),
         Poly.Parse ("M10,10 H90 V30 Q70,50,1 H30 Q10,30,-1 Z"),
         Poly.Circle (new (70, 30), 15),
      ];
      DrawPolys (polys, mapping, vpscale);

      DrawText ("Hello, World!", new (25, 15), new (0, 0), 20, mapping, vpscale);
      DrawText ("A Brave New World.", new (70, 30), new (0, 0), 30, mapping, vpscale);

      panel.EndRender ();
   }
}
#endregion
