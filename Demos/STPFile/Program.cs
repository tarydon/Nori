using Nori;

internal class Program {
   static void Main (string[] args) {
      Lib.Init ();
      var files = Directory.GetFiles ("C:\\Step", "*.step", SearchOption.TopDirectoryOnly);
      foreach (var file in files) {
         var step = new STEPReader (file);
         try {
            step.Parse ();
            File.Move (file, "C:\\Step\\Good\\" + Path.GetFileName (file));
         } catch (Exception e) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine (e);
            Console.ResetColor ();
            // File.Move (file, "C:\\Step\\Bad\\" + Path.GetFileName (file));
            return;
         }
      }
   }
}
