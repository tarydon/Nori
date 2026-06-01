// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ Misc2.cs
// ║║║║╬║╔╣║ Further miscellaneous tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (17, "Miscellaneous tests 2", "Misc")]
class Misc2 {
   [Test (53, "Eval: expression evaluation tests 1")]
   void Test1 () {
      Eval e = new ();
      e.TryEvaluate ("123", out double v).IsTrue (); v.Is (123);
      e.TryEvaluate ("-123", out v).IsTrue (); v.Is (-123);
      e.TryEvaluate ("123 - -123", out v).IsTrue (); v.Is (246);
      e.TryEvaluate ("123 + -123", out v).IsTrue (); v.Is (0);
      e.TryEvaluate ("10.5 - 4.2 - 6.3", out v).IsTrue (); v.Is (0);
      e.TryEvaluate ("10 / 2 / 4", out v).IsTrue (); v.Is (1.25);

      e.TryEvaluate ("1.2.3", out _).IsFalse ();
      e.TryEvaluate ("123 - * 123", out _).IsFalse ();
      e.TryEvaluate ("123 456", out _).IsFalse ();
      e.TryEvaluate ("- *", out _).IsFalse ();
   }

   [Test (54, "Eval: expression evaluation tests 2")]
   void Test2 () {
      Eval e = new ();
      e.TryEvaluate ("asin (sin 45)", out double v).IsTrue (); v.Is (45);
      e.TryEvaluate ("acos (cos 45)", out v).IsTrue (); v.Is (45);
      e.TryEvaluate ("atan (tan 45)", out v).IsTrue (); v.Is (45);
      e.TryEvaluate ("sin -90", out v).IsTrue (); v.Is (-1);
      e.TryEvaluate ("sin (-90)", out v).IsTrue (); v.Is (-1);
      double atan = Math.Atan2 (20, 30).R2D ();
      e.TryEvaluate ("atan2 20 30", out v).IsTrue (); v.Is (atan);
      e.TryEvaluate ("atan2 (20 30)", out v).IsTrue (); v.Is (atan);
      e.TryEvaluate ("atan2 (20, 30)", out v).IsTrue (); v.Is (atan);
      e.TryEvaluate ("log 10", out v).IsTrue (); v.Is (1);
      e.TryEvaluate ("exp 1", out v).IsTrue (); v.Is (Math.E);
      e.TryEvaluate ("sqr (sqrt 4)", out v).IsTrue (); v.Is (4);
      e.TryEvaluate ("abs (sin -90)", out v).IsTrue (); v.Is (1);
   }

   [Test (115, "Eval: exceptions during expression evaluation")]
   void Test3 () {
      Eval e = new ();
      e.TryEvaluate ("1 + 2 +", out _).IsFalse ();
      e.TryEvaluate ("1 + 2 3", out _).IsFalse ();
      e.TryEvaluate ("1 * (2 + 3", out _).IsFalse ();
   }

   [Test (169, "UndoStep.Push with no UndoStack in place")]
   void Test4 () {
      Dwg2 dwg = new ();
      var p1 = new E2Poly (dwg.Layers.Current, Poly.Line (new (0, 0), new (10, 0)));
      new ModifyDwgEnts (dwg, "Add Line", [p1], []).Push ();
      dwg.Ents.Count.Is (1);

      var p2 = new E2Poly (dwg.Layers.Current, Poly.Line (new (10, 0), new (10, 5)));
      new ModifyDwgEnts (dwg, "Add Line2", [p2], []).Push ();
      dwg.Ents.Count.Is (1);
      var e2p = (E2Poly)dwg.Ents[0];
      e2p.Poly.Is ("M0,0H10V5");
   }

