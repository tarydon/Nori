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
      _ = dwg.Layers.Current;
      var layer = dwg.Layers["0"];
   }

   static void Main1 () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      Dwg2 dwg = new ();
      // dwg.Add (new Point2 (3, 4));
      _ = dwg.Layers.Current;
      dwg.Layers.Add (new Layer2 ("DIMENSION", Color4.Blue, ELineType.Continuous));
      dwg.Layers.Add (new Layer2 ("BEND", Color4.Black, ELineType.Dot));
      dwg.Layers.Current = dwg.Layers[1];
      CurlWriter.Save (dwg, "c:/etc/test.curl");

      var dwg2 = (Dwg2)CurlReader.Load ("c:/etc/test.curl");
      CurlWriter.Save (dwg2, "c:/etc/test1.curl");

      UndoStack.Current = new ();
      dwg.Layers.Insert (0, new Layer2("SUMMAT", Color4.Random, ELineType.Dot));
      Dump (dwg);
      dwg.Layers.RemoveAt (0);
      Dump (dwg);
      dwg.Layers.RemoveAt (0);
      Dump (dwg);
      dwg.Layers.Current = dwg.Layers[1];
      dwg.Layers.RemoveAt (0);
      Dump (dwg);
      // dwg.Layers.RemoveAt (0);
      Dump (dwg);
      _ = dwg.Layers.Current;
      Dump (dwg);
   }

   static void Dump (Dwg2 dwg) {
      var stack = UndoStack.Current!;
      Console.WriteLine ($"UNDO: {stack.NextUndo?.Description}");
      Console.WriteLine ($"REDO: {stack.NextRedo?.Description}");
      for (int i = 0; i < dwg.Layers.Count; i++) {
         Console.Write ($"{i}) {dwg.Layers[i]}");
         if (dwg.Layers.Current == dwg.Layers[i]) Console.Write (" *");
         Console.WriteLine ();
      }
      Console.WriteLine ();
   }

   static void Notes (LChange<Layer2> c) {
      Console.ForegroundColor = ConsoleColor.Green;
      Console.WriteLine ($"{c.Kind} {c.Index} OLD({c.OldValue}) NEW({c.NewValue})");
      Console.ResetColor ();
   }
}
