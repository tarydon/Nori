using System.Text;
using Nori;

internal class Program {
   static void Main1 (string[] args) {
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

   static void Main () {
      Lib.Init ();
      Lib.Testing = true;
      var sr = new STEPReader ("N:/TData/STEP/S00178.stp");
      sr.Parse ();
      var model = sr.Build ();
      var b = model.Bound;
      CurlWriter.Save (model, "c:/etc/test.curl", "S00178.stp");

      var meshes = model.Ents.OfType<E3Surface> ().Select (a => a.Mesh).ToList ();
      var sb = new StringBuilder ();
      foreach (var ent in model.Ents.OfType<E3Surface> ()) {
         var mesh = ent.Mesh;
         if (mesh.Triangle.Length == 0) {
            Console.Write ("X");
         }
         sb.Append (mesh.ToTMesh ()); sb.AppendLine ("--------------------"); 
      }
      File.WriteAllText ("c:/etc/test1.txt", sb.ToString ());
   }
}
