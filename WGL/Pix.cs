// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Pix.cs
// ║║║║╬║╔╣║ The Pix class: public interface to the Pix rendering engine
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Pix ----------------------------------------------------------------------------------
/// <summary>The public interface to the Pix renderer</summary>
public static partial class Pix {
   /// <summary>Creates the Pix rendering panel</summary>
   public static UIElement CreatePanel ()
      => Panel.It;

   /// <summary>This is a good prototype of how rendering with the Pix renderer will look like</summary>
   static void DrawScene () { // POI.
      Pix.LineWidth = 3f;
      Pix.DrawColor = Color4.Yellow;
      Pix.Lines ([new (10, 10), new (90, 10), new (90, 10), new (90, 40)]);

      Pix.LineWidth = 6f;
      Pix.DrawColor = Color4.White;
      Pix.Beziers ([new (10, 10), new (10, 40), new (80, 20), new (80, 50)]);
   }

   /// <summary>Stub for the Render method that is called when each frame has to be painted</summary>
   public static void Render () {
      // Don't look at all this too closely - it is temporary code that will
      // later go away and be replaced by something more clean
      var panel = Panel.It;
      panel.BeginRender (panel.Size, ETarget.Screen);
      Pix.Reset (panel.Size);
      GLState.Reset (panel.Size, Color4.Black);

      DrawScene ();

      RBatch.All.ForEach (a => a.Issue ());
      RBuffer.It?.Release ();
      RBuffer.It = null;
      RBatch.All.Clear ();

      panel.EndRender ();
   }

   /// <summary>This is called at the start of every frame to reset to known</summary>
   public static void Reset ((int X, int Y) viewport) {
      VPScale = new Vec2F (2.0 / viewport.X, 2.0 / viewport.Y);
      Xfm = (Mat4F)Matrix3.Map (new Bound2 (0, 0, 100, 100), viewport);
      DrawColor = Color4.White;
      LineWidth = 3f;
      PointSize = 3f;
   }
}
#endregion
