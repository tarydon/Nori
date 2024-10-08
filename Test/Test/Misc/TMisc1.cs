// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ TMisc1.cs
// ║║║║╬║╔╣║ Miscellaneous tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (3, "Miscellaneous tests", "Misc")]
class TMisc {
   [Test (22, "Test of Color4")]
   void Test1 () {
      new Color4 (48, 12, 24, 36).Is ("#300C1824");
      new Color4 (12, 24, 36).Is ("#0C1824");
      Color4.Nil.IsNil.Is (true); Color4.Red.IsNil.Is (false);
      Color4.Red.Is ("Red"); Color4.Nil.Is ("Nil");
      Color4.Transparent.Is ("Transparent"); Color4.Black.Is ("Black");
      Color4.Green.Is ("Green"); Color4.Blue.Is ("Blue");
      Color4.Yellow.Is ("Yellow"); Color4.Magenta.Is ("Magenta");
      Color4.Cyan.Is ("Cyan"); Color4.White.Is ("White");
      Color4.Red.EQ (Color4.Red).Is (true);
      Color4.Red.Value.Is (4278190335U);
      Color4.Gray (128).Is ("#808080");
      Color4.Gray (0x33).Is ("#333");
      new Color4 (0x11, 0x22, 0x33).Is ("#123");
      ((Vec4F)Color4.Magenta).Is ("<1,0,1,1>");
      Color4.Random.A.Is ((byte)255);
      Color4.Cyan.Deconstruct (out int r, out int g, out int b, out int a);
      r.Is (0); g.Is (255); b.Is (255); a.Is (255);
   }

   [Test (23, "Test of Coverage class")]
   void Test2 () {
      var c = new Coverage ($"{NT.Data}/Misc/coverage.xml");
      TestRunner.SetNoriFiles (c);
      var sb = new StringBuilder ();
      foreach (var (no, data) in c.Files.Numbered ()) sb.Append ($"{no + 1}. {data}\n");
      foreach (var (no, b) in c.Blocks.Numbered ())
         sb.Append ($"{no + 1}. File({b.FileId + 1}) : ({b.Start.Line},{b.Start.Col}) .. ({b.End.Line},{b.End.Col}) : {(b.Covered ? 1 : 0)}\n");
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual ($"{NT.Data}/Misc/coverage.txt", NT.TmpTxt);
   }

   // Test of various extension methods
   [Test (24, "Extensions test")]
   void Test3 () {
      // Clamp
      1.5.Clamp (1, 2).Is (1.5); 0.5.Clamp (1, 2).Is (1); 2.5.Clamp (1, 2).Is (2);
      2.0.Clamp ().Is (1);
      1.5f.Clamp (1, 2).Is (1.5f); 1.5f.Clamp (2, 3).Is (2f);

      // D2R, R2D
      30.D2R ().Is (Lib.PI / 6); 45.0.D2R ().Is (Lib.PI / 4); Lib.PI.R2D ().Is (180);

      // EQ variants
      1.000001.EQ (1.000002).IsFalse (); 1.0000001.EQ (1.0000002).IsTrue ();
      1.001.EQ (1.002, 0.0011).IsTrue (); 1.001.EQ (1.002, 0.0001).IsFalse ();
      1.00001f.EQ (1.00002f).IsFalse (); 1.000001f.EQ (1.000002f).IsTrue ();
      Half a = (Half)1.0001, b = (Half)1.0002; a.EQ (b).IsTrue ();
      Half c = (Half)1.001, d = (Half)1.003; c.EQ (d).IsFalse ();
      "hello".EqIC ("HELLO").IsTrue (); "hello".EqIC ("hola").IsFalse ();
      string? s = null; s.IsBlank ().IsTrue (); "".IsBlank ().IsTrue ();
      "  \t\r\n".IsBlank ().IsTrue (); ".".IsBlank ().IsFalse ();
      0.0000011.IsZero ().IsFalse (); 0.0000001.IsZero ().IsTrue ();
      if (new Random ().NextBool ()) 1.1.EQ (1.2).IsFalse ();

      // HasAttribute
      this.GetType ().HasAttribute<FixtureAttribute> ().IsTrue ();
      1.GetType ().HasAttribute<FixtureAttribute> ().IsFalse ();

      // Numbered
      string[] list = ["zero", "one", "two"];
      string.Join (" | ", list.Numbered ()).Is ("(0, zero) | (1, one) | (2, two)");
      string tmp1 = "";
      list.Select (a => a.ToUpper ()).ForEach (s => tmp1 += s);
      tmp1.Is ("ZEROONETWO");

      // Round, R3, R5, R6
      1.23456.Round (3).Is (1.235); 1.23456f.Round (2).Is (1.23);
      Half aa = (Half)1.234567; aa.R3 ().Is (1.234);
      1.2345678f.R5 ().Is ("1.23457");
      1.2345678.R6 ().Is ("1.234568");

      Dictionary<int, int> squares = [];
      squares.Get (5, n => n * n).Is (25);
      squares.Get (5, n => n * n).Is (25);
      5.0.IsNaN ().IsFalse (); double.NaN.IsNaN ().IsTrue ();
      5f.IsNaN ().IsFalse (); float.NaN.IsNaN ().IsTrue ();
      5f.IsZero ().IsFalse (); 1e-6f.IsZero ().IsTrue ();
      22.RoundUp (10).Is (30);
      "1.5".ToDouble ().Is (1.5);
      "abc".ToDouble ().Is (0);
   }

