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
         if (Lib.Set (ref mBlending, value))
            GL.Enable (ECap.Blend, value);
      }
   }
   static bool mBlending;

   /// <summary>Is depth-testing now enable (default = false)</summary>
   public static bool DepthTest {
      set {
         if (Lib.Set (ref mDepthTest, value))
            GL.Enable (ECap.DepthTest, value);
      }
   }
   static bool mDepthTest;

   /// <summary>Is polygon-offset-fill enabled? (default = false)</summary>
   public static bool PolygonOffsetFill {
      set {
         if (Lib.Set (ref mPolygonOffsetFill, value))
            GL.Enable (ECap.PolygonOffsetFill, value);
      }
   }
   static bool mPolygonOffsetFill;

   public static bool PrimitiveRestart {
      set {
         if (Lib.Set (ref mPrimitiveRestart, value))
            GL.Enable (ECap.PrimitiveRestart, value);
      }
   }
   static bool mPrimitiveRestart;

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
            StencilBehavior = value.StencilBehavior;
            PrimitiveRestart = value.Mode == EMode.TriangleFan;
         }
         GL.UseProgram (value?.Handle ?? 0);
      }
   }
   static ShaderImp? mProgram;
   static internal int mPgmChanges;    // Number of program changes in this frame

   /// <summary>The 'stencil behavior' of the current program</summary>
   /// See the TriFanStencil shader for a more detailed example of this algorithm (this is used
   /// to implement the FillPoly Lux method).
   public static EStencilBehavior StencilBehavior {
      set {
         if (mStencilBehavior == value) return;
         mStencilBehavior = value;
         // We enable the stencil test when the stencil-behavior is set other than NONE.
         // This enables both the StencilOp (used to update the stencil buffer) and the StencilFunc
         // used to test and discard some pixels using stencil buffer comparisons
         GL.Enable (ECap.StencilTest, mStencilBehavior != EStencilBehavior.None);
         switch (value) {
            case EStencilBehavior.Stencil:
               // This is used by the TriFanStencil shader - the first phase of the stencil-then-cover
               // algorithm. We use only one bit of the Stencil buffer (bit 0), and start with this
               // full cleared (by glClear). Then, for every pixel touched, we invert this as we draw
               // a triangle-fan from a fixed point to every segment of every closed polyline in the path.
               // That will end up leaving the stencil bit set for every point inside the path. We also
               // use StencilFunc to avoid updating any actual color buffer pixels during this phase.
               GL.StencilOp (EFace.FrontAndBack, EStencilOp.Invert, EStencilOp.Invert, EStencilOp.Invert);
               GL.StencilFunc (EFace.FrontAndBack, EStencilFunc.Never, 0, 0);
               break;
            case EStencilBehavior.Cover:
               // This is used by the TriFanCover shader - the second phase of the stencil-then-cover
               // algorithm. For every pixel where the stencil bit 0 is set, we pass and update the
               // color buffer (painting through the stencil). We also clear those bits using the
               // StencilOp at the same time, so the stencil buffer is reset in preparation for the next
               // primitive to be drawn
               GL.StencilOp (EFace.FrontAndBack, EStencilOp.Zero, EStencilOp.Zero, EStencilOp.Zero);
               GL.StencilFunc (EFace.FrontAndBack, EStencilFunc.Equal, 1, 0x1);
               break;
         }
      }
   }
   static EStencilBehavior mStencilBehavior;

   /// <summary>The current typeface being used</summary>
   public static TypeFace? TypeFace {
      set {
         if (mTypeFaceId == value?.UID) return;
         if (value != null) {
            GL.ActiveTexture (ETexUnit.Tex0);
            GL.PixelStore (EPixelStoreParam.UnpackAlignment, 1);
            GL.BindTexture (ETexTarget.TexRectangle, value.Texture);
            mTypeFaceId = value.UID;
         }
      }
   }
   static int mTypeFaceId = 0;

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
   public static void StartFrame (Vec2S size, Color4 bgrdColor) {
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
      mStencilBehavior = EStencilBehavior.Cover; StencilBehavior = EStencilBehavior.None;
      mPolygonOffsetFill = true; PolygonOffsetFill = false;
      mProgram = null; GL.UseProgram (0);
      mHVAO = 0; GL.BindVertexArray (0);
      mPgmChanges = 0; mVAOChanges = 0;
      mTypeFaceId = 0;

      var (r, g, b, a) = bgrdColor;
      GL.ClearColor (r / 255f, g / 255f, b / 255f, a / 255f);
      GL.Clear (EBuffer.Color | EBuffer.Depth | EBuffer.Stencil);
   }
}
#endregion
