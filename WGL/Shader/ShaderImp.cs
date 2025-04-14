// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ ShaderImp.cs
// ║║║║╬║╔╣║ ShaderImp is the low level wrapper around an OpenGL shader pipeline
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO;
namespace Nori;

#region class ShaderImp ----------------------------------------------------------------------------
/// <summary>Wrapper around an OpenGL shader pipeline</summary>
class ShaderImp {
   // Constructor --------------------------------------------------------------
   /// <summary>Construct a pipeline given the code for the individual shaders</summary>
   ShaderImp (string name, EMode mode, EVertexSpec vspec, string[] code, bool blend, bool depthTest, bool polyOffset, EStencilBehavior stencil) {
      (Name, Mode, VSpec, Blending, DepthTest, PolygonOffset, StencilBehavior, Handle) 
         = (name, mode, vspec, blend, depthTest, polyOffset, stencil, GL.CreateProgram ());
      code.ForEach (a => GL.AttachShader (Handle, sCache.Get (a, CompileShader)));
      GL.LinkProgram (Handle);
      string log2 = GL.GetProgramInfoLog (Handle);
      if (GL.GetProgram (Handle, EProgramParam.LinkStatus) == 0)
         throw new Exception ($"GLProgram link error in program '{Name}':\r\n{log2}");
      if (!string.IsNullOrWhiteSpace (log2))
         Lib.Trace ($"Warning while linking program '{Name}':\r\n{log2}\n");

      // Get information about the uniforms
      int cUniforms = GL.GetProgram (Handle, EProgramParam.ActiveUniforms);
      mUniforms = new UniformInfo[cUniforms];
      for (int i = 0; i < cUniforms; i++) {
         GL.GetActiveUniform (Handle, i, out int size, out var type, out string uname, out int location);
         object value = type switch {
            EDataType.Int or EDataType.Sampler2D or EDataType.Sampler2DRect => 0,
            EDataType.Vec2f => new Vec2F (0, 0),
            EDataType.Vec4f => new Vec4F (0, 0, 0, 0),
            EDataType.Float => 0f,
            EDataType.Mat4f => Mat4F.Zero,
            _ => throw new NotImplementedException ()
         };
         mUniformMap[uname] = location;
         mUniforms[location] = new UniformInfo (uname, type, location, value);
         if (uname == "LTypeTexture") MakeLTypeTexture ();
      }
   }
   // A cache of already compiled individual shaders 
   static Dictionary<string, HShader> sCache = [];

   // Properties ---------------------------------------------------------------
   /// <summary>Enable blending when this program is used</summary>
   public readonly bool Blending;
   /// <summary>Enable depth-testing when this program is used</summary>
   public readonly bool DepthTest;
   /// <summary>The OpenGL handle for this shader program (set up with GL.UseProgram)</summary>
   public readonly HProgram Handle;
   /// <summary>The primitive draw-mode used for this program</summary>
   public readonly EMode Mode;
   /// <summary>The name of this shader</summary>
   public readonly string Name;
   /// <summary>What is the special 'stencil-buffer' behavior of this program</summary>
   public readonly EStencilBehavior StencilBehavior;
   /// <summary>Enable polygon-offset-fill when this program is used</summary>
   public readonly bool PolygonOffset;
   /// <summary>The vertex-specification for this shader</summary>
   public readonly EVertexSpec VSpec;

   /// <summary>The list of all the uniforms used by this shader</summary>
   public IReadOnlyList<UniformInfo> Uniforms => mUniforms;

   // Methods ------------------------------------------------------------------
   /// <summary>Gets the Id of a uniform value</summary>
   public int GetUniformId (string name) {
      if (mUniformMap.TryGetValue (name, out int id)) return id;
      return -1;
   }

   /// <summary>Sets a Uniform variable of type Color4 (we pass these as Vec4F)</summary>
   public ShaderImp Set (int index, Color4 color)
      => Set (index, (Vec4F)color);

