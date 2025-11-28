namespace Nori;
using static CallingConvention;

#region class GL -----------------------------------------------------------------------------------
public static class GL {
   // Methods ------------------------------------------------------------------                    
   /// <summary>Clears one or more buffers to preset values</summary>
   public static void Clear (EBuffer mask) => pClear (mask);
   [UnmanagedFunctionPointer (Cdecl)] delegate void glClear (EBuffer mask);
   static glClear pClear;

   /// <summary>Specifies clear values for the color buffer (components should be between 0.0 to 1.0)</summary>
   public static void ClearColor (float red, float green, float blue, float alpha) => pClearColor (red, green, blue, alpha);
   [UnmanagedFunctionPointer (Cdecl)] delegate void glClearColor (float red, float green, float blue, float alpha);
   static glClearColor pClearColor;

   // Implementation -----------------------------------------------------------
   static GL () {
      Load (out pClear); Load (out pClearColor);
   }

   static void Load<T> (out T pFunc) where T : Delegate {
      Type type = typeof (T);
      var ptr = Marshal.StringToHGlobalAnsi (type.Name);
      nint proc = GLFW.GetProcAddress (ptr);
      Marshal.FreeHGlobal (ptr);
      if (proc == 0) throw new Exception ($"OpenGL function '{type.Name}' not found.");
      Delegate del = Marshal.GetDelegateForFunctionPointer (proc, type);
      pFunc = (T)del;
   }
}
#endregion
