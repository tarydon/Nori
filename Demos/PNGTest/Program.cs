using Nori;
using PNGTest;
namespace PNGTest;


internal class Program {
   static void Main (string[] args) {
      var reader = new PNGReader (File.ReadAllBytes ("c:/etc/tiny.png"));
      reader.Load ();
   }
}