   /// <summary>Sets a Uniform variable of type float</summary>
   public ShaderImp Set (int index, float f) {
      if (index != -1) {
         var data = mUniforms[index];
         if (!f.EQ ((float)data.Value)) { data.Value = f; GL.Uniform (index, f); }
      }
      return this;
   }

   /// <summary>Sets a Uniform variable of type int</summary>
   public ShaderImp Set (int index, int n) {
      if (index != -1) {
         var data = mUniforms[index];
         if (n != (int)data.Value) { data.Value = n; GL.Uniform1i (index, n); }
      }
      return this;
   }

   /// <summary>Set a uniform of type Vec2f</summary>
   public ShaderImp Set (int index, Vec2F v) {
      if (index != -1) {
         var data = mUniforms[index];
         if (!v.EQ ((Vec2F)data.Value)) { data.Value = v; GL.Uniform (index, v.X, v.Y); }
      }
      return this;
   }

   /// <summary>Sets a uniform variable of type Vec4f</summary>
   public ShaderImp Set (int index, Vec4F v) {
      if (index != -1) {
         var data = mUniforms[index];
         if (!v.EQ ((Vec4F)data.Value)) { data.Value = v; GL.Uniform (index, v.X, v.Y, v.Z, v.W); }
      }
      return this;
   }

   /// <summary>Set a uniform of type Mat4f</summary>
   public unsafe ShaderImp Set (int index, ref Mat4F m) {
      if (index != -1) {
         var data = mUniforms[index]; data.Value = m;
         fixed (float* f = &m.M11) GL.Uniform (index, false, f);
      }
      return this;
   }

   // Standard shaders ---------------------------------------------------------
   public static ShaderImp Bezier2D => mBezier2D ??= Load ();
   public static ShaderImp Line2D => mLine2D ??= Load ();
   public static ShaderImp DashLine2D => mDashLine2D ??= Load ();
   public static ShaderImp Point2D => mPoint2D ??= Load ();
   public static ShaderImp Triangle2D => mTriangle2D ??= Load ();
   public static ShaderImp Quad2D => mQuad2D ??= Load ();
   static ShaderImp? mLine2D, mBezier2D, mPoint2D, mTriangle2D, mQuad2D, mDashLine2D;

   public static ShaderImp BlackLine => mBlackLine ??= Load ();
   public static ShaderImp GlassLine => mGlassLine ??= Load ();
   public static ShaderImp Gourad => mGourad ??= Load ();
   public static ShaderImp Phong => mPhong ??= Load ();
   public static ShaderImp Glass => mGlass ??= Load ();
   public static ShaderImp FlatFacet => mFlatFacet ??= Load ();
   static ShaderImp? mBlackLine, mGlassLine, mGourad, mPhong, mFlatFacet, mGlass;

   public static ShaderImp TriFanStencil => mTriFanStencil ??= Load ();
   public static ShaderImp TriFanCover => mTriFanCover ??= Load ();
   static ShaderImp? mTriFanStencil, mTriFanCover;

   public static ShaderImp TextPx => mTextPx ??= Load ();
   public static ShaderImp Text2D => mText2D ??= Load ();
   static ShaderImp? mTextPx, mText2D;

   // Nested types ------------------------------------------------------------
   /// <summary>Provides information about a Uniform</summary>
   public class UniformInfo {
      public UniformInfo (string name, EDataType type, int location, object value)
         => (Name, Type, Location, Value) = (name, type, location, value);
      /// <summary>Name of this uniform</summary>
      public readonly string Name;
      /// <summary>Data-type of this uniform</summary>
      public readonly EDataType Type;
      /// <summary>Shader location for this uniform</summary>
      public readonly int Location;
      /// <summary>Last-set value for this uniform</summary>
      public object Value;

      public override string ToString ()
         => $"Uniform({Location}) {Type} {Name}";
   }

