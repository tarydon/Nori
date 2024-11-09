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

      var mapping = (Mat4F)Matrix3.Map (new Bound2 (0, 0, 100, 100), panel.Size);
      var vpscale = new Vec2F (2.0 / panel.Size.X, 2.0 / panel.Size.Y);

      Random r = new ();
      var pts = new Point2[40];
      for (int i = 0; i < pts.Length; i++) 
         pts[i] = new Point2 (r.Next (100), r.Next (100));

      var pgm = Pipeline.Line2D;
      GL.UseProgram (pgm.Handle);
      pgm.Uniform ("DrawColor", (Vec4F)Color4.Gray (100));
      pgm.Uniform ("Xfm", mapping);
      pgm.Uniform ("VPScale", vpscale);
      pgm.Uniform ("LineWidth", 3f);
      for (int i = 0; i < pts.Length; i += 4) {
         GL.Begin (EMode.LineStrip);
         for (int j = 0; j < 4; j++)
            GL.Vertex ((float)pts[i + j].X, (float)pts[i + j].Y);
         GL.End ();
      }

      pgm = Pipeline.Bezier;
      GL.UseProgram (pgm.Handle);
      pgm.Uniform ("DrawColor", (Vec4F)Color4.White);
      pgm.Uniform ("Xfm", mapping);
      pgm.Uniform ("VPScale", vpscale);
      pgm.Uniform ("LineWidth", 3f);
      GL.Begin (EMode.Patches);
      pts.ForEach (a => GL.Vertex ((float)a.X, (float)a.Y));
      GL.End ();

      panel.EndRender ();
   }
}
#endregion
