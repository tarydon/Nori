// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TDwg.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (38, "Tests for DwgSnap", "Dwg")]
class DwgSnapTests {
   DwgSnapTests () {
      mDwg = DXFReader.Load (NT.File ("Dwg/Snap.dxf"));
      List<Ent2> ents = [new E2Poly (mDwg.CurrentLayer, Poly.Circle (Point2.Zero, 10)), 
                         new E2Poly (mDwg.CurrentLayer, Poly.Line (Point2.Zero, new (10, 10)))];
      Block2 block = new Block2 ("STAR", Point2.Zero, ents); mDwg.Add (block);
      Style2 style = new Style2 ("STD", "SIMPLEX", 0, 1, 0); mDwg.Add (style);
      mDwg.Ents.Add (new E2Text (mDwg.CurrentLayer, mDwg.Styles[^1], "Hello", new (-110, 20), 10, 0, 0, 1, ETextAlign.BaseLeft));
      mDwg.Ents.Add (new E2Insert (mDwg, mDwg.CurrentLayer, "STAR", new (-110, 30), 0, 1, 1));
      mSnap = new DwgSnap (mDwg);
   }

   [Test (206, "Test on, mid, end snap (Line / Arc)")]
   void Test1 () {
      mSnap.Snap (new (56, 29.6), 1); Dump ("ONLine", "ON snap on line");
      mSnap.Snap (new (120.1, 44), 1); Dump ("ONArc", "ON snap on arc");

      mSnap.Snap (new (20.1, 30.1), 1); Dump ("ENDLine", "END snap on line");
      mSnap.Snap (new (118.41, 98.4), 1); Dump ("ENDArc", "END snap on arc");

      mSnap.Snap (new (55.1, 29.9), 1); Dump ("MIDLine", "MID snap on line");
      mSnap.Snap (new (127, 54.5), 1); Dump ("MIDArc", "MID snap on arc");
   }

   [Test (207, "Test intersection (Line-Line, Line-Arc, Arc-Arc)")]
   void Test2 () {
      mSnap.Snap (new (52.4, 146), 1); Dump ("INTLineLine", "INT snap line-line");
      mSnap.Snap (new (127.4, 184), 1); Dump ("INTLineArc", "INT snap line-arc");
      mSnap.Snap (new (124.7, 150.1), 1); Dump ("INTArcArc", "INT snap arc-arc");
   }

   [Test (208, "Test center, quadrant")]
   void Test3 () {
      mSnap.Snap (new (180.1, 70.1), 1); Dump ("CENCircle", "CEN snap on circle");
      mSnap.Snap (new (240.1, 69.9), 1); Dump ("CENArc", "CEN snap on arc");

      mSnap.Snap (new (180.1, 110.1), 1); Dump ("QUADCircle", "QUAD snap on circle");
      mSnap.Snap (new (280.1, 70.1), 1); Dump ("QUADArc", "QUAD snap on arc");
      mSnap.Snap (new (200.1, 130.1), 1); Dump ("QUADMiss", "QUAD snap missed");
   }

   [Test (209, "Construction lines")]
   void Test4 () {
      mSnap.LastClickedPt = new (50, 60);
      mSnap.Snap (new (50.1, 60.1), 1); Dump ("AUX1", "Snap on LastClickedPoint");
      mSnap.Snap (new (55.1, 60.1), 1); Dump ("CONHorz", "CON horizontal");
      mSnap.Snap (new (50.1, 65.1), 1); Dump ("CONVert", "CON vertical");

      mSnap.Snap (new (190.1, 170.1), 1); Dump ("CON1", "CON-Intersection");
      mSnap.Snap (new (164.3, 214.7), 1); Dump ("CON2", "CON-Perpendicular1");
      mSnap.Snap (new (163.2, 185.6), 1); Dump ("CON3", "CON-Perpendicular2");
      mSnap.Snap (new (163.2, 154.6), 1); Dump ("CON4", "CON-Tangent1");
      mSnap.Snap (new (164.1, 125.2), 1); Dump ("CON5", "CON-Tangent2");
   }

