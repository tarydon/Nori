// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      Dwg2 dwg = new ();
      dwg.Add (new Point2 (3, 4));
      dwg.Layers.Add (new Layer2 ("DIMENSION", Color4.Blue, ELineType.Continuous));
      dwg.Layers.Add (new Layer2 ("BEND", Color4.Black, ELineType.Dot));
      dwg.Layers.Current = dwg.Layers[1];
      CurlWriter.Save (dwg, "c:/etc/test.curl");

      var dwg2 = (Dwg2)CurlReader.Load ("c:/etc/test.curl");
      CurlWriter.Save (dwg2, "c:/etc/test1.curl");

      var ustack = UndoStack.Current = new ();
      dwg.Layers.Add (new Layer2 ("MBEND", Color4.Red, ELineType.Phantom));     

      Console.WriteLine ("LIST");
      for (int i = 0; i < dwg.Layers.Count; i++)
         Console.WriteLine ($"{i}) {dwg.Layers[i]}");
      Console.WriteLine ($"CURRENT: {dwg.Layers.Current}");
      Console.WriteLine ($"Layers[\"BEND\"] = {dwg.Layers["BEND"]}");
      Console.WriteLine ("UPDATE CURRENT");
      dwg.Layers.Current = dwg.Layers[0];
      Console.WriteLine ($"CURRENT: {dwg.Layers.Current}");
      dwg.Layers[2] = new Layer2 ("SOMETHING", Color4.Red, ELineType.Continuous);
      dwg.Layers.RemoveAt (3);
   }

   static void Notes (LChange<Layer2> c) {
      Console.ForegroundColor = ConsoleColor.Green;
      Console.WriteLine ($"{c.Kind} {c.Index} OLD({c.OldValue}) NEW({c.NewValue})");
      Console.ResetColor ();
   }
}
