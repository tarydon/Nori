namespace Nori;

#region Enumerations -------------------------------------------------------------------------------
/// <summary>Buffers we can clear with GL.Clear()</summary>
[Flags]
public enum EBuffer : uint {
   /// <summary>The depth buffer</summary>
   Depth = 0x100,
   /// <summary>The stencil buffer</summary>
   Stencil = 0x400,
   /// <summary>The buffers enabled for color writing</summary>
   Color = 0x4000,
}
#endregion