   [Test (210, "Node snaps")]
   void Test5 () {
      mSnap.Snap (new (-110.1, 10.1), 1); Dump ("NODEPt", "NODE snap on point");
      mSnap.Snap (new (-110.1, 20.1), 1); Dump ("NODETxt", "NODE snap on text");
      mSnap.Snap (new (-110.1, 30.1), 1); Dump ("NODEIns", "NODE snap on insert");
   }

   [Test (211, "Cons-line intersect")]
   void Test6 () {
      mSnap.Snap (new (20.1, 30.1), 1); mSnap.ESnap.Is (ESnap.Endpoint);
      mSnap.Snap (new (130.1, 70.1), 1); mSnap.ESnap.Is (ESnap.Quadrant);
      mSnap.Snap (new (20.1, 70.1), 1); Dump ("CONSInt", "CONS lines intersect");

      mSnap.Snap (new (271.3, 155), 1); mSnap.ESnap.Is (ESnap.Endpoint);
      mSnap.Snap (new (237.3, 197.3), 1); Dump ("CONSSegInt", "CONS-Seg intersection");
   }

   [Test (212, "Perpendicular, tangent snaps")]
   void Test7 () {
      mSnap.LastClickedPt = new (20, 160);
      mSnap.Snap (new (32.1, 136.1), 1); Dump ("PERPLine", "Perpendicular line snap");
      mSnap.LastClickedPt = new (140, 110);
      mSnap.Snap (new (151.8, 98.3), 1); Dump ("PERPArc1", "Perpendicular arc snap");
      mSnap.Snap (new (208.9, 41.8), 1); Dump ("PERPArc2", "Perpendicular arc snap-2");

      mSnap.LastClickedPt = new (160, 130);
      mSnap.Snap (new (142.7, 84.2), 1); Dump ("TANGArc1", "Tangent snap-1");
      mSnap.Snap (new (201, 103), 1); Dump ("TANGArc2", "Tangent snap-2");
   }

   void Dump (string file, string title) {
      var sb = new StringBuilder ();
      sb.AppendLine ($"Title = {title}");
      sb.AppendLine ($"Pt = {mSnap.PtSnap}, ESnap={mSnap.ESnap}");
      foreach (var (Pt, Angle) in mSnap.Lines) 
         sb.AppendLine ($"Line = ({Pt}, {Angle.R2D ().Round (3)}");
      foreach (var (text, Pt, above) in mSnap.Labels) 
         sb.AppendLine ($"Label = (\"{text}\", {Pt}, {above})");
      File.WriteAllText (NT.TmpTxt, sb.ToString ());
      Assert.TextFilesEqual ($"Dwg/{file}.txt", NT.TmpTxt);
   }

   Dwg2 mDwg;
   DwgSnap mSnap;
}

[Fixture (39, "Miscellaneous drawing tests", "Dwg")]
class DwgMiscTests {
   [Test (213, "Test of Ent2.IsSelected, InBlock")]
   void Test1 () {
      Dwg2 dwg = DXFReader.Load (NT.File ("IO/DXF/Block01.dxf"));
      var ent = dwg.Ents[0];
      // Check InBlock is set only for entities in a block
      dwg.Blocks[0].Ents[0].InBlock.IsTrue ();
      ent.InBlock.IsFalse ();
      // Toggle IsSelected, and make sure the notification is fired
      ent.IsSelected.IsFalse ();
      List<EProp> fired = [];
      IDisposable disp = ent.Subscribe (fired.Add);
      ent.IsSelected = true;
      ent.IsSelected.IsTrue ();
      fired.Count.Is (1); fired[0].Is (EProp.Selected);
      // After disposing the watcher, it should no longer fire
      disp.Dispose ();
      ent.IsSelected = false;
      fired.Count.Is (1);
   }

