namespace ConDemo;
using Nori;

class Program {
   static void Main () {
      Lib.Init ();
      var dwg = DXFReader.FromFile ("W:/SPLINE/D17666.dxf");
      DXFWriter.SaveFile (dwg, "C:/ETC/Test.dxf");
      DXFReader.FromFile ("C:/ETC/Test.dxf");
   }
}