   [Test (170, "UndoStep.Push with UndoStack in place")]
   void Test5 () {
      try {
         Dwg2 dwg = new ();
         var stack = UndoStack.Current = new (); 
         var p0 = new E2Poly (dwg.Layers.Current, Poly.Circle (Point2.Zero, 10));
         new ModifyDwgEnts (dwg, "Add Circle", [p0], []).Push ();
         dwg.Ents.Count.Is (1);
         stack.NextUndo?.Description.Is ("Add Circle");
         (stack.NextRedo == null).IsTrue ();

         var p1 = new E2Poly (dwg.Layers.Current, Poly.Line (new (0, 0), new (10, 0)));
         new ModifyDwgEnts (dwg, "Add Line", [p1], [p0]).Push ();
         dwg.Ents.Count.Is (1);
         var e2p = (E2Poly)dwg.Ents[0]; e2p.Poly.Is ("M0,0H10");

         // Add a "BEND" layer
         stack.NextDescription = "Add Bend Layer";
         var bend = new Layer2 ("Bend", Color4.Blue, ELineType.Dot);
         dwg.Layers.Add (bend); 
         dwg.Layers.Count.Is (2);
         var mbend = new Layer2 ("MBend", Color4.Green, ELineType.Dash);
         stack.NextDescription = "Add MBend Layer";
         dwg.Layers[1] = mbend;
         dwg.Layers.Count.Is (2);
         dwg.Layers[1].Name.Is ("MBend");

         var p3 = new E2Poly (mbend, Poly.Rectangle (0, 0, 10, 5));
         new ModifyDwgEnts (dwg, "Add Rect", [p3], []).Push ();
         dwg.Ents.Count.Is (2);

         stack.NextUndo?.Description.Is ("Add Rect");
         stack.Undo (); // Add Rect
         stack.Undo (); // Replace layer Bend with MBend
         stack.Undo (); // Add layer Bend
         stack.NextUndo?.Description.Is ("Add Line");
         stack.NextRedo?.Description.Is ("Add Bend Layer");
         stack.Redo ();
         stack.Redo ();
         stack.NextUndo?.Description.Is ("Add MBend Layer");
         stack.NextRedo?.Description.Is ("Add Rect");

         var p4 = new E2Poly (dwg.Layers.Current, Poly.Circle (Point2.Zero, 5));
         new ModifyDwgEnts (dwg, "Add Circle2", [p4], []).Push ();
         (stack.NextRedo == null).IsTrue ();
         stack.NextUndo?.Description.Is ("Add Circle2");
      } finally {
         UndoStack.Current = null;
      }
   }

   [Test (171, "ClubbedStep tests")]
   void Test6 () {
      try {
         Dwg2 dwg = new ();
         var stack = UndoStack.Current = new ();
         var p0 = new E2Poly (dwg.Layers.Current, Poly.Circle (Point2.Zero, 10));
         new ModifyDwgEnts (dwg, "Add Circle", [p0], []).Push ();
         dwg.Ents.Count.Is (1);

         // No steps to club, ClubSteps will be a no-op
         new ClubbedStep (dwg, "Empty").Push ();
         stack.ClubSteps ();
         stack.NextUndo?.Description.Is ("Add Circle");
         // One step to club, ClubSteps will still wrap around it, presumably because
         // the ClubbedStep provides a better Undo description than the step inside
         new ClubbedStep (dwg, "OneStep").Push ();
         var p1 = new E2Poly (dwg.Layers.Current, Poly.Circle (Point2.Zero, 5));
         new ModifyDwgEnts (dwg, "Add Circle2", [p1], []).Push ();
         stack.ClubSteps ();
         dwg.Ents.Count.Is (2);
         stack.NextUndo?.Description.Is ("OneStep");

         // Club with 2 steps
         new ClubbedStep (dwg, "Add Layer and Line").Push ();
         var bend = new Layer2 ("Bend", Color4.Blue, ELineType.Dot);
         dwg.Layers.Add (bend);
         var p2 = new E2Poly (bend, Poly.Line (new (0, 0), new (10, 0)));
         new ModifyDwgEnts (dwg, "Add Line", [p2], []).Push ();
         stack.ClubSteps ();

         dwg.Ents.Count.Is (3); dwg.Layers.Count.Is (2);
         stack.NextUndo?.Description.Is ("Add Layer and Line");
         stack.Undo ();
         dwg.Ents.Count.Is (2); dwg.Layers.Count.Is (1);
         stack.NextUndo?.Description.Is ("OneStep");
         stack.NextRedo?.Description.Is ("Add Layer and Line");
         stack.Redo ();
         dwg.Ents.Count.Is (3); dwg.Layers.Count.Is (2);
      } finally {
         UndoStack.Current = null;
      }
   }

