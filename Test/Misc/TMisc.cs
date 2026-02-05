// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ TMisc.cs
// ║║║║╬║╔╣║ Miscellaneous tests
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections;
using System.Collections.Immutable;
using static System.Math;
namespace Nori.Testing;

[Fixture (3, "Miscellaneous tests", "Misc")]
class TMisc {
   // Test of various extension methods
   [Test (29, "Extensions test")]
   unsafe void Test1 () {
      // Clamp
      1.5.Clamp (1, 2).Is (1.5); 0.5.Clamp (1, 2).Is (1); 2.5.Clamp (1, 2).Is (2);
      2.0.Clamp ().Is (1);
      1.5f.Clamp (1, 2).Is (1.5f); 1.5f.Clamp (2, 3).Is (2f);
      1.5f.Clamp ().Is (1f);

      // Along
      0.5.Along ((1, 3, 5), (5, 7, 9)).Is ("(3,5,7)");
      0.5.Along ((1, 3), (5, 7)).Is ("(3,5)");
      0.5.Along (1, 5).Is (3);

      // D2R, R2D
      30.D2R ().Is (Lib.PI / 6); 45.0.D2R ().Is (Lib.PI / 4); Lib.PI.R2D ().Is (180);
      "45".ToInt ().Is (45); " 46a".ToInt ().Is (46); "X".ToInt ().Is (0);
      float f90 = (float)Lib.HalfPI; f90.R2D ().Is (90);

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
      0.001.IsZero (0.01).IsTrue (); 0.001.IsZero (0.0001).IsFalse ();
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
      13.532.Round (0.2).Is (13.6);

      Dictionary<int, int> squares = [];
      squares.Get (5, n => n * n).Is (25);
      squares.Get (5, n => n * n).Is (25);
      5.0.IsNan.IsFalse (); double.NaN.IsNan.IsTrue ();
      5f.IsNaN ().IsFalse (); float.NaN.IsNaN ().IsTrue ();
      5f.IsZero ().IsFalse (); 1e-6f.IsZero ().IsTrue ();
      22.RoundUp (10).Is (30);
      "1.5".ToDouble ().Is (1.5);
      "abc".ToDouble ().Is (0);
      squares.SafeGet (10, 981).Is (981);

      int[] ints = [1, 2, 3, 4, 5];
      ints.Roll (2).ToCSV ().Is ("3,4,5,1,2");
      ints.Roll (-1).ToCSV ().Is ("5,1,2,3,4");
      ints.Roll (0).ToCSV ().Is ("1,2,3,4,5");
      var aints = ints.ToImmutableArray (); aints.Sum ().Is (15);
      var ints2 = aints.ToArray (); ints2.Sum ().Is (15);
      int[] empty = []; empty.MinIndex ().Is (-1);
      string[] strs = ["one", "t,wo", "\'three\'"];
      strs.ToCSV ().Is ("one,'t,wo',three");
      "".ToInt ().Is (0);

      byte[] bin = [65, 66, 67, 0];
      fixed (byte* ptr = bin) {
         string ss = ((nint)ptr).ToUTF8 ();
         ss.Is ("ABC");
      }
      using (var stm = Lib.OpenRead ("nori:DXF/color.txt")) {
         byte[] bar = new byte[4]; stm.ReadExactly (bar);
         bar.Select (a => (int)a).Sum ().Is (48 * 4);
      }
      using (var stm = Lib.OpenRead ("nori:DXF/color.txt"))
         stm.ReadInt32 ().Is (0x30303030);
   }

