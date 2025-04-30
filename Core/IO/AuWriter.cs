// ╔═╦╗
// ║╬╠╬╦╗ AuWriter.cs
// ║╔╣╠║╣ <<TODO>>
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Collections;
using Nori;

public class AuWriter {
   public static byte[] Write (object obj, string? comment)
      => new AuWriter ().WriteImp (obj, comment);

   public static void Write (object obj, string? comment, string file)
      => File.WriteAllBytes (file, Write (obj, comment));

   byte[] WriteImp (object obj, string? comment) {
      if (comment != null) B.Write ("; "u8).Write (comment).Write ('\n');
      Write (obj, AuType.Get (typeof (object)));
      return B.IndentAndReturn ();
   }

   void Write (object? obj, AuType nominal) {
      if (obj == null) return;
      AuType at = AuType.Get (obj.GetType ());
      switch (at.Kind) {
         case EAuTypeKind.Class or EAuTypeKind.Struct:
            if (at != nominal) at.WriteOverride (B);
            B.Write ("{\n"u8);
            foreach (var af in at.Fields) {
               object? value = af.GetValue (obj);
               if (af.Skip (value)) continue;
               af.WriteLabel (B);
               switch (af.Tactic) {
                  case EAuCurl.ByName: af.WriteByName (B, value); break;
                  default: Write (value, af.AuType); break;
               }
               B.NewLine ();
            }
            B.Write ("}\n"u8);
            break;
         case EAuTypeKind.List:
            B.Write ("[\n"u8);
            Type elemType = at.Type.IsArray ? at.Type.GetElementType ()! : at.Type.GetGenericArguments ()[0];
            AuType elemAuType = AuType.Get (elemType);
            switch (elemAuType.Kind) {
               case EAuTypeKind.AuPrimitive:
                  foreach (var elem in (IList)obj) { elemAuType.WriteAuPrimitive (B, elem); B.NewLine (); }
                  break;
               case EAuTypeKind.Primitive:
                  foreach (var elem in (IList)obj) { elemAuType.WritePrimitive (B, elem); B.NewLine (); }
                  break;
               case EAuTypeKind.Enum:
                  foreach (var elem in (IList)obj) { elemAuType.WriteEnum (B, elem); B.NewLine (); }
                  break;
               default:
                  var subtype = AuType.Get (elemType);
                  foreach (var elem in (IList)obj) { Write (elem, subtype); B.NewLine (); }
                  break;
            }
            B.Write ("]\n"u8);
            break;
         case EAuTypeKind.AuPrimitive: at.WriteAuPrimitive (B, obj); break;
         case EAuTypeKind.Primitive: at.WritePrimitive (B, obj); break;
         case EAuTypeKind.Enum: at.WriteEnum (B, obj); break;
         default: throw new NotImplementedException ();
      }
   }

   UTFWriter B = new ();
   HashSet<AuType> mSeen = [];
}
