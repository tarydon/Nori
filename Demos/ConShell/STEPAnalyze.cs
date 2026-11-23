using Nori.STEP;
using static Nori.STEP.EKind;
namespace Nori;

public partial class STEPReader {
   public void Analyze () {
      Console.WriteLine ();
      foreach (var sd in ShapeDefn) {
         Console.WriteLine (sd);


         var kind = Data[sd.Target].Kind;
         if (kind == COMPOUND) LoadCompound (sd.Target);
         Console.WriteLine ($"  {Data[sd.Target].Kind} | {Data[sd.Shape].Kind}");
      }
   }

   void LoadCompound (int n) {
      var span = GetCompoundBytes (n);
      if (Seek (span, "REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION"u8)) {
         mR.Match ('(');
         int xfm = RRef ();
         Check (Seek (span, "REPRESENTATION_RELATIONSHIP"u8), 26);
         mR.Match ('('); RString (); RString (); int brep = RRef (); 


      }
   }
}
