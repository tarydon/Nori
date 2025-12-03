// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ GLTypes.cs
// ║║║║╬║╔╣║ Support types used for OpenGL
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region OpenGL enums -------------------------------------------------------------------------------
// The buffers we can clear with glClear
[Flags] public enum EBuffer : uint { Depth = 256, Color = 16384, Stencil = 1024 }

// Buffer targets for BufferData, BindBuffer etc
public enum EBufferTarget : uint { Array = 0x8892, ElementArray = 0x8893 }

// Usage hint for GL.BufferData
public enum EBufferUsage : uint { StreamDraw = 0x88E0, StaticDraw = 0x88E4 }

// Blend factors used for BlendFunc
public enum EBlendFactor : uint { Zero = 0, One = 1, SrcAlpha = 770, OneMinusSrcAlpha = 771 }

// Various capabilities we can Enable / Disable
public enum ECap : uint { 
   Blend = 0xBE2, DepthTest = 0xB71, PolygonOffsetFill = 0x8037, ScissorTest = 0xC11,
   CullFace = 0xB44, StencilTest = 0xB90, PrimitiveRestart = 0x8F9D
}

// Various data types for storing in vertex array buffers
public enum EDataType : uint {
   Byte = 0x1400, UByte = 0x1401, Short = 0x1402, UShort = 0x1403, Int = 0x1404, UInt = 0x1405,
   Half = 0x140B, Float = 0x1406, Double = 0x140A, Vec2f = 0x8B50, Vec3f = 0x8B51, Vec4f = 0x8B52,
   Mat2f = 0x8B5A, Mat3f = 0x8B5B, Mat4f = 0x8B5C, IVec4 = 0x8B55, IVec2 = 0x8B53,
   Sampler2DRect = 0x8B63, Sampler2D = 0x8B5E
}

// Binding targets for a FrameBuffer
public enum EFrameBufferTarget : uint { Draw = 0x8CA9, Read = 0x8CA8, DrawAndRead = 0x8D40 }

// Defines a frame-buffer attachment point
public enum EFrameBufferAttachment { Depth = 0x8D00, Color0 = 0x8CE0, Color1 = 0x8CE1, DepthStencil = 0x821A }

// Values returned by CheckFrameBufferStatus
public enum EFrameBufferStatus { Complete = 0x8CD5 }

// Data types that could be used for the indices in a DrawElement call
public enum EIndexType : uint { UByte = 5121, UShort = 5123, UInt = 5125 }

// Values to pass to GL.StencilOpSeparate, GL.StencilFuncSeparate
public enum EFace : uint { Front = 0x404, Back = 0x405, FrontAndBack = 0x408 }

// Access types for MapBufferRange
[Flags]
public enum EMapAccess : uint {
   Read = 0x1, Write = 0x2, Persistent = 0x40, Coherent = 0x80, InvalidateRange = 0x4,
   InvalidateBuffer = 0x8, FlushExplicit = 0x10, Unsynchronized = 0x20,
}

// Various modes that can be passed to glBegin
public enum EMode : uint { 
   Points = 0, Lines = 1, LineLoop = 2, LineStrip = 3, Triangles = 4, TriangleFan = 6,
   TriangleStrip = 5, Quads = 7, Patches = 14 
}

/// <summary>Various shading modes to pass to Lux.Mesh(...)</summary>
public enum EShadeMode { Flat, Gourad, Phong, Glass }

// Pixel storage formats
public enum EPixelFormat : uint { 
   DepthComponent = 0x1902, Red = 0x1903, RGB = 0x1907, RGBA = 0x1908, BGRA = 32993 
}
// Pixel data type
public enum EPixelType : uint { Byte = 5120, UByte = 5121, Float = 5126 }

// Parameter for PixelStore
public enum EPixelStoreParam : uint { UnpackAlignment = 3317, PackAlignment = 3333 }

// Values passed to GetProgram
public enum EProgramParam : uint {
   InfoLogLength = 0x8B84, LinkStatus = 0x8B82, ActiveAttributes = 0x8B89, ActiveUniforms = 0x8B86
}

// Various Primitive types used in tessellation
enum EPrimitive { Triangles = 0x0004, TriangleStrip = 0x0005, TriangleFan = 0x0006 }

// Used with 'patches' type glDrawElements
public enum EPatchParam : uint { PatchVertices = 36466 }

// Binding targets for a RenderBuffer
public enum ERenderBufferTarget : uint { RenderBuffer = 0x8D41 }

// Storage formats in render buffer
public enum ERenderBufferFormat : uint  { RGBA8 = 0x8058, Depth32 = 0x81A7, Depth16 = 0x81A5, Depth24Stencil8 = 0x88F0 }

// The various types of OpenGL shaders
public enum EShader : uint {
   Vertex = 0x8B31, Fragment = 0x8B30, Geometry = 0x8DD9, TessControl = 0x8E88, TessEvaluation = 0x8E87,
   Vert = Vertex, Frag = Fragment, Geom = Geometry, TCtrl = TessControl, TEval = TessEvaluation
}

// Parameters passed to GL.GetShader
public enum EShaderParam : uint { DeleteStatus = 35712, CompileStatus = 35713, InfoLogLength = 0x8B84 }

// Parameter for GL.StencilFunc
public enum EStencilFunc : uint { 
   Never = 0x200, Less = 0x201, LEqual = 0x203, Greater = 0x204, GEqual = 0x206, 
   Equal = 0x202, NotEqual = 0x205, Always = 0x207 
}
// Parameters passed to GL.StencilOp
public enum EStencilOp : uint { Keep = 0x1e00, Zero = 0, Replace = 0x1e01, Incr = 0x1e02, Decr = 0x1e03, Invert = 0x150a }

// Texture related enums
public enum ETexUnit : uint { Tex0 = 33984, Tex1 = 33985, Tex2 = 33986, Tex3 = 33987 }
public enum ETexTarget : uint { Texture1D = 3552, Texture2D = 3553, TexRectangle = 0x84F5 }
public enum EPixelInternalFormat : uint { Red = 6403 }
public enum ETexParam : uint { MagFilter = 0x2800, MinFilter = 0x2801, WrapS = 0x2802, WrapT = 0x2803 }
public enum ETexFilter { Nearest = 9728, Linear = 9729 }
public enum ETexWrap { Clamp = 10496, Repeat = 10497 }
// Enumeration for the winding-rule to be used in polygon tessellation and boolean operations
public enum EWindingRule { Odd = 100130, NonZero = 100131, Positive = 100132, AbsGeqTwo = 100134 }

#endregion

#region Strongly typed handles ---------------------------------------------------------------------
// Window GDI device context handle
enum HDC : ulong { Zero }
// OpenGL rendering-context handle
public enum HGLRC : ulong { Zero }
// Win32 windows handle
enum HWindow : ulong { Zero }
// A complete OpenGL shader pipeline
public enum HProgram : ulong { Zero }
// An OpenGL shader (part of a pipeline)
public enum HShader : ulong { Zero }

// OpenGL VertexArrayObject (VAO)
public enum HVertexArray : ulong { Zero }
// OpenGL data Buffer object 
public enum HBuffer : ulong { Zero }
// OpenGL frame-buffer object
public enum HFrameBuffer : ulong { Zero }
// OpenGL render-buffer object
public enum HRenderBuffer : ulong { Zero }
// Tessellator object used by GL based tessellators.
enum HTesselator : ulong { Zero }
// Texture object, created with GenTexture
public enum HTexture : ulong { Zero }
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
            ColorBits = 32, DepthBits = 32, StencilBits = 8
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
