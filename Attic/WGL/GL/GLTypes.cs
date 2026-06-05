// ────── ╔╗
// ╔═╦╦═╦╦╬╣ GLTypes.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.WGL;

// Win32 windows handle
enum HWindow : ulong { Zero }
// Window GDI device context handle
enum HDC : ulong { Zero }
// OpenGL rendering-context handle
enum HGLRC : ulong { Zero }

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
