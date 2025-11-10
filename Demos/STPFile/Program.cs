using Nori;

internal class Program {
   static void Main (string[] args) {
      Lib.Init ();
      var files = Directory.GetFiles ("W:\\Step", "*.stp", SearchOption.TopDirectoryOnly);
      foreach (var file in files) {
         var step = new STEPReader (file);
         try {
            step.Parse ();
            var m = step.Build ();
            foreach (var ent in m.Ents.OfType<E3Surface> ()) _ = ent.Mesh;
            if (m.Bound.Diagonal > 1e6) throw new Exception ("Too big");
            File.Move (file, "W:\\Step\\Good\\" + Path.GetFileName (file));
         } catch (Exception e) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine (e);
            Console.ResetColor ();
            File.Move (file, "W:\\Step\\Bad\\" + Path.GetFileName (file));
         }
      }
   }

   static void Main1 () {
      Lib.Init ();
      var sr = new STEPReader ("c:/etc/boot.step");
      sr.Parse ();
      sr.Build ();
   }
}
