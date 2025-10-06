namespace ConDemo;
using Nori;

class Program {
   static void Main () {
      Lib.Init ();
      var dwg = DXFReader.Load ("W:/SPLINE/D17666.dxf");
      DXFWriter.SaveFile (dwg, "C:/ETC/Test.dxf");
      DXFReader.Load ("C:/ETC/Test.dxf");
   }
}
