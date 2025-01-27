// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ ShaderImp.cs
// ║║║║╬║╔╣║ ShaderImp is the low level wrapper around an OpenGL shader pipeline
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO;
using System.Reflection;
namespace Nori;

#region class ShaderImp ----------------------------------------------------------------------------
/// <summary>Wrapper around an OpenGL shader pipeline</summary>
class ShaderImp {
   // Constructor --------------------------------------------------------------
   /// <summary>Construct a pipeline given the code for the individual shaders</summary>
   ShaderImp (string name, EMode mode, string[] code, bool blend, bool depthTest, bool polyOffset) {
      (Name, Mode, Blending, DepthTest, PolygonOffset, Handle) 
         = (name, mode, blend, depthTest, polyOffset, GL.CreateProgram ());
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

      int cAttributes = GL.GetProgram (Handle, EProgramParam.ActiveAttributes);
      mAttributes = new AttributeInfo[cAttributes];
      for (int i = 0; i < cAttributes; i++) {
         GL.GetActiveAttrib (Handle, i, out int elems, out var type, out string aname, out int location);
         var (size, integral, dimensions, elemtype) = GetTypeInfo (type);
         mAttributes[location] = new (aname, type, size, elems, location, integral, dimensions, elemtype);
         CBVertex += size;
      }
      string vertexDesc = string.Join ('_', mAttributes.Select (a => a.Type.ToString ()));
   }
   // A cache of already compiled individual shaders 
   static Dictionary<string, HShader> sCache = [];

   // Properties ---------------------------------------------------------------
   /// <summary>The list of all the attributes used by this shader</summary>
   public IReadOnlyList<AttributeInfo> Attributes => mAttributes;

   /// <summary>Enable blending when this program is used</summary>
   public readonly bool Blending;
   /// <summary>Size of each vertex</summary>
   public readonly int CBVertex;
   /// <summary>Enable depth-testing when this program is used</summary>
   public readonly bool DepthTest;
   /// <summary>The OpenGL handle for this shader program (set up with GL.UseProgram)</summary>
   public readonly HProgram Handle;
   /// <summary>The primitive draw-mode used for this program</summary>
   public readonly EMode Mode;
   /// <summary>The name of this shader</summary>
   public readonly string Name;
   /// <summary>Enable polygon-offset-fill when this program is used</summary>
   public readonly bool PolygonOffset;

   /// <summary>The list of all the uniforms used by this shader</summary>
   public IReadOnlyList<UniformInfo> Uniforms => mUniforms;

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

   /// <summary>Select this Pipeline for use</summary>
   public void Use () => GLState.Program = this; 

   // Standard shaders ---------------------------------------------------------
   public static ShaderImp Line2D => mLine2D ??= Load ();
   public static ShaderImp Bezier2D => mBezier2D ??= Load ();
   static ShaderImp? mLine2D, mBezier2D;

   // Nested types ------------------------------------------------------------
   /// <summary>Provides information about an attribute</summary>
   public class AttributeInfo {
      internal AttributeInfo (string name, EDataType type, int size, int elems, int location, bool integral, int dimensions, EDataType elemType)
         => (Name, Type, Size, ArrayElems, Location, Integral, Dimensions, ElemType) = (name, type, size, elems, location, integral, dimensions, elemType);

      /// <summary>Name of this attribute</summary>
      public readonly string Name;
      /// <summary>Data-type for this attribute</summary>
      public readonly EDataType Type;
      /// <summary>Size of each attribute element</summary>
      public readonly int Size;
      /// <summary>Size of this attribute (array size)</summary>
      public readonly int ArrayElems;
      /// <summary>The attribute location</summary>
      public readonly int Location;
      /// <summary>Element type is integral</summary>
      public readonly bool Integral;
      /// <summary>The number of dimensions in this attribute (like Vec3f = 3, Vec4f = 4, Float = 1)</summary>
      public readonly int Dimensions;
      /// <summary>Type of each element</summary>
      public readonly EDataType ElemType;
   }

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

   static (int Size, bool Integral, int Dimensions, EDataType ElemType) GetTypeInfo (EDataType type) =>
      type switch {
         EDataType.Int => (1, true, 1, EDataType.Int),
         EDataType.IVec4 => (16, true, 4, EDataType.Int),
         EDataType.IVec2 => (8, true, 2, EDataType.Int),
         EDataType.Vec2f => (8, false, 2, EDataType.Float),
         EDataType.Vec3f => (12, false, 3, EDataType.Float),
         EDataType.Vec4f => (16, false, 4, EDataType.Float),
         _ => throw new NotImplementedException ()
      };

   // This loads the information for a particular shader from the Shader/Index.txt
   // and builds it (that index contains the list of actual vertex / geometry / fragment
   // programs)
   static ShaderImp Load ([CallerMemberName] string name = "") {
      sIndex ??= Lib.ReadLines ("wad:GL/Shader/Index.txt");
      // Each line in the index.txt contains these:
      // 0:Name  1:Mode  2:Blending  3:DepthTest  4:PolygonOffset  6:Programs
      foreach (var line in sIndex) {
         var w = line.Split (' ', StringSplitOptions.RemoveEmptyEntries);
         if (w.Length >= 6 && w[0] == name) {
            var mode = Enum.Parse<EMode> (w[1]);
            bool blending = w[2] == "1", depthtest = w[3] == "1", offset = w[4] == "1";
            var programs = w[5].Split ('|').ToArray ();
            return new (name, mode, programs, blending, depthtest, offset);
         }
      }
      throw new NotImplementedException ($"Shader {name} not found in Shader/Index.txt");
   }
   static string[]? sIndex;

   public override string ToString () {
      var sb = new StringBuilder ();
      sb.Append ($"Shader {Name}\nUniforms:\n");
      Uniforms.ForEach (a => sb.Append ($"  {a.Type} {a.Name}\n"));
      sb.Append ("Attributes:\n");
      Attributes.ForEach (a => sb.Append ($"  {a.Type} {a.Name}\n"));
      return sb.ToString ();
   }

   // Private data -------------------------------------------------------------
   AttributeInfo[] mAttributes;     // Set of attributes for this program
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

   public static Dictionary<Type, Attrib> Map = new () {
      [typeof (Vec2F)] = AVec2f, [typeof (short)] = AShort, [typeof (int)] = AInt, 
      [typeof (Vec4F)] = AVec4f, [typeof (Vec3F)] = AVec3f, [typeof (Vec3H)] = AVec3h,
      [typeof (float)] = AFloat,
   };
}
#endregion

