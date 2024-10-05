// ---------------------------------------------------------------------------------------
// Pix ~ Scene-graph based rendering engine built on OpenGL
// ---------------------------------------------------------------------------------------
// PX.cs
// Implements the PX class - the public interface to the Pix rendering engine
// ---------------------------------------------------------------------------------------
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

      GL.ClearColor (0, 0, 1, 1);
      GL.Clear (EBuffer.Depth | EBuffer.Color);
      GL.Enable (ECap.Blend);
      GL.BlendFunc (EBlendFactor.SrcAlpha, EBlendFactor.OneMinusSrcAlpha);

      panel.EndRender ();
   }
}
#endregion
