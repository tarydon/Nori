using Nori.UX;
namespace Nori.Inlay;

public class Part {
   public bool Is2D => false;
   public bool Is3D => true;
}

public class InlayHub {
   // Menu ----------------
   public static void DoFileNew () => Lib.Trace ("FILE/NEW");
   public static void DoFileOpen (string? file = null) => Lib.Trace ($"FILE/OPEN({file})");
   public static void DoFileSave () => Lib.Trace ("FILE/SAVE");
   public static void DoFileExport (string fmt) => Lib.Trace ($"FILE/EXPORT({fmt})");
   public static void DoExit () => Lib.Trace ("FILE/EXIT");

   public static void DoUndo () => Lib.Trace ($"UNDO {UndoStack[NUndo--]}");
   public static void DoRedo () => Lib.Trace ($"REDO {UndoStack[++NUndo]}");
   public static void DoCut () => Lib.Trace ("EDIT/CUT");
   public static void DoCopy () => Lib.Trace ("EDIT/COPY");
   public static void DoPaste () => Lib.Trace ("EDIT/PASTE");

   public static Part? CurrentPart { get; set; } = new Part ();
   public static List<string> MRUList = ["C:/Etc/Demo.fx", "C:/Parts/Assy/T142.step"];
   public static List<string> UndoStack = ["Clear Screen", "Draw Poly", "Close Poly", "Prep Laser"];
   public static int NUndo = 2;  // Next operation to be undone

   public static string? NextUndo => UndoStack.SafeGet (NUndo);
   public static string? NextRedo => UndoStack.SafeGet (NUndo + 1);

   public static void DoHelpAbout () => UXLayout.Add (new UXLayout ("dialog.in"));

   // Dialog ---------------
   public static bool UseRayTracer { get; set { field = value; Lib.Trace ($"UseRayTracer = {value}"); } }
   public static bool AmbientOcclusion { get; set { field = value; Lib.Trace ($"AmbientOcculsion = {value}"); } } = true;
   public static int ShaderType { get; set { field = value; Lib.Trace ($"ShaderType = {value}"); } } = 2;
}