   // Implementation -----------------------------------------------------------
   // Compiles an individual shader, given the source file (this reuses already compiled
   // shaders where possible, since some shaders are part of multiple pipelines)
   HShader CompileShader (string file) {
      var text = Lib.ReadText ($"wad:GL/Shader/{file}");
      var eShader = Enum.Parse<EShader> (Path.GetExtension (file)[1..], true);
      var shader = GL.CreateShader (eShader);
      GL.ShaderSource (shader, text);
      GL.CompileShader (shader);
      if (GL.GetShader (shader, EShaderParam.CompileStatus) == 0) {
         string log = GL.GetShaderInfoLog (shader);
         throw new Exception ($"OpenGL shader compile error in '{file}':\r\n{log}");
      }
      return shader;
   }

   // This loads the information for a particular shader from the Shader/Index.txt
   // and builds it (that index contains the list of actual vertex / geometry / fragment
   // programs)
   static ShaderImp Load ([CallerMemberName] string name = "") {
      sIndex ??= Lib.ReadLines ("wad:GL/Shader/Index.txt");
      // Each line in the index.txt contains these:
      // 0:Name  1:Mode  2:VSpec  3:Blending  4:DepthTest  5:PolygonOffset  6:StencilBehavior  7:Programs
      foreach (var line in sIndex) {
         var w = line.Split (' ', StringSplitOptions.RemoveEmptyEntries);
         if (w.Length >= 8 && w[0] == name) {
            var mode = Enum.Parse<EMode> (w[1], true);
            var vspec = Enum.Parse<EVertexSpec> (w[2], true);
            bool blending = w[3] == "1", depthtest = w[4] == "1", offset = w[5] == "1";
            var stencil = Enum.Parse<EStencilBehavior> (w[6], true);
            var programs = w[7].Split ('|').ToArray ();
            return new (name, mode, vspec, programs, blending, depthtest, offset, stencil);
         }
      }
      throw new NotImplementedException ($"Shader {name} not found in Shader/Index.txt");
   }
   static string[]? sIndex;

   // This is called exactly once in the application lifetime to make the line-type texture.
   // We store the different line-type patterns in a texture. The t coordinate is used to select
   // one of the different line-types and then the s coordinate is used to pick up the stipple 
   // pattern for that linetype.
   static void MakeLTypeTexture () {
      if (miLTypeTextureMade) return;
      miLTypeTextureMade = true;
      // We're always hardcoding that texture-unit 1 will be used for the linetype texture
      // (just like texture unit 0 is used for truetype font texture)
      GL.ActiveTexture (ETexUnit.Tex1);
      HTexture idTexture = GL.GenTexture ();
      GL.BindTexture (ETexTarget.Texture2D, idTexture);

      byte[,] data = new byte[10, 60];
      foreach (string s in mLTypeData) {
         string[] w = s.Split ([' '], StringSplitOptions.RemoveEmptyEntries);
         int n = (int)Enum.Parse<ELineType> (w[0], true);
         for (int j = 0; j < 60; j++) data[n, j] = (w[1][j] == 'x') ? (byte)255 : (byte)0;
      }
      GL.PixelStore (EPixelStoreParam.UnpackAlignment, 1);
      GL.TexImage2D (ETexTarget.Texture2D, EPixelInternalFormat.Red, 60, 10, EPixelFormat.Red, EPixelType.UByte, data);
      GL.TexParameter (ETexTarget.Texture2D, ETexParam.MagFilter, (int)ETexFilter.Linear);
      GL.TexParameter (ETexTarget.Texture2D, ETexParam.MinFilter, (int)ETexFilter.Linear);
      GL.TexParameter (ETexTarget.Texture2D, ETexParam.WrapS, (int)ETexWrap.Repeat);
   }
   // This gets set to true once we have made the linetype texture
   static bool miLTypeTextureMade;
   // This defines the actual bit patterns for each of the linetypes
   static readonly string[] mLTypeData = [
      "Phantom     xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx....xxxxxxxx....xxxxxxxx....",
      "Dash        xxxxxxxxxx.....xxxxxxxxxx.....xxxxxxxxxx.....xxxxxxxxxx.....",
      "DashDotDot  xxxxxxxxxxxxxxxxxxxxxxxxxxxx........xxxx........xxxx........",
      "Dot         xxx...xxx...xxx...xxx...xxx...xxx...xxx...xxx...xxx...xxx...",
      "Dash2       xxxxxxxxxxxxxxxxxx............xxxxxxxxxxxxxxxxxx............",
      "Hidden      xxxxxxx........xxxxxxx........xxxxxxx........xxxxxxx........",
      "Center      xxxxxxxxxx......xxxx......xxxxxxxxxxxxxx......xxxx......xxxx",
      "Border      xxxxxxxxxxxxxxxxx.....xxxxxxxxxxxxxxxxxx........xxxx........",
      "DashDot     xxxxxxxxxxxxxxxxxxxx....xx....xxxxxxxxxxxxxxxxxxxx....xx....",
      "Continuous  xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
   ];