   [Test (214, "Test of IsCloser, GetBound for all Ent2")]
   void Test2 () {
      Dwg2 dwg = DXFReader.Load (NT.File ("IO/DXF/AllEnt.dxf"));
      var bound = dwg.Bound; bound.Is ("(0~400,0~200)");
      Matrix2 xfm = Matrix2.Rotation (Lib.HalfPI);
      Point2 far = new (1000, 1000);
      // Poly
      var poly = dwg.Ents.OfType<E2Poly> ().Single ();
      double t = 2; poly.IsCloser (new (-1, 100), ref t).Is (true); t.Is (1);
      t = 2; poly.IsCloser (new (-3, 100), ref t).Is (false);
      poly.IsCloser (far, ref t).Is (false);
      poly.GetBound (xfm).Is ("(-200~0,0~400)");
      // Point
      var point = dwg.Ents.OfType<E2Point> ().Single ();
      t = 2; point.IsCloser (new (9, 10), ref t).Is (true); t.Is (1);
      t = 2; point.IsCloser (new (7, 10), ref t).Is (false);
      point.IsCloser (far, ref t).Is (false);
      point.GetBound (xfm).Is ("(-10~-10,10~10)");
      // Bendline
      var bend = dwg.Ents.OfType<E2Bendline> ().Single ();
      t = 2; bend.IsCloser (new (20, 29), ref t).Is (true); t.Is (1);
      t = 2; bend.IsCloser (new (-3, 30), ref t).Is (false);
      bend.IsCloser (far, ref t).Is (false);
      bend.GetBound (xfm).Is ("(-30~-30,0~400)");
      // Solid
      var solid = dwg.Ents.OfType<E2Solid> ().Single ();
      t = 2; solid.IsCloser (new (149, 65), ref t).Is (true); t.Is (1);
      t = 2; solid.IsCloser (new (147, 65), ref t).Is (false);
      solid.IsCloser (far, ref t).Is (false);
      solid.GetBound (xfm).Is ("(-75~-60,150~170)");
      // Spline
      var spline = dwg.Ents.OfType<E2Spline> ().Single ();
      t = 2; spline.IsCloser (new (40, 85), ref t).Is (true); Math.Round (t, 2).Is (0.23);
      t = 2; spline.IsCloser (new (40, 89), ref t).Is (false);
      spline.IsCloser (far, ref t).Is (false);
      spline.GetBound (xfm).Is ("(-110~-50,10~110)");
      // Insert
      var insert = dwg.Ents.OfType<E2Insert> ().Single ();
      t = 2; insert.IsCloser (new (141, 70), ref t).Is (true); t.Is (1);
      t = 2; insert.IsCloser (new (141.5, 71.5), ref t).Is (false);
      insert.IsCloser (far, ref t).Is (false);
      insert.GetBound (xfm).Is ("(-70~-60,120~140)");
      // Text
      var text = dwg.Ents.OfType<E2Text> ().Single ();
      t = 2; text.IsCloser (new (199, 110), ref t).Is (true); t.Is (0.8);
      t = 2; text.IsCloser (new (197, 110), ref t).Is (false);
      text.IsCloser (far, ref t).Is (false);
      text.GetBound (xfm).Is ("(-110~-98.1,150~240.39999)");
      // Dimension
      dwg = DXFReader.Load (NT.File ("IO/DXF/AllEnts.dxf"));
      var dim = dwg.Ents.OfType<E2Dimension> ().Single ();
      t = 2; dim.IsCloser (new (65, 56), ref t).Is (true); Math.Round (t, 2).Is (0.21);
      t = 2; dim.IsCloser (new (65, 53), ref t).Is (false);
      dim.IsCloser (far, ref t).Is (false);
      dim.GetBound (xfm).Is ("(-59.33142~-50,0~90)");
   }

   [Test (215, "Curl round-trip of all Ent2")]
   void Test3 () {
      Dwg2 dwg = DXFReader.Load (NT.File ("IO/DXF/AllEnts.dxf"));
      CurlWriter.Save (dwg, NT.TmpTxt, "All-Ents");
      Assert.TextFilesEqual ("IO/DXF/AllEnts.curl", NT.TmpTxt);
      Dwg2 dwg2 = (Dwg2)CurlReader.Load (NT.File ("IO/DXF/AllEnts.curl"));
      CurlWriter.Save (dwg2, NT.TmpTxt, "All-Ents");
      Assert.TextFilesEqual ("IO/DXF/AllEnts.curl", NT.TmpTxt);
   }
}
