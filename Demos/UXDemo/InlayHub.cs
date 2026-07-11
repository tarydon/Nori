namespace Nori.Inlay;

public class Part {
   public bool Is2D => true;
   public bool Is3D => false;
}

public class InlayHub {
   public static void DoFileNew () => Lib.Trace ("FILE/NEW");
   public static void DoFileOpen (string? file = null) => Lib.Trace ($"FILE/OPEN({file})");
   public static void DoFileSave () => Lib.Trace ("FILE/SAVE");
   public static void DoFileExport (string fmt) => Lib.Trace ($"FILE/EXPORT({fmt})");
   public static Part? CurrentPart { get; set; } = new Part ();
   public static List<string> MRUList = ["C:/Etc/Demo.fx", "C:/Parts/Assy/T142.step"];
}
