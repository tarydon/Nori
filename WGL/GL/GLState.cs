// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ GLState.cs
// ║║║║╬║╔╣║ Class GLState - used to effect changes to OpenGL state (write only on change)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class GLState ------------------------------------------------------------------------------
/// <summary>The GLState class is used to effect any changes to OpenGL state</summary>
/// The point of this class is to call underlying functions like GL.Enable, 
/// GL.Scissor etc only when the state actually changes (these are global state). 
/// Also, at the start of every frame, we call GLState.Reset() to reset 
/// all this state to a stable known default. This is an internal class,
/// used primarily by the Lux draw classes.
static class GLState {
   // Properties ---------------------------------------------------------------
   /// <summary>Is Blending now enable (default = false)</summary>
   public static bool Blending {
      set {
         if (mBlending == value) return;
         mBlending = value;
         if (value) GL.Enable (ECap.Blend); else GL.Disable (ECap.Blend);
      }
   }
   static bool mBlending;

   /// <summary>Is depth-testing now enable (default = false)</summary>
   public static bool DepthTest {
      set {
         if (mDepthTest == value) return;
         mDepthTest = value;
         if (value) GL.Enable (ECap.DepthTest); else GL.Disable (ECap.DepthTest);
      }
   }
   static bool mDepthTest;

   /// <summary>Is polygon-offset-fill enabled? (default = false)</summary>
   public static bool PolygonOffsetFill {
      set {
         if (mPolygonOffsetFill == value) return;
         mPolygonOffsetFill = value;
         if (value) GL.Enable (ECap.PolygonOffsetFill); else GL.Disable (ECap.PolygonOffsetFill);
      }
   }
   static bool mPolygonOffsetFill;

   /// <summary>The current shader program to use</summary>
   public static ShaderImp? Program {
      set {
         if (mProgram == value) return;
         mProgram = value;
         if (value != null) {
            mPgmChanges++;
            Blending = value.Blending;
            DepthTest = value.DepthTest;
            PolygonOffsetFill = value.PolygonOffset;
         }
         GL.UseProgram (value?.Handle ?? 0);
      }
   }
   static ShaderImp? mProgram;
   static internal int mPgmChanges;    // Number of program changes in this frame

   /// <summary>The current vertex-array-object being used</summary>
   public static HVertexArray VAO {
      get => mHVAO;
      set {
         if (mHVAO == value) return;
         GL.BindVertexArray (mHVAO = value);
         if (value != 0) mVAOChanges++;
      }
   }
   static HVertexArray mHVAO;
   static internal int mVAOChanges;    // Number of VAO changes in this frame

   // Methods ------------------------------------------------------------------
   /// <summary>Resets everything to a known state (at the start of every frame)</summary>
   public static void StartFrame ((int X, int Y) size, Color4 bgrdColor) {
      GL.Viewport (0, 0, size.X, size.Y);
      // GL.Enable (ECap.ScissorTest);
      GL.BlendFunc (EBlendFactor.SrcAlpha, EBlendFactor.OneMinusSrcAlpha);
      GL.PatchParameter (EPatchParam.PatchVertices, 4);
      GL.PrimitiveRestartIndex (0xFFFFFFFF);
      GL.PolygonOffset (1, 1);

      // We 'force-set' each of these settings below by priming them with a
      // different value beforehand
      mBlending = true; Blending = false;
      mDepthTest = true; DepthTest = false;
      mPolygonOffsetFill = true; PolygonOffsetFill = false;
      mProgram = null; GL.UseProgram (0);
      mHVAO = 0; GL.BindVertexArray (0);
      mPgmChanges = 0; mVAOChanges = 0;

      var (r, g, b, a) = bgrdColor;
      GL.ClearColor (r / 255f, g / 255f, b / 255f, a / 255f);
      GL.Clear (EBuffer.Color | EBuffer.Depth);
   }
}
#endregion
