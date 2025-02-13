// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Lux.cs
// ║║║║╬║╔╣║ The Lux class: public interface to the Lux rendering engine
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Subjects;
namespace Nori;

#region class Lux ----------------------------------------------------------------------------------
/// <summary>The public interface to the Lux renderer</summary>
public static partial class Lux {
   /// <summary>The current scene that is rendered in the visible viewport</summary>
   public static Scene? UIScene { get => mUIScene; set { mUIScene = value; Redraw (); } }
   static Scene? mUIScene;

   internal static int Rung;

   /// <summary>Creates the Lux rendering panel</summary>
   public static UIElement CreatePanel ()
      => Panel.It;

   public static Action? OnReady;
   static bool mReadyFired;

   /// <summary>Stub for the Render method that is called when each frame has to be painted</summary>
   public static void Render () {
      // Don't look at all this too closely - it is temporary code that will
      // later go away and be replaced by something more clean
      if (!mReadyFired) { mReadyFired = true; OnReady?.Invoke (); }

      var panel = Panel.It;
      panel.BeginRender (panel.Size, ETarget.Screen);
      StartFrame (panel.Size);
      GLState.StartFrame (panel.Size, mUIScene?.BgrdColor ?? Color4.Gray (96));
      RBatch.StartFrame ();
      Shader.StartFrame ();
      mFrame++;

      if (mUIScene != null) {
         mUIScene.Viewport = panel.Size;
         Xfm = (Mat4F)mUIScene.Xfm;
         NormalXfm = Xfm.ExtractRotation ();
         mUIScene.Draw ();
      }

      RBatch.IssueAll ();
      RBatch.ReleaseAll ();

      panel.EndRender ();
      Info.FrameOver ();
   }
   static int mFrame;

   public static void Redraw ()
      => Panel.It.Redraw ();

   /// <summary>This is called at the start of every frame to reset to known</summary>
   public static void StartFrame (Vec2S viewport) {
      VPScale = new Vec2F (2.0 / viewport.X, 2.0 / viewport.Y);
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