   [Test (29, "AList<T> tests")]
   void Test4 () {
      AList<int> set = [];
      List<ListChange> changes = [];
      set.Subscribe (changes.Add);
      set.Add (1); set.Add (2); set.Insert (0, 3); set.Remove (2); set.Add (5); set[1] = 10;
      set[1].Is (10);
      set.Count.Is (3);
      set.Contains (5).IsTrue (); set.Contains (55).IsFalse ();
      set.Clear ();
      set.Remove (55).IsFalse ();
      string s = string.Join (' ', changes.Select (a => $"{a.Action.ToString ()[0]}{a.Index}"));
      s.Is ("A0 A1 A0 R2 A2 R1 A1 C2");
      changes[0].Is ("Added(0)");

      set.IsReadOnly.IsFalse (); set.IsFixedSize.IsFalse (); set.IsSynchronized.IsFalse ();
      set.Add ((object)24); set.Contains ((object)24).IsTrue (); set.IndexOf ((object)24).Is (0);
      set.Insert (0, (object)16); set.Remove ((object)16); set[0].Is (24); set.Count.Is (1);
      ReadOnlySpan<int> span = set; span.Length.Is (1);
      int[] a1 = new int[100];
      Exception? e = null;
      try { set.CopyTo ((Array)a1, 0); } catch (Exception e1) { e = e1; }
      (e == null).IsFalse ();
      set.CopyTo (a1, 1);
      object? o1 = ((System.Collections.IList)set)[0]; 
   }

   [Test (50, "Basic test of RBTree")]
   void Test5 () {
      // Values in this tree are 4-character strings of the form "[12]", and the key
      // for that string is just the 2-digit integer in the middle (as shown in the key-extractor
      // function below)
      RBTree<string, int> tree = new (s => int.Parse (s[1..3]));
      tree.Count.Is (0);
      foreach (var n in new[] { 21, 47, 12, 93, 56, 65, 42, 11, 98, 74 })
         tree.Add ($"[{n}]");
      string.Join (" ", tree).Is ("[11] [12] [21] [42] [47] [56] [65] [74] [93] [98]");
      tree.Count.Is (10);
      tree.Get (12).Is ("[12]"); tree.Get (93).Is ("[93]");
      tree.GetFloor (50).Is ("[47]"); tree.GetFloor (21).Is ("[21]");
      tree.GetCeiling (75).Is ("[93]"); tree.GetCeiling (98).Is ("[98]");
      tree.Contains (42).IsTrue ();
      tree.Remove (42); tree.Remove (56); tree.Remove (98); tree.Remove (12); tree.Remove (47);
      tree.Contains (42).IsFalse ();
      string.Join (" ", tree).Is ("[11] [21] [65] [74] [93]");
      ref readonly string s1 = ref tree.Get (56);
      Lib.IsNull (in s1).IsTrue ();
      tree.Min ().Is ("[11]"); tree.Max ().Is ("[93]");
      ref readonly string s2 = ref tree.GetFloor (10);
      Lib.IsNull (in s2).IsTrue ();
      ref readonly string s3 = ref tree.GetCeiling (100);
      Lib.IsNull (in s3).IsTrue ();
      tree.Add ("(74)");
      string.Join (" ", tree).Is ("[11] [21] [65] (74) [93]");
      tree.Count.Is (5);
   }

   [Test (51, "Stress test of RBTree")]
   void Test6 () {
      Random r = new (1);
      RBTree<int, int> tree = new (a => a);
      SortedSet<int> set = [];
      HashSet<int> toremove = [];
      // Add about 10000 elements, and check that the RBTree contents are
      // exactly equal to the contents of the .Net SortedSet
      int max = 10000;
      for (int i = 0; i < max; i++) {
         int n = r.Next (max * 5);
         tree.Add (n); set.Add (n);
         if (r.NextBool ()) toremove.Add (n);
      }
      tree.SequenceEqual (set).IsTrue ();
      // Now, remove about 5000 elements and check again
      foreach (var n in toremove) {
         tree.Remove (n); set.Remove (n);
      }
      tree.SequenceEqual (set).IsTrue ();
      // Now add in about 1000 elements an check again
      for (int i = 0; i < max / 10; i++) {
         int n = r.Next (max * 5);
         tree.Add (n); set.Add (n);
      }
      tree.SequenceEqual (set).IsTrue ();
   }
}
