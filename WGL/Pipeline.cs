// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Pipeline.cs
// ║║║║╬║╔╣║ A low level wrapper around an OpenGL shader pipeline
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO;
using System.Reflection;
namespace Nori;

#region class Pipeline -----------------------------------------------------------------------------
/// <summary>Wrapper around an OpenGL shader pipeline</summary>
class Pipeline {
   // Constructor --------------------------------------------------------------
   /// <summary>Construct a pipeline given the code for the individual shaders</summary>
   Pipeline (string name, params string[] code) {
      Debug.WriteLine ($"Compiling shader pipeline: {name}");
      (Name, Handle) = (name, GL.CreateProgram ());
      code.ForEach (a => GL.AttachShader (Handle, sCache.Get (a, CompileShader)));
      GL.LinkProgram (Handle);
      string log2 = GL.GetProgramInfoLog (Handle);
      if (GL.GetProgram (Handle, EProgramParam.LinkStatus) == 0)
         throw new Exception ($"GLProgram link error in program '{Name}':\r\n{log2}");
      if (!string.IsNullOrWhiteSpace (log2))
         Debug.WriteLine ($"Warning while linking program '{Name}':\r\n{log2}");

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
         Debug.WriteLine (mUniforms[location]);
      }
   }
   // A cache of already compiled individual shaders 
   static Dictionary<string, HShader> sCache = [];
   Dictionary<string, int> mUniformMap = new (StringComparer.OrdinalIgnoreCase);
   UniformInfo[] mUniforms;

   // Properties ---------------------------------------------------------------
   /// <summary>The OpenGL handle for this shader program (set up with GL.UseProgram)</summary>
   public readonly HProgram Handle;
   /// <summary>The name of this shader</summary>
   public readonly string Name;

   // Methods ------------------------------------------------------------------
   /// <summary>Sets a Uniform variable of type float</summary>
   public void Uniform (string name, float f) {
      if (mUniformMap.TryGetValue (name, out int n)) Uniform (n, f);
   }
   /// <summary>Sets a Uniform variable of type float</summary>
   public void Uniform (int index, float f) {
      var data = mUniforms[index];
      if (f.EQ ((float)data.Value)) return;
      data.Value = f; GL.Uniform (index, f);
   }

   /// <summary>Set a uniform of type Vec2f</summary>
   public void Uniform (int id, Vec2F v) {
      var data = mUniforms[id];
      if (v.EQ ((Vec2F)data.Value)) return;
      data.Value = v; GL.Uniform (id, v.X, v.Y);
   }
   /// <summary>Set a uniform of type Vec2f</summary>
   public void Uniform (string name, Vec2F v) {
      if (mUniformMap.TryGetValue (name, out int n)) Uniform (n, v);
   }

   /// <summary>Sets a uniform variable of type Vec4f</summary>
   public void Uniform (string name, Vec4F vec) {
      if (mUniformMap.TryGetValue (name, out int n)) Uniform (n, vec);
   }
   /// <summary>Sets a uniform variable of type Vec4f</summary>
   public void Uniform (int index, Vec4F v) {
      var data = mUniforms[index];
      if (v.EQ ((Vec4F)data.Value)) return;
      data.Value = v; GL.Uniform (index, v.X, v.Y, v.Z, v.W);
   }

   /// <summary>Set a uniform of type Mat4f</summary>
   public unsafe void Uniform (int id, Mat4F m) {
      if (id == -1) return;
      var data = mUniforms[id]; data.Value = m;
      GL.Uniform (id, false, &m.M11);
   }
   /// <summary>Set a uniform of type Mat4f</summary>
   public unsafe void Uniform (string name, Mat4F m) {
      if (mUniformMap.TryGetValue (name, out int n)) Uniform (n, m);
   }

   // Standard shaders ---------------------------------------------------------
   public static Pipeline Line2D => mLine2D ??= new ("Line2D", "Basic2D.vert", "Line2D.geom", "Line.frag");
   static Pipeline? mLine2D;

   public static Pipeline Point2D => mPoint2D ??= new ("Point2D", "Basic2D.vert", "Point2D.geom", "Point.frag");
   static Pipeline? mPoint2D;

   public static Pipeline ArrowHead => mArrowHead ??= new ("ArrowHead", "Basic2D.vert", "ArrowHead.geom", "ArrowHead.frag");
   static Pipeline? mArrowHead;

   public static Pipeline Bezier => mBezier ??= new ("Bezier", "Basic2D.vert", "Bezier.tctrl", "Bezier.teval", "Line2D.geom", "Line.frag");
   static Pipeline? mBezier;

   // Nested types ------------------------------------------------------------
   /// <summary>Provides information about a Uniform</summary>
   class UniformInfo {
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
      var assembly = Assembly.GetExecutingAssembly ();
      file = $"Nori.WGL.Res.Shader.{file}";
      using var stm = assembly.GetManifestResourceStream (file)!;
      using var reader = new StreamReader (stm);
      string text = reader.ReadToEnd ().ReplaceLineEndings ("\n");
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

   public override string ToString () => $"Shader: {Name}";
}
#endregion
