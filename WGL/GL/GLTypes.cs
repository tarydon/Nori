// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ GLTypes.cs
// ║║║║╬║╔╣║ Support types used for OpenGL
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region OpenGL enums -------------------------------------------------------------------------------
// The buffers we can clear with glClear
[Flags] enum EBuffer : uint { Depth = 256, Color = 16384 }

// Buffer targets for BufferData, BindBuffer etc
enum EBufferTarget : uint { Array = 0x8892, ElementArray = 0x8893 }

// Usage hint for GL.BufferData
enum EBufferUsage : uint { StaticDraw = 0x88E4 }

// Blend factors used for BlendFunc
enum EBlendFactor : uint { Zero = 0, One = 1, SrcAlpha = 770, OneMinusSrcAlpha = 771 }

// Various capabilities we can Enable / Disable
enum ECap : uint { 
   Blend = 0xBE2, DepthTest = 0xB71, PolygonOffsetFill = 0x8037, ScissorTest = 0xC11,
};

// <summary>Various data types for storing in vertex array buffers</summary>
enum EDataType : uint {
   Byte = 0x1400, UByte = 0x1401, Short = 0x1402, UShort = 0x1403, Int = 0x1404, UInt = 0x1405,
   Half = 0x140B, Float = 0x1406, Double = 0x140A, Vec2f = 0x8B50, Vec3f = 0x8B51, Vec4f = 0x8B52,
   Mat2f = 0x8B5A, Mat3f = 0x8B5B, Mat4f = 0x8B5C, IVec4 = 0x8B55, IVec2 = 0x8B53,
   Sampler2DRect = 0x8B63, Sampler2D = 0x8B5E,
}

// Data types that could be used for the indices in a DrawElement call
enum EIndexType : uint { UByte = 5121, UShort = 5123, UInt = 5125 }

// Various modes that can be passed to glBegin
enum EMode : uint { Points = 0, Lines = 1, LineLoop = 2, LineStrip = 3, Triangles = 4, Quads = 7, Patches = 14 };

// Pixel storage formats
enum EPixelFormat : uint { DepthComponent = 6402, Red = 6403, Rgba = 6408, Bgra = 32993 }
// Pixel data type
enum EPixelType : uint { UByte = 5121, Float = 5126 }
// Parameter for PixelStore
enum EPixelStoreParam : uint { UnpackAlignment = 3317, PackAlignment = 3333 }

// Values passed to GetProgram
enum EProgramParam : uint {
   InfoLogLength = 0x8B84, LinkStatus = 0x8B82, ActiveAttributes = 0x8B89, ActiveUniforms = 0x8B86
}

// Used with 'patches' type glDrawElements
enum EPatchParam : uint { PatchVertices = 36466 }

// The various types of OpenGL shaders
enum EShader : uint {
   Vertex = 0x8B31, Fragment = 0x8B30, Geometry = 0x8DD9, TessControl = 0x8E88, TessEvaluation = 0x8E87,
   Vert = Vertex, Frag = Fragment, Geom = Geometry, TCtrl = TessControl, TEval = TessEvaluation,
}

// Parameters passed to GL.GetShader
enum EShaderParam : uint { DeleteStatus = 35712, CompileStatus = 35713, InfoLogLength = 0x8B84 }

// Texture related enums
enum ETexUnit : uint { Tex0 = 33984, Tex1 = 33985, Tex2 = 33986, Tex3 = 33987 }
enum ETexTarget : uint { Texture1D = 3552, Texture2D = 3553, TexRectangle = 0x84F5 };
enum EPixelInternalFormat : uint { Red = 6403 }
enum ETexParam : uint { MagFilter = 0x2800, MinFilter = 0x2801, WrapS = 0x2802, WrapT = 0x2803 }
enum ETexFilter { Nearest = 9728, Linear = 9729 };
enum ETexWrap { Clamp = 10496, Repeat = 10497 }
#endregion

#region Strongly typed handles ---------------------------------------------------------------------
// Window GDI device context handle
enum HDC : ulong { Zero }
// OpenGL rendering-context handle
enum HGLRC : ulong { Zero };
// Win32 windows handle
enum HWindow : ulong { Zero };
// A complete OpenGL shader pipeline
enum HProgram : ulong { Zero };
// An OpenGL shader (part of a pipeline)
enum HShader : ulong { Zero }

// OpenGL VertexArrayObject (VAO)
enum HVertexArray : ulong { Zero }
// OpenGL data Buffer object 
enum HBuffer : ulong { Zero }
// Texture object, created with GenTexture
enum HTexture : ulong { Zero }
#endregion

#region struct PixelFormatDescriptor ---------------------------------------------------------------
// Structure used to describe an OpenGL pixel-format descriptor
[StructLayout (LayoutKind.Sequential)]
struct PixelFormatDescriptor {
   ushort Size, Version;
   uint Flags;
   byte PixelType, ColorBits, RedBits, RedShift, GreenBits, GreenShift, BlueBits, BlueShift;
   byte AlphaBits, AlphaShift, AccumBits, AccumRedBits, AccumGreenBits, AccumBlueBits, AccumAlphaBits;
   byte DepthBits, StencilBits, AuxBuffers, LayerType, Reserved;
   uint LayerMask, VisibleMask, DamageMask;

   // Static used to obtain a 'default' pixel-format-descriptor
   public static PixelFormatDescriptor Default {
      get {
         const uint PFD_DRAW_TO_WINDOW = 4, PFD_SUPPORT_OPENGL = 32, PFD_DOUBLEBUFFER = 1;
         PixelFormatDescriptor pfd = new () {
            Size = 40, Version = 1,
            Flags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
            ColorBits = 32, DepthBits = 32
         };
         if (40 != Marshal.SizeOf<PixelFormatDescriptor> ())
            throw new Exception ("Unexpected size for PixelFormatDescriptor");
         if (8 != Marshal.SizeOf<nint> ())
            throw new Exception ("Expecting 64-bit compilation");
         return pfd;
      }
   }
}
#endregion
