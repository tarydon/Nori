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

   static void Process (string file, int cx, int cy, int xRot, int yRot) {
      var dwg = DXFReader.Load (NT.File ($"Tenkai/Fold/{file}.dxf"));
      var folder = new PaperFolder (dwg); var model = folder.Process ();

      // Render the scene, measure it and crop the image as needed
      var scene = new Scene3 { Bound = model.Bound, BgrdColor = Color4.White, 
                               Root = new Model3VN (model), Viewpoint = new (xRot, yRot) };
      var dib = scene.RenderZoomedImage (new (cx, cy), DIBitmap.EFormat.Gray8, out _);
      new PNGWriter (dib).Write (NT.TmpPNG);
      Assert.PNGFilesEqual (NT.File ($"Tenkai/Fold/{file}.png"), NT.TmpPNG, dib);
   }
}
