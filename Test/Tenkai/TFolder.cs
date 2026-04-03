// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TFolder.cs
// ║║║║╬║╔╣║ Tests for PaperFolder
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (41, "Paper model folder tests", "Folder")]
class PaperFolderTests {
   [Test (229, "Fold A.dxf : Bracket holder")]
   void Test1 () => Process ("A", 600, 388, -60, -45);

   [Test (230, "Fold B.dxf : Curved gasket")]
   void Test2 () => Process ("B", 600, 394, -75, 45);

   [Test (231, "Fold C.dxf : Excavate claw")]
   void Test3 () => Process ("C", 600, 366, -60, 45);

   [Test (232, "Fold D.dxf : Box with many holes")]
   void Test4 () => Process ("D", 600, 508, -60, 45);

   [Test (233, "Fold E.dxf : Shallow cone")]
   void Test5 () => Process ("E", 600, 392, 120, 195);

   [Test (234, "Fold F.dxf : Flanges in holes")]
   void Test6 () => Process ("F", 600, 352, -45, 15);

   [Test (235, "Fold G.dxf : Flange on flange")]
   void Test7 () => Process ("G", 600, 292, -60, 45);

   [Test (236, "Fold H.dxf: Complex box")]
   void Test8 () => Process ("H", 600, 448, -60, 45);

   [Test (237, "Fold I.dxf: Simple double-flange")]
   void Test9 () => Process ("I", 600, 302, -60, 45);

   [Test (238, "Fold J.dxf: Flange-in-flange-in-flange")]
   void Test10 () => Process ("J", 600, 402, -60, 105);

   [Test (239, "Various error returns")]
   void Test11 () {
      Error ("BAD.1", EResult.NoOuterContour);
      Error ("BAD.2", EResult.IntersectingBendlines);
      Error ("BAD.3", EResult.IllFormedDrawing);
   }

   static void Error (string file, EResult result) {
      var dwg = DXFReader.Load (NT.File ($"Tenkai/Fold/{file}.dxf"));
      var folder = new PaperFolder (dwg);
      folder.Process (out var _).IsFalse ();
      folder.Result.Is (result);
   }

   static void Process (string file, int cx, int cy, int xRot, int yRot) {
      var dwg = DXFReader.Load (NT.File ($"Tenkai/Fold/{file}.dxf"));
      var folder = new PaperFolder (dwg);
      folder.Process (out var model).IsTrue (); 

      // Render the scene, measure it and crop the image as needed
      var scene = new Scene3 { Bound = model!.Bound, BgrdColor = Color4.White, 
                               Root = new Model3VN (model), Viewpoint = new (xRot, yRot) };
      var dib = scene.RenderZoomedImage (new (cx, cy), DIBitmap.EFormat.Gray8, out _);
      new PNGWriter (dib).Write (NT.TmpPNG);
      Assert.PNGFilesEqual (NT.File ($"Tenkai/Fold/{file}.png"), NT.TmpPNG, dib);
   }
}
