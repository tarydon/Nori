namespace Nori.Doc;

class Program {
   static void Main () {
      Project p = new ("N:/Doc/Gen/Nori.fdoc");
      p.Process ();
   }

   // Prints error and stops
   [DoesNotReturn]
   public static void Fatal (string s) {
      Console.WriteLine (s);
      Environment.Exit (-1);
   }
}