   public override string ToString () {
      var sb = new StringBuilder ();
      sb.Append ($"Shader {Name}\nUniforms:\n");
      Uniforms.ForEach (a => sb.Append ($"  {a.Type} {a.Name}\n"));
      return sb.ToString ();
   }

   // Private data -------------------------------------------------------------
   UniformInfo[] mUniforms;         // Set of uniforms for this program
   // Dictionary mapping uniform names to uniform locations
   Dictionary<string, int> mUniformMap = new (StringComparer.OrdinalIgnoreCase);
}
#endregion

#region struct Attrib ------------------------------------------------------------------------------
/// <summary>Attrib represents one attribute in a VAO buffer</summary>
readonly record struct Attrib (int Dims, EDataType Type, int Size, bool Integral) {
   public static Attrib AVec2f = new (2, EDataType.Float, 8, false);
   public static Attrib AInt = new (1, EDataType.Int, 4, true);
   public static Attrib AShort = new (1, EDataType.Short, 2, true);
   public static Attrib AFloat = new (1, EDataType.Float, 4, false);
   public static Attrib AVec3f = new (3, EDataType.Float, 12, false);
   public static Attrib AVec4f = new (4, EDataType.Float, 16, false);
   public static Attrib AVec3h = new (3, EDataType.Half, 6, false);
   public static Attrib AVec4s = new (4, EDataType.Short, 8, true);

   public static Attrib[] GetFor (EVertexSpec spec) 
      => spec switch {
         EVertexSpec.Vec2F => [AVec2f],
         EVertexSpec.Vec3F_Vec3H => [AVec3f, AVec3h],
         EVertexSpec.Vec4S_Int => [AVec4s, AInt],
         EVertexSpec.Vec2F_Vec4S_Int => [AVec2f, AVec4s, AInt],
         _ => throw new BadCaseException (spec)
      };

   public static int GetSize (EVertexSpec spec)
      => spec switch {
         EVertexSpec.Vec2F => 8,
         EVertexSpec.Vec3F_Vec3H => 18,
         EVertexSpec.Vec4S_Int => 12,
         EVertexSpec.Vec2F_Vec4S_Int => 20,
         _ => throw new BadCaseException (spec)
      };
}
#endregion

#region enum EVertexSpec ---------------------------------------------------------------------------
// The various Vertex specifications used by OpenGL shaders
enum EVertexSpec { Vec2F, Vec3F_Vec3H, Vec4S_Int, Vec2F_Vec4S_Int, _Last };
#endregion

#region enum EStencilBehavior ----------------------------------------------------------------------
/// <summary>Does this shader have any special behavior related to the stencil-buffer?</summary>
/// See the TriFanStencil and TriFanCover shaders for more details on this
enum EStencilBehavior { None, Stencil, Cover };
#endregion
