// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ TEval.cs
// ║║║║╬║╔╣║ Expression evaluator tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (17, "Expression evaluation tests", "UI.Input")]
class EvalTest {
   [Test (53, "Expression evaluation tests 1")]
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

   [Test (54, "Expression evaluation tests 2")]
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

   [Test (129, "Exceptions during expression evaluation")]
   void Test3 () {
      Eval e = new ();
      e.TryEvaluate ("1 + 2 +", out double v).IsFalse ();
      e.TryEvaluate ("1 + 2 3", out v).IsFalse ();
      e.TryEvaluate ("1 * (2 + 3", out v).IsFalse ();
   }
}