namespace Nori.Inlay;

public class Part {
   public bool Is2D => true;
   public bool Is3D => false;
}

public class InlayHub {
   public static void DoFileNew () { }
   public static void DoFileOpen (string? file = null) { }
   public static void DoFileSave () { }
   public static void DoFileExport (string fmt) { }
   public static Part? CurrentPart { get; set; }
   public static List<string> MRUList = [];
}
