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

   public static void SetScene (Color4 color, Action<(int, int)> callback) {
      mSceneBgrd = color;
      mSceneDraw = callback;
   }
   static Color4 mSceneBgrd;
   static Action<(int, int)>? mSceneDraw;

   /// <summary>Stub for the Render method that is called when each frame has to be painted</summary>
   public static void Render () {
      var panel = Panel.It;
      panel.BeginRender (panel.Size, ETarget.Screen);

      var clr = mSceneBgrd;
      GL.ClearColor (clr.R / 255f, clr.G / 255f, clr.B / 255f, clr.A / 255f); 
      GL.Clear (EBuffer.Depth | EBuffer.Color);
      GL.Enable (ECap.Blend);
      GL.BlendFunc (EBlendFactor.SrcAlpha, EBlendFactor.OneMinusSrcAlpha);
      mSceneDraw?.Invoke (panel.Size);

      var mapping = (Mat4F)Matrix3.Map (new Bound2 (0, 0, 100, 100), panel.Size);
      var vpscale = new Vec2F (2.0 / panel.Size.X, 2.0 / panel.Size.Y);

      List<Point2> pts = [];
      for (int i = 10; i <= 90; i += 5) {
         pts.Add (new (0, i));
         pts.Add (new (i, i));
      }

      var pgm = Pipeline.Line2D;
      GL.UseProgram (pgm.Handle);
      pgm.Uniform ("LineWidth", 3f);

      pgm.Uniform ("DrawColor", (Vec4F)Color4.White);
      pgm.Uniform ("Xfm", mapping);
      pgm.Uniform ("VPScale", vpscale);
      GL.Begin (EMode.Lines);
      foreach (var pt in pts) GL.Vertex ((float)pt.X, (float)pt.Y);
      GL.End ();

      pgm = Pipeline.ArrowHead;
      GL.UseProgram (pgm.Handle);
      pgm.Uniform ("DrawColor", (Vec4F)Color4.White);
      pgm.Uniform ("Xfm", mapping);
      pgm.Uniform ("VPScale", vpscale);
      pgm.Uniform ("ArrowSize", 25);
      GL.Begin (EMode.Lines);
      foreach (var pt in pts) GL.Vertex ((float)pt.X, (float)pt.Y);
      GL.End ();

      Random r = new (0);
      pgm = Pipeline.Point2D;
      GL.UseProgram (pgm.Handle);
      pgm.Uniform ("DrawColor", (Vec4F)Color4.Yellow);
      pgm.Uniform ("Xfm", mapping);
      pgm.Uniform ("VPScale", vpscale);
      pgm.Uniform ("PointSize", 20);
      GL.Begin (EMode.Points);
      for (int i = 0; i < 20; i++) GL.Vertex (r.Next (100), r.Next (100));
      GL.End ();

      panel.EndRender ();
   }
}
#endregion
