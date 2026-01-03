using Nori;
namespace ConDemo;

class Program {
   static void Main () {
      var (bArea, bFile, bId) = (0.0, "", 0);
      foreach (var file in Directory.GetFiles ("c:/etc/t3")) {
         // if (!file.Contains ("-003")) continue; 
         Console.WriteLine (file);
         var model = new T3XReader (file).Load ();
         foreach (var ent in model.Ents.OfType<E3RuledSurface> ()) {
            double area = ent.Area;
            if (area > bArea) (bArea, bFile, bId) = (area, file, ent.Id);
         }
      }
      Console.WriteLine ($"Biggest NURB {bFile} {bId}");
   }
}