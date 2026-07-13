namespace Nori.UX;

public class UXLayout {
   public UXLayout (string file) {
      mFile = Path.Combine (Root, file);
      FileSystemWatcher fsw = new (Root, file);
      fsw.Changed += OnChanged;
      fsw.EnableRaisingEvents = true;
   }
   readonly string mFile;

   public static void Add (UXLayout layout) => mAll.Add (layout);

   static public IReadOnlyList<UXLayout> All => mAll;
   static List<UXLayout> mAll = [];

   public Action Render {
      get {
         Current = this;
         if (mRenderFunc == null) {
            InlayGen ig = new (mFile);
            var s = ig.Generate (false);
            File.WriteAllText ($"c:/etc/{Path.GetFileNameWithoutExtension (mFile)}.cs", s);

            InlayCompiler ic = new (s);
            mRenderFunc = ic.Compile () ?? (() => { });
            foreach (var err in ic.Diagnostics) Lib.Trace (err);
         }
         return mRenderFunc;
      }
   }
   Action? mRenderFunc;

   public static void RemoveCurrent () => mAll.Remove (Current!);

   void OnChanged (object sender, FileSystemEventArgs e) {
      mRenderFunc = null; Lux.Redraw ();
   }

   public static string Root = Lib.DevRoot;
   public static UXLayout? Current;
}
