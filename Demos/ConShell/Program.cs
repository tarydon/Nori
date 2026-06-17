// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Linq.Expressions;
using System.Text;
using Nori;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
      Lib.Testing = true;
      Test2 ();
   }

   static void Test2 () {
      Dwg2 dwg = new ();
      _ = dwg.Layers.Current;
      dwg.Add (new Point2 (1, 2));

      List<string> changes = [];
      List<Exception> exceptions = [];
      dwg.Layers.Changes.Subscribe (OnChange);

      UndoStack.Current = new ();
      dwg.Layers.Add (new ("TRIAL", Color4.Red, ELineType.Dot));
      Dump ("Add layer TRIAL");

      var layer = dwg.Layers["TRIAL"]!;
      Console.WriteLine (layer);

      try { dwg.Layers.RemoveAt (0); } catch (Exception e) { exceptions.Add (e); }
      Dump ("TRY remove current layer");

      dwg.Layers.Current = dwg.Layers[1];
      Dump ("Change current layer");

      try { dwg.Layers.Remove (dwg.Layers["0"]!); } catch (Exception e) { exceptions.Add (e); }
      Dump ("TRY remove in-use layer");

      UndoStack.Current.Push (new ClubbedStep (dwg, "INSERT+CHANGE"));
      dwg.Layers.Insert (0, new Layer2 ("BEND", Color4.Green, ELineType.Dash));
      dwg.Layers.Current = dwg.Layers["0"]!;
      UndoStack.Current.ClubSteps ();
      Dump ("INSERT layer, change current layer");

      try { dwg.Layers.Add (new Layer2 ("", Color4.Blue, ELineType.Border)); } catch (Exception e) { exceptions.Add (e); }
      Dump ("TRY add invalid layer");

      try { dwg.Layers.Add (new Layer2 ("BEND", Color4.DarkBlue, ELineType.Border)); } catch (Exception e) { exceptions.Add (e); }
      Dump ("TRY add duplicate layer");

      dwg.Layers.RemoveAt (0);
      Dump ("Remove a layer");

      UndoStack.Current.Push (new ClubbedStep (dwg, "REPLACE-LAYER"));
      dwg.Layers[0] = new Layer2 ("STANDARD", Color4.White, ELineType.Continuous);
      UndoStack.Current.ClubSteps ();
      Dump ("Replace a layer");

      void OnChange (LChange<Layer2> ch) 
         => changes.Add (ch.UndoDescription);

      void Dump (string s) {
         var sb = Compose (s);
         Console.WriteLine (sb.ToString ());
         Console.ReadKey ();
      }

      StringBuilder Compose (string desc) {
         var sb = new StringBuilder ();
         sb.AppendLine (desc);
         sb.AppendLine (new string ('-', desc.Length));

         if (exceptions.Count > 0) {
            sb.AppendLine ("EXCEPTIONS:");
            foreach (var e in exceptions) sb.AppendLine (e.Message);
            exceptions.Clear ();
            return sb;
         }

         if (changes.Count > 0) {
            sb.AppendLine ("CHANGES:");
            foreach (var s in changes) sb.AppendLine (s);
            changes.Clear (); 
            sb.AppendLine ();
         }
         sb.AppendLine ("UNDO:");
         sb.AppendLine (UndoStack.Current?.NextUndo?.Description);
         sb.AppendLine ();

         sb.AppendLine ("DWG:");
         sb.AppendLine (Encoding.UTF8.GetString (CurlWriter.SaveToByteArray (dwg)));
         
         changes.Clear ();
         exceptions.Clear ();
         return sb;
      }
   }

   static void Test1 () {
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
