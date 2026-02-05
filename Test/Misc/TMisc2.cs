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
      e.TryEvaluate ("1 + 2 +", out double v).IsFalse ();
      e.TryEvaluate ("1 + 2 3", out v).IsFalse ();
      e.TryEvaluate ("1 * (2 + 3", out v).IsFalse ();
   }

   [Test (169, "UndoStep.Push with no UndoStack in place")]
   void Test4 () {
      Dwg2 dwg = new ();
      var p1 = new E2Poly (dwg.CurrentLayer, Poly.Line (new (0, 0), new (10, 0)));
      new ModifyDwgEnts (dwg, "Add Line", [p1], []).Push ();
      dwg.Ents.Count.Is (1);

      var p2 = new E2Poly (dwg.CurrentLayer, Poly.Line (new (10, 0), new (10, 5)));
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
         var p0 = new E2Poly (dwg.CurrentLayer, Poly.Circle (Point2.Zero, 10));
         new ModifyDwgEnts (dwg, "Add Circle", [p0], []).Push ();
         dwg.Ents.Count.Is (1);
         stack.NextUndo?.Description.Is ("Add Circle");
         (stack.NextRedo == null).IsTrue ();

         var p1 = new E2Poly (dwg.CurrentLayer, Poly.Line (new (0, 0), new (10, 0)));
         new ModifyDwgEnts (dwg, "Add Line", [p1], [p0]).Push ();
         dwg.Ents.Count.Is (1);
         var e2p = (E2Poly)dwg.Ents[0]; e2p.Poly.Is ("M0,0H10");

         var l1 = new Layer2 ("Bend", Color4.Blue, ELineType.Dot);
         new ModifyDwgLayers (dwg, "Add Bend Layer", [l1], []).Push ();
         dwg.Layers.Count.Is (2);
         var l2 = new Layer2 ("MBend", Color4.Green, ELineType.Dash);
         new ModifyDwgLayers (dwg, "Add MBend Layer", [l2], [l1]).Push ();
         dwg.Layers.Count.Is (2);
         dwg.Layers[1].Name.Is ("MBend");

         var p3 = new E2Poly (l2, Poly.Rectangle (0, 0, 10, 5));
         new ModifyDwgEnts (dwg, "Add Rect", [p3], []).Push ();
         dwg.Ents.Count.Is (2);

         stack.Undo (); // Add Rect
         stack.Undo (); // Replace layer Bend with MBend
         stack.Undo (); // Add layer Bend
         stack.NextUndo?.Description.Is ("Add Line");
         stack.NextRedo?.Description.Is ("Add Bend Layer");
         stack.Redo ();
         stack.Redo ();
         stack.NextUndo?.Description.Is ("Add MBend Layer");
         stack.NextRedo?.Description.Is ("Add Rect");

         var p4 = new E2Poly (dwg.CurrentLayer, Poly.Circle (Point2.Zero, 5));
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
         var p0 = new E2Poly (dwg.CurrentLayer, Poly.Circle (Point2.Zero, 10));
         new ModifyDwgEnts (dwg, "Add Circle", [p0], []).Push ();
         dwg.Ents.Count.Is (1);

         // No steps to club, ClubSteps will be a no-op
         new ClubbedStep (dwg, "Empty").Push ();
         stack.ClubSteps ();
         stack.NextUndo?.Description.Is ("Add Circle");
         // One step to club, ClubSteps will simply keep that step
         new ClubbedStep (dwg, "OneStep").Push ();
         var p1 = new E2Poly (dwg.CurrentLayer, Poly.Circle (Point2.Zero, 5));
         new ModifyDwgEnts (dwg, "Add Circle2", [p1], []).Push ();
         stack.ClubSteps ();
         dwg.Ents.Count.Is (2);
         stack.NextUndo?.Description.Is ("Add Circle2");

         // Club with 2 steps
         new ClubbedStep (dwg, "Add Layer and Line").Push ();
         var l1 = new Layer2 ("Bend", Color4.Blue, ELineType.Dot);
         new ModifyDwgLayers (dwg, "Add Layer", [l1], []).Push ();
         var p2 = new E2Poly (l1, Poly.Line (new (0, 0), new (10, 0)));
         new ModifyDwgEnts (dwg, "Add Line", [p2], []).Push ();
         stack.ClubSteps ();

         dwg.Ents.Count.Is (3); dwg.Layers.Count.Is (2);
         stack.NextUndo?.Description.Is ("Add Layer and Line");
         stack.Undo ();
         dwg.Ents.Count.Is (2); dwg.Layers.Count.Is (1);
         stack.NextUndo?.Description.Is ("Add Circle2");
         stack.NextRedo?.Description.Is ("Add Layer and Line");
         stack.Redo ();
         dwg.Ents.Count.Is (3); dwg.Layers.Count.Is (2);
      } finally {
         UndoStack.Current = null;
      }
   }
}