   [Test (30, "AList<T> tests")]
   void Test2 () {
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

   [Test (31, "Basic test of RBTree")]
   void Test3 () {
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

   [Test (32, "Stress test of RBTree")]
   void Test4 () {
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

   [Test (33, "Tests of the Nori.Core.Lib class")]
   void Test5 () {
      Lib.Testing.IsTrue ();
      Lib.Acos (-2).R2D ().Is (180);
      Lib.GetLocalFile ("Demo").Replace ('\\', '/').EndsWith ("Bin/Demo");
      Lib.AddNamespace ("Nori");
      Lib.NiceName (typeof (System.Boolean)).Is ("bool");
      Lib.NiceName (typeof (Vector2)).Is ("Vector2");
      Lib.NormalizeAngle (Lib.TwoPI + Lib.HalfPI).Is (Lib.HalfPI);
      Lib.NormalizeAngle (-Lib.TwoPI - Lib.QuarterPI).Is (-Lib.QuarterPI);
      Lib.Print ("", ConsoleColor.Yellow); Lib.Println ("", ConsoleColor.Green);
      Lib.SolveLinearPair (3, 4, -13.3, 5, 6, -20.7, out var x, out var y).IsTrue ();
      x.Is (1.5); y.Is (2.2);
      Lib.SolveLinearPair (3, 4, -13.3, 30, 40, -133, out _, out _).IsFalse ();
      Lib.GetArcSteps (10, Lib.PI, 0.1, 1.05).Is (12);
      Lib.GetArcSteps (10, Lib.PI, 0.1, 10.01.D2R ()).Is (18);
      Lib.GetArcSteps (10, Lib.PI, 0.01, 1.05).Is (36);
      int a = 3, b = 2; Lib.Sort (ref a, ref b);
      a.Is (2); b.Is (3);
      Lib.ReadText ("nori:GL/Shader/arrowhead.frag").Length.Is (240);
      Lib.ReadBytes ("nori:GL/Shader/arrowhead.frag").Length.Is (251);
      Lib.ReadLines ("nori:GL/Shader/arrowhead.frag").Length.Is (12);
      double aa = 1.5; Lib.Set (ref aa, 1.5).IsFalse ();
      Lib.Set (ref aa, 2.5).IsTrue (); aa.Is (2.5);

      int n = 0; Lib.Set (ref n, 1).IsTrue (); Lib.Set (ref n, 1).IsFalse (); n.Is (1);
      float f = 0; Lib.Set (ref f, 1).IsTrue (); Lib.Set (ref f, 1).IsFalse (); f.Is (1f);
      double d = 0; Lib.Set (ref d, 1).IsTrue (); Lib.Set (ref f, 1).IsFalse (); f.Is (1.0);
      Color4 clr = Color4.Yellow; Lib.Set (ref clr, Color4.Blue).IsTrue (); Lib.Set (ref clr, Color4.Blue).IsFalse (); clr.Is (Color4.Blue);
      object o1 = new (), o2 = new (); Lib.SetR (ref o1, o2).IsTrue (); Lib.SetR (ref o1, o2).IsFalse (); o1.Equals (o2).IsTrue ();
      EDir e = EDir.N; Lib.SetE (ref e, EDir.S).IsTrue (); Lib.SetE (ref e, EDir.S).IsFalse (); e.Is (EDir.S);

      string s = "";
      Lib.Check (true, "This is true").IsTrue ();
      try { Lib.Check (false, "Crash1"); } catch (Exception e2) { s = e2.Message; }
      s.Is ("Crash1");
   }

   [Test (34, "Throwing various exceptions")]
   void Test6 () {
      new BadCaseException (12).Message.Is ($"Unhandled case '12' in {nameof (Test6)}");
      new ParseException ("13e", typeof (double)).Message.Is ("Cannot convert '13e' to double");
      // Except
      string s = "";
      try { throw new IncompleteCodeException ("Some"); } catch (Exception e) { s = e.Message; }
      s.Is ("Incomplete code: Some");
   }

   [Test (35, "Test of IdxList")]
   void Test7 () {
      IdxHeap<T1Type> list = new ();
      T1Type a = list.Alloc (), b = list.Alloc (); a.Is ("T1"); b.Is ("T2");
      list.Count.Is (2);
      list.Alloc ().Is ("T3");
      list.Release (b.Idx);
      list.Count.Is (2);
      T1Type c = list.Alloc (); c.Is ("T2");

      for (int i = 0; i < 10; i++) list.Alloc ();
      list.Count.Is (13); list[5].Is ("T5");
      list.Is ("IdxHeap<T1Type>, Count=13");
   }

   [Test (36, "Chains (a linked-list collection) test")]
   void Test8 () {
      Chains<int> chains = new ();
      int ones = 0, twos = 0;
      // Create two chains in parallel
      chains.Add (ref ones, 1);
      chains.Add (ref ones, 11);
      chains.Add (ref twos, 2);
      chains.Add (ref ones, 111);
      chains.Add (ref twos, 22);
      chains.Add (ref twos, 222);
      // Validate chains
      chains.Contains (ones, 111).IsTrue ();
      chains.Contains (ones, 222).IsFalse ();
      chains.Contains (twos, 222).IsTrue ();
      string.Join (",", chains.Enum (ones)).Is ("111,11,1");
      // Release a chain
      chains.ReleaseChain (ref twos); twos.Is (0);
      // Remove and add a few items
      int zero = 0;
      chains.Remove (ref zero, 0);   // Should be a no-op
      chains.Remove (ref ones, 11);
      chains.Remove (ref ones, 111);
      chains.Add (ref ones, 1111);
      chains.Contains (ones, 11).IsFalse ();
      string.Join (",", chains.Enum (ones)).Is ("1111,1");
      // Gather indices test
      List<int> indices = [];
      chains.GatherRawIndices (ones, indices);
      indices.Count.Is (2);
      chains.Data[indices.Last ()].Is (1);
   }

   [Test (38, "LineFont test")]
   void Test10 () {
      List<Poly> poly = [];
      List<Point2> pts = [];
      pts.AddRange ([(0, 0), (0, 5), (0, 10), (0, 15)]);
      var lf = LineFont.Get ("simplex");
      Out (0, 0, ETextAlign.BotLeft);
      Out (0, 5, ETextAlign.BaseLeft);
      Out (0, 10, ETextAlign.MidLeft);
      Out (0, 15, ETextAlign.TopLeft);

      pts.AddRange ([(15, 0), (15, 10), (15, 22), (15, 34), (34, 34)]);
      Out2 (15, 0, ETextAlign.BotLeft);
      Out2 (15, 10, ETextAlign.BaseLeft);
      Out2 (15, 22, ETextAlign.MidLeft);
      Out2 (15, 34, ETextAlign.TopLeft);
      Out2 (34, 34, ETextAlign.TopRight);

      pts.AddRange ([(8, 20), (8, 25), (8, 30), (0, 17)]);
      Out3 (8, 20, ETextAlign.BaseLeft);
      Out3 (8, 25, ETextAlign.BaseCenter);
      Out3 (8, 30, ETextAlign.BaseRight);
      lf.Render ("TRIPE", (0, 17), ETextAlign.BaseLeft, 15.D2R (), 1, 2, 0, poly);

      poly.Add (Poly.Line (-1, 5, 9, 5));
      poly.Add (Poly.Line (-1, 7, 9, 7));

      pts.AddRange ([(30, 0), (33, 17), (30, 22), (43, 0), (58, 0), (52, 14), (47, 21), (59, 30)]);
      Out4 (30, 0, ETextAlign.BaseLeft);
      Out4 (33, 17, ETextAlign.TopRight);
      Out4 (30, 22, ETextAlign.MidCenter);
      lf.Render ("ELONGATE", (43, 0), ETextAlign.BaseLeft, 15.D2R (), 1.5, 3, 90.D2R (), poly);

      lf.Render ("Sub\nSaharan\nAntarctica", (58, 0), ETextAlign.BaseRight, 0, 1, 1.5, 0, poly);
      lf.Render ("Sub\nSaharan\nAntarctica", (52, 14), ETextAlign.MidCenter, 0, 1, 1.5, 0, poly);
      lf.Render ("Sub\nSaharan\nAntarctica", (47, 21), ETextAlign.BaseLeft, 30.D2R (), 1, 1.5, 0, poly);
      lf.Render ("Reversed", (59, 30), ETextAlign.BaseLeft, 0, -0.5, 4, 0, poly);

      var sb = new StringBuilder ();
      poly.ForEach (a => sb.AppendLine (a.ToString ()));
      pts.ForEach (a => sb.AppendLine ($"P{a.X},{a.Y}"));
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual ("Misc/LineFont.txt", NT.TmpTxt);

      // Helpers ...............................................................
      void Out (double x, double y, ETextAlign align)
         => lf.Render ("Cray{}", new (x, y), align, 0, 1, 2, 0, poly);
      void Out2 (double x, double y, ETextAlign align)
         => lf.Render ("A()\nCray{}\n[123]", new (x, y), align, 0, 1, 1.5, 0, poly);
      void Out3 (double x, double y, ETextAlign align)
         => lf.Render ("MAX", (x, y), align, 0, 0.5, 3, 0, poly);
      void Out4 (double x, double y, ETextAlign align)
         => lf.Render ("Hello\nWorld", new (x, y), align, 0, 1, 2, 30.D2R (), poly);
   }


   [Test (68, "2D tessellation tests")]
   void Test11 () {
      // Create poly with holes
      PolyBuilder outer = new ();
      outer.Line (0, 0).Line (500, 0).Arc (500, 200, 500, 300, Poly.EFlags.CW)
         .Line (400, 300).Arc (100, 300, 100, 200, Poly.EFlags.CCW).Line (0, 200).Close ();
      List<Poly> polys = [
         outer.Build (),
         Poly.Circle ((80, 80), 60),
         Poly.Circle ((450, 70), 20),
         Poly.Circle ((250, 120), 20),
         Poly.Rectangle (160, 160, 180, 180),
         Poly.Rectangle (170, 20, 280, 40),
         Poly.Polygon ((350, 150), 30, 6),
         Poly.Polygon ((350, 50), 20, 5),
         Poly.Polygon ((50, 250), 20, 3),
         Poly.Circle ((250, 250), 20),
      ];

      // Make tessellation inputs
      List<Point2> pts = []; List<int> splits = [0];
      foreach (var poly in polys) {
         poly.Discretize (pts, 0.1, 0.5411);
         splits.Add (pts.Count);
      }

      // Tessellate the polygon into triangles
      var tries = Tess2D.Process (pts, splits);
      var nodes = tries.Select (n => (Point3)pts[n]).ToList ();

      // Build and compare the mesh
      File.WriteAllText (NT.TmpTxt, new Mesh3Builder (nodes.AsSpan ()).Build ().ToTMesh ());
      Assert.TextFilesEqual ("Geom/Tess/gl2d.tmesh", NT.TmpTxt);
   }

   [Test (12, "Test for E2BendLine properties")]
   void Test12 () {
      var dwg = new Dwg2 ();
      dwg.Add (Poly.Rectangle (0, 0, 100, 60));
      dwg.Add (new E2Bendline (dwg, Point2.List (75, 0, 75, 60), Lib.HalfPI, 1, 0.38, 1));
      var s = dwg.Ents.OfType<E2Bendline> ().First ();
      Assert.IsTrue (Round (s.FlatWidth, 3) == 2.168 && Round (s.Deduction, 3) == 1.832);
      s.Deduction = 1.5;
      Assert.IsTrue (Round (s.FlatWidth, 1) == 2.5 && Round (s.KFactor, 3) == 0.592);
   }

   [Test (114, "Miscellany: DIBitmap, MultiDispose, AList")]
   void Test13 () {
      // DIBitmap
      var dib = new DIBitmap (20, 10, DIBitmap.EFormat.Gray8, new byte[200]);
      dib.Is ("DIBitmap: 20x10, Gray8");
      // MultiDispose
      int[] arr = new int[10];
      T2Disp d1 = new (arr, 1, 10), d2 = new (arr, 2, 20);
      using (var md = new MultiDispose (d1, d2)) { }
      arr[1].Is (10); arr[2].Is (20);
      // AList
      AList<int> set = [1, 2, 3];
      (set.SyncRoot is List<int>).IsTrue ();
      IList il = set;
      il[2] = 20; int n = (int)il[2]!; n.Is (20);
   }

   [Test (134, "Test to handle duplicate layers")]
   void Test14 () {
      var dwg = new Dwg2 ();
      // Add poly entities in different layers.
      SetLayer (new Layer2 ("Circle", Color4.Black, ELineType.Continuous));
      dwg.Add (Poly.Circle (new (0, 0), 25)); dwg.Add (Poly.Circle (new (50, 50), 50));
      SetLayer (new Layer2 ("Rect", Color4.Red, ELineType.Continuous));
      dwg.Add (Poly.Rectangle (5, 5, 20, 20)); dwg.Add (Poly.Rectangle (40, 60, 80, 100));
      SetLayer (new Layer2 ("Line", Color4.Blue, ELineType.Continuous));
      dwg.Add (Poly.Line (0, 0, 50, 50)); dwg.Add (Poly.Line (50, 50, 100, 100));

      // Add new layers with their names matching existing layers
      dwg.Add (new Layer2 ("Rect", Color4.Yellow, ELineType.Dot));
      dwg.Add (new Layer2 ("Line", Color4.Green, ELineType.DashDot));
      dwg.Add (new Layer2 ("Circle", Color4.White, ELineType.Dash));
      Assert.IsTrue (dwg.Layers.Count == 3);
      CurlWriter.Save (dwg, NT.TmpCurl);
      Assert.TextFilesEqual ("Misc/Layers.curl", NT.TmpCurl);

      void SetLayer (Layer2 layer) { dwg.Add (layer); dwg.CurrentLayer = layer; }
   }

   [Test (147, "Minimum Enclosing Circle/Sphere")]
   void Test16 () {
      double[] D = [.. File.ReadAllLines (NT.File ("Misc/MES.pts")).Select (double.Parse)];
      // Circle unit tests ------------
      // 1. Invalid inputs
      // Empty set
      var c = MinCircle.From ([]);
      c.OK.IsFalse ();
      // One point
      c = MinCircle.From ([(10, 10)]);
      c.OK.IsFalse ();
      // Duplicate points
      c = MinCircle.From ([(10, 10), (10, 10), (10, 10), (10, 10)]);
      c.OK.IsFalse ();

      // 2. Two points.
      c = MinCircle.From ([(10, 0), (-10, 0)]);
      c.Radius.Is (10); c.Center.Is ("(0,0)");
      // Equality tests
      Assert.IsTrue (c.EQ (MinCircle.From ([(0, 10), (0, -10)])));
      Assert.IsFalse (c.EQ (MinCircle.From ([(0, 11), (0, -11)])));
      Assert.IsFalse (c.EQ (MinCircle.From ([(10, 10), (10, -10)])));

      // 3. Three points.
      // Three points on circumference.
      c = MinCircle.From ([(10, 0), (-10, 0), (0, 10)]);
      c.Radius.Is (10); c.Center.Is ("(0,0)");
      // Two points on circumference, one inside.
      c = MinCircle.From ([(10, 0), (0, 9), (-10, 0)]);
      c.Radius.Is (10); c.Center.Is ("(0,0)");
      c = MinCircle.From ([(10, 0), (10, 0), (-10, 0)]);
      c.Radius.Is (10); c.Center.Is ("(0,0)");

      // 4. Four or more points.
      // Three points on circumference, one inside.
      c = MinCircle.From ([(10, 0), (-10, 0), (0, 10), (0, -9)]);
      c.Radius.Is (10); c.Center.Is ("(0,0)");
      // Two points on circumference, two inside.
      c = MinCircle.From ([(10, 0), (0, 9), (0, -9), (-10, 0)]);
      c.Radius.Is (10); c.Center.Is ("(0,0)");
      // All on circumference.
      c = MinCircle.From ([(10, 0), (0, 10), (-10, 0), (0, -10)]);
      c.Radius.Is (10); c.Center.Is ("(0,0)");
      // Dataset from file
      Point2[] pt2 = [.. D.Chunk (2).Select (a => (a[0], a[1]))];
      c = MinCircle.From (pt2);
      // 639.129608, (240.79299,189.43126)
      c.ToString ().Is ("639.129608, (240.79299,189.43126)");
      // There should not be any point outside the circle
      c.Radius.Is (pt2.Max (c.Center.DistTo));

      // Sphere unit tests ------------
      // 1. Invalid inputs
      // Empty set
      var s = MinSphere.From ([]);
      s.OK.IsFalse ();
      // One point
      s = MinSphere.From ([(10, 10, 0)]);
      s.OK.IsFalse ();
      // Duplicate points
      s = MinSphere.From ([(10, 10, 0), (10, 10, 0), (10, 10, 0), (10, 10, 0)]);
      s.OK.IsFalse ();

      // 2. Two points.
      s = MinSphere.From ([(10, 0, 0), (-10, 0, 0)]);
      s.Radius.Is (10); s.Center.Is ("(0,0,0)");
      // Equality tests
      Assert.IsTrue (s.EQ (MinSphere.From ([(0, 10, 0), (0, -10, 0)])));
      Assert.IsFalse (s.EQ (MinSphere.From ([(0, 11, 0), (0, -11, 0)])));
      Assert.IsFalse (s.EQ (MinSphere.From ([(10, 0, 10), (10, 0, -10)])));

      // 3.Three points.
      // Three points on circumference.
      s = MinSphere.From ([(10, 0, 0), (-10, 0, 0), (0, 0, 10)]);
      s.Radius.Is (10); s.Center.Is ("(0,0,0)");
      // Two points on circumference, one inside.
      s = MinSphere.From ([(10, 0, 0), (0, 9, 0), (-10, 0, 0)]);
      s.Radius.Is (10); s.Center.Is ("(0,0,0)");
      s = MinSphere.From ([(10, 0, 0), (10, 0, 0), (-10, 0, 0)]);
      s.Radius.Is (10); s.Center.Is ("(0,0,0)");

      // 4. Four or more points.
      // Two points defining diameter, two points inside.
      s = MinSphere.From ([(10, 0, 0), (8, 0, 1), (0, 5, 0), (-10, 0, 0)]);
      s.Radius.Is (10); s.Center.Is ("(0,0,0)");
      // Another variant of the above but all in same plane.
      s = MinSphere.From ([(10, 0, 0), (3, 3, 0), (-3, -3, 0), (-10, 0, 0)]);
      s.Radius.Is (10); s.Center.Is ("(0,0,0)");
      // Three points on surface (circle circumference), one inside.
      double a = 2 * PI / 3, b = 4 * PI / 3;
      s = MinSphere.From ([(10, 0, 0), (10 * Cos (a), 10 * Sin (a), 0), (10 * Cos (b), 10 * Sin (b), 0), (1, 0, 2)]);
      s.Radius.Is (10); s.Center.Is ("(0,0,0)");
      // All on circumference.
      s = MinSphere.From ([(10, 0, 0), (0, 10, 0), (0, 0, 10), (-10, 0, 0), (0, -10, 0), (0, 0, -10)]);
      s.Radius.Is (10); s.Center.Is ("(0,0,0)");

      // 5. Dataset from file
      Point3[] pt3 = [.. D.Chunk (3).Select (a => (a[0], a[1], a[2]))];
      s = MinSphere.From (pt3);
      // 531.058455, (169.309693,321.368582,63.459182)
      s.ToString ().Is ("531.058455, (169.309693,321.368582,63.459182)");
      // There should not be any point outside the sphere
      s.Radius.Is (pt3.Max (s.Center.DistTo));
   }

   [Test (148, "Plane intersection with mesh")]
   public void Test17 () {
      var mesh = new STLReader (NT.File ("Misc/cali-bee.STL")).BuildMesh ();
      var plane = new PlaneDef (Point3.Zero, Vector3.YAxis);
      List<Polyline3> curves = [];
      new MeshSlicer ([mesh]).Compute (plane, curves);
      CurlWriter.Save (curves, NT.TmpCurl);
      Assert.TextFilesEqual ("Misc/Curves.curl", NT.TmpCurl);
   }

   [Test (167, "OBB from points")]
   void Test19 () {
      Point3f[] pts = [new (500, 0, 0), new (0, 500, 0), new (0, 0, 500), new (-500, 0, 0), new (0, -500, 0), new (0, 0, -500)];
      var obb = OBB.From (pts);
      obb.Center.Is ("(0,0,0)");
      obb.X.Is ("<0.70711,-0.70711,0>");
      obb.Y.Is ("<0.40825,0.40825,0.8165>");
      obb.Extent.Is ("<353.55338,408.24826,288.67514>");
      // Test when OBB is AABB
      pts = [new (500, 400, 300), new (500, -400, 300), new (-500, 400, 300), new (-500, -400, 300), 
         new (500, 400, -300), new (500, -400, -300), new (-500, 400, -300), new (-500, -400, -300)];
      var aabb = OBB.From (pts);
      aabb.X.Is ("<1,0,0>"); aabb.Y.Is ("<0,1,0>");
      aabb.Extent.Is ("<500,400,300>");
   }

   [Test (169, "Convex-hull of point set")]
   void Test20 () {
      Point2[] pts = [(0, 0), (100, 0), (100, 100), (50, 50), (0, 100), (50, 25), (75, 75)];
      var hull = ConvexHull.Compute (pts);
      Assert.IsTrue (hull.Count == 4);
      Assert.IsTrue (hull.Contains ((0, 0)) && hull.Contains ((100, 0)) && hull.Contains ((0, 100)) && hull.Contains ((100, 100)));
   }

   [Test (170, "Convex-hull of simple polygon")]
   void Test21 () {
      Point2[] pts = [(0, 0), (100, 0), (100, 100), (50, 50), (0, 100), (50, 25)];
      var hull = ConvexHull.ComputeForSimplePolygon (pts);
      Assert.IsTrue (hull.Count == 4);
      Assert.IsTrue (hull.Contains ((0, 0)) && hull.Contains ((100, 0)) && hull.Contains ((0, 100)) && hull.Contains ((100, 100)));
      hull = ConvexHull.ComputeForSimplePolygon ([.. pts.Reverse ()]);
      Assert.IsTrue (hull.Count == 4);
      Assert.IsTrue (hull.Contains ((0, 0)) && hull.Contains ((100, 0)) && hull.Contains ((0, 100)) && hull.Contains ((100, 100)));

      pts = [(0, 0), (100, 0), (100, 100), (0, 100)]; // A simple square.
      hull = ConvexHull.ComputeForSimplePolygon ([.. pts.Reverse ()]);
      Assert.IsTrue (hull.Count == 4);
      Assert.IsTrue (hull.Contains ((0, 0)) && hull.Contains ((100, 0)) && hull.Contains ((0, 100)) && hull.Contains ((100, 100)));

      pts = [(0, 0), (25, 0), (50, 10), (75, 0), (100, 0), (100, 100), (0, 100)]; // Simple polygon with a "dent"
      hull = ConvexHull.ComputeForSimplePolygon ([.. pts.Reverse ()]);
      Assert.IsTrue (hull.Count == 4);
      Assert.IsTrue (hull.Contains ((0, 0)) && hull.Contains ((100, 0)) && hull.Contains ((0, 100)) && hull.Contains ((100, 100)));

      hull = ConvexHull.Compute (Poly.Lines (pts, true), true);
      Assert.IsTrue (hull.Count == 4);
      Assert.IsTrue (hull.Contains ((0, 0)) && hull.Contains ((100, 0)) && hull.Contains ((0, 100)) && hull.Contains ((100, 100)));
   }

   class T1Type : IIndexed {
      public override string ToString () => $"T{Idx}";
      public int Idx { get; set; }
   }

   class T2Disp (int[] Array, int Index, int Value) : IDisposable {
      public void Dispose () => Array[Index] = Value;
   }
}