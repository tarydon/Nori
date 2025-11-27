namespace Nori.Internal;

enum HWindow : ulong { Zero };

enum ErrorCode {
   None = 0x0,                      // No error
   NotInitialized = 0x10001,     // GLFW not initialized
   NoCurrentContext = 0x10002,   // No GL context on the current thread
   InvalidEnum = 0x10003,        // One of the arguments to a function was an invalid enum value
   InvalidValue = 0x10004,       // One of the arguments to the function was an invalid value
   OutOfMemory = 0x10005,        // Memory allocation failed
   ApiUnavailable = 0x10006,     // GLFW could not find support for the requested API
   VersionUnavailable = 0x10007, // Requested OpenGL version (including required hints) is not available
   PlatformError = 0x10008,      // Platform-specific error occured
   FormatUnavailable = 0x10009,  // Required PIXEL format / Clipboard format not supported
   NoWindowContext = 0x1000A     // Windows passed to a function does not have an OpenGL context
}
