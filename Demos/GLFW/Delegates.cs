using static System.Runtime.InteropServices.CallingConvention;
namespace Nori.Internal;

// Signature for receiving error callbacks
[UnmanagedFunctionPointer (Cdecl)]
delegate void ErrorCallback (ErrorCode code, IntPtr message);
