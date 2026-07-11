using Nori;
using Nori.UX;
namespace UXDemo;

public static class UXCompile {
   static public void Render () {
      if (mRender == null) {
         FileSystemWatcher fsw = new ("N:/Demos/UXDemo/Inlay", "*.*");
         fsw.Changed += OnChanged;
         fsw.EnableRaisingEvents = true;

         string file = "N:/Demos/UXDemo/Inlay/root.in";
         //file = "c:/etc/zero.in";
         InlayGen ig = new (file);
         var s = ig.Generate ();
         File.WriteAllText ("c:/etc/output.cs", s);

         InlayCompiler ic = new (s);
         mRender = ic.Compile () ?? (() => { });
         foreach (var err in ic.Diagnostics) Lib.Trace (err);
      }
      mRender ();
   }

   static void OnChanged (object sender, FileSystemEventArgs e) {
      mRender = null; Lux.Redraw ();
   }

   static Action? mRender;
}
