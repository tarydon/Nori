// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Lux.cs
// ║║║║╬║╔╣║ The Lux class: public interface to the Lux rendering engine
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Subjects;
namespace Nori;

#region class Lux ----------------------------------------------------------------------------------
/// <summary>The public interface to the Lux renderer</summary>
public static partial class Lux {
   public static int Rung;

   /// <summary>Creates the Lux rendering panel</summary>
   public static UIElement CreatePanel ()
      => Panel.It;

   /// <summary>This is a good prototype of how rendering with the Lux renderer will look like</summary>
   static void DrawScene () {
      PointSize = 36f;
      DrawColor = Color4.Magenta;
      Points ([new (98, 48), new (18, 18), new (98, 18)]);

      LineWidth = 3f;
      DrawColor = Color4.Yellow;
      Lines ([new (10, 10), new (90, 10), new (90, 10), new (90, 40)]);
      Lines ([new (13, 13), new (93, 13), new (93, 13), new (93, 43)]);

      LineWidth = 6f;
      DrawColor = Color4.White;
      Beziers ([new (10, 10), new (10, 40), new (80, 20), new (80, 50)]);

      LineWidth = 12f;
      DrawColor = Color4.Blue;
      Lines ([new (90, 40), new (10, 10)]);

      PointSize = 12f;
      DrawColor = Color4.Green;
      Points ([new (95, 45), new (15, 15), new (95, 15)]);

      LineWidth = 3f;
      DrawColor = Color4.Yellow;
      Lines ([new (13, 43), new (93, 13)]);

      PointSize = 36f;
      DrawColor = Color4.Magenta;
      Points ([new (98, 48), new (18, 18), new (98, 18)]);

      DrawColor = Color4.Cyan;
      Triangles ([new (30, 40), new (40, 40), new (40, 45)]);

      DrawColor = Color4.Cyan;
      Quads ([new (50, 40), new (60, 40), new (65, 45), new (50, 50)]);
   }

   /// <summary>Stub for the Render method that is called when each frame has to be painted</summary>
   public static void Render () {
      // Don't look at all this too closely - it is temporary code that will
      // later go away and be replaced by something more clean
      var panel = Panel.It;
      panel.BeginRender (panel.Size, ETarget.Screen);
      Lux.StartFrame (panel.Size);
      GLState.StartFrame (panel.Size, Color4.Black);
      RBatch.StartFrame ();
      Shader.StartFrame ();
      mFrame++;

      DrawScene ();

      RBatch.IssueAll ();
      RBatch.ReleaseAll ();

      panel.EndRender ();
      Info.FrameOver ();
   }
   static int mFrame;

   /// <summary>This is called at the start of every frame to reset to known</summary>
   public static void StartFrame ((int X, int Y) viewport) {
      VPScale = new Vec2F (2.0 / viewport.X, 2.0 / viewport.Y);
      Xfm = (Mat4F)Matrix3.Map (new Bound2 (0, 0, 100, 100), viewport);
      DrawColor = Color4.White;
      LineWidth = 3f;
      PointSize = 3f;
      Rung++;
   }

   public class Stats {
      /// <summary>The current frame number</summary>
      public int NFrame => mFrame;
      /// <summary>How many times is a program change happening, per frame</summary>
      public int PgmChanges => GLState.mPgmChanges;
      /// <summary>How many times is a new VAO bound, per frame</summary>
      public int VAOChanges => GLState.mVAOChanges;
      /// <summary>How many times are we applying new uniforms per frame</summary>
      public int ApplyUniforms => Shader.mApplyUniforms;
      /// <summary>How many draw calls per frame</summary>
      public int DrawCalls => RBatch.mDrawCalls;
      /// <summary>Number of vertices drawn</summary>
      public int VertsDrawn => RBatch.mVertsDrawn;
      public int SetConstants => Shader.mSetConstants;
   }
   static Stats sStats = new ();

   public class TInfo : IObservable<Stats> {
      public IDisposable Subscribe (IObserver<Stats> observer) => (mSubject ??= new ()).Subscribe (observer);

      public void FrameOver () => mSubject?.OnNext (sStats);
      Subject<Stats>? mSubject;
   }
   static public TInfo Info = new ();
}
#endregion