   [Test (255, "Test of Ledger<T> type")]
   void Test7 () {
      Dwg2 dwg = new Dwg2 ();
      _ = dwg.Layers.Current;
      dwg.Add (new Point2 (1, 2));

      try {
         List<string> changes = [];
         List<Exception> exceptions = [];
         dwg.Layers.Changes.Subscribe (OnChange);
         UndoStack.Current = new ();

         DumpDwg ("START");

         // 1. Add layer
         dwg.Layers.Add (new ("TRIAL", Color4.Red, ELineType.Dot));
         Dump ("ADD", "Add Layer TRIAL", true);

         // 2. Check dictionary access
         object.ReferenceEquals (dwg.Layers["TRIAL"], dwg.Layers[1]).IsTrue ();

         // 3. TRY remove current layer
         try { dwg.Layers.RemoveAt (0); } catch (Exception e) { exceptions.Add (e); }
         Dump ("REMOVE-CURRENT", "TRY remove current layer", false);

         // 4. Change current layer
         dwg.Layers.Current = dwg.Layers[1];
         Dump ("SET-CURRENT", "Change current layer", true);

         // 5. TRY remove in-use layer
         try { dwg.Layers.RemoveAt (0); } catch (Exception e) { exceptions.Add (e); }
         Dump ("REMOVE-INUSE", "TRY remove in-use layer", false);

         // 6. Insert layer, change current layer
         UndoStack.Current.Push (new ClubbedStep (dwg, "INSERT+CHANGE"));
         dwg.Layers.Insert (0, new Layer2 ("BEND", Color4.Green, ELineType.Dash));
         dwg.Layers.Current = dwg.Layers["0"]!;
         UndoStack.Current.ClubSteps ();
         Dump ("INSERT+CHANGE", "Insert layer, change current layer", true);

         // 7. TRY add invalid layer
         try { dwg.Layers.Add (new Layer2 ("", Color4.Blue, ELineType.Border)); } catch (Exception e) { exceptions.Add (e); }
         Dump ("BADNAME", "Add layer with bad name", false);

         // 8. TRY add duplicate layer
         try { dwg.Layers.Add (new Layer2 ("BEND", Color4.DarkBlue, ELineType.Border)); } catch (Exception e) { exceptions.Add (e); }
         Dump ("DUPLICATE", "Add layer with duplicate name", false);

         // 9. Remove a layer
         dwg.Layers.RemoveAt (0);
         Dump ("REMOVE", "Remove a layer", true);

         // 10. Remove a layer
         UndoStack.Current.Push (new ClubbedStep (dwg, "REPLACE-LAYER"));
         dwg.Layers[0] = new Layer2 ("STANDARD", Color4.White, ELineType.Continuous);
         UndoStack.Current.ClubSteps ();
         Dump ("REPLACE", "Replace a layer", true);

         // 11. Few other exceptions
         dwg.Layers[1] = new Layer2 ("TRIAL", Color4.Red, ELineType.Dot);  // Should be fine!
         UndoStack.Current.Undo ();
         try { dwg.Layers[0] = new Layer2 ("TRIAL", Color4.Black, ELineType.Dot); } catch (Exception e) { exceptions.Add (e); }
         try { dwg.Layers.Add (null!); } catch (Exception e) { exceptions.Add (e); }
         try { dwg.Layers.Insert (0, new Layer2 (" ", Color4.Black, ELineType.Dot)); } catch (Exception e) { exceptions.Add (e); }
         Dump ("EXCEPT", "Exceptions", true);

         Undo ("REMOVE");
         Undo ("INSERT+CHANGE");
         Undo ("SET-CURRENT");
         Undo ("ADD");
         Undo ("START");
         (UndoStack.Current.NextUndo == null).IsTrue ();
         Redo ("ADD");
         Redo ("SET-CURRENT");
         Redo ("INSERT+CHANGE");
         Redo ("REMOVE");
         Redo ("REPLACE");
         Redo ("EXCEPT");
         (UndoStack.Current.NextRedo == null).IsTrue ();

         void OnChange (LChange<Layer2> c)
            => changes.Add (c.UndoDescription);

         void Dump (string file, string desc, bool dwgOut) {
            var sb = Compose (desc);
            File.WriteAllText (NT.TmpTxt, sb.ToString ());
            Assert.TextFilesEqual (NT.File ($"Misc/Ledger/{file}.txt"), NT.TmpTxt);
            if (dwgOut) DumpDwg (file);
         }

         StringBuilder Compose (string desc) {
            var sb = new StringBuilder ();
            sb.AppendLine (desc).AppendLine (new string ('-', desc.Length));
            if (exceptions.Count > 0) {
               foreach (var e in exceptions.OfType<NoriException> ()) sb.AppendLine (e.Code.ToString ());
               exceptions.Clear ();
               return sb;
            }

            if (changes.Count > 0) {
               sb.AppendLine ("CHANGES:");
               foreach (var s in changes) sb.AppendLine (s);
               changes.Clear ();
               sb.AppendLine ();
            }

            sb.Append ("UNDO: ");
            sb.AppendLine (UndoStack.Current?.NextUndo?.Description);
            return sb;
         }

         void Undo (string file) {
            UndoStack.Current?.Undo ().IsTrue ();
            DumpDwg (file);
         }

         void Redo (string file) {
            UndoStack.Current?.Redo ().IsTrue ();
            DumpDwg (file);
         }

         void DumpDwg (string file) {
            var dwgCurl = Encoding.UTF8.GetString (CurlWriter.SaveToByteArray (dwg));
            File.WriteAllText (NT.TmpCurl, dwgCurl);
            Assert.TextFilesEqual (NT.File ($"Misc/Ledger/{file}.curl"), NT.TmpCurl);
         }
      } finally {
         UndoStack.Current = null;
      }
   }
}
