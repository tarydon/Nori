// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CurlWriter.cs
// ║║║║╬║╔╣║ CurlWriter: Writes an object out to a Curl file
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections;
namespace Nori;

#region class CurlWriter ---------------------------------------------------------------------------
/// <summary>AuWriter is used to write out an object to a Curl file</summary>
/// If the type is a class or struct, we need metadata for the type, and this is
/// loaded from AuManifest files
public class CurlWriter {
   // Methods ------------------------------------------------------------------
   /// <summary>Write an object (with a possible leading comment) to a byte[]</summary>
   public static byte[] WriteToSpan (object obj, string? comment = null) {
      var w = new CurlWriter ();
      if (comment != null) w.B.Write ("; "u8).Write (comment).Write ('\n');
      w.Write (obj, AuType.Get (typeof (object)));
      return w.B.IndentAndReturn ();
   }

   /// <summary>Write an object (with a possible header comment) to a file</summary>
   public static void WriteToFile (object obj, string file, string? comment = null)
      => File.WriteAllBytes (file, WriteToSpan (obj, comment));

   // Implementation -----------------------------------------------------------
   // Recursive routine that writes out any object
   void Write (object? obj, AuType nominal) {
      if (obj == null) return;
      AuType at = nominal;
      if (nominal.Kind is EAuTypeKind.Object or EAuTypeKind.Class)
         at = AuType.Get (obj.GetType ());
      if (at != nominal) at.WriteOverride (B);
      switch (at.Kind) {
         // Write out a class or struct. This is delimited by { } and contains a list of
         // fields expressed as Name:Value pairs. Before we start writing out the object,
         // we may write out an optional type override like "(E2Poly)" if that will be needed
         // during the read-back to disambiguate
         case EAuTypeKind.Class or EAuTypeKind.Struct:
            B.Write ("{\n"u8);
            foreach (var af in at.Fields) {
               object? value = af.GetValue (obj);
               if (af.SkipWriting (value)) continue;
               af.WriteLabel (B);
               switch (af.Tactic) {
                  case ECurlTactic.ByName: af.WriteByName (B, value); break;
                  default: Write (value, af.FieldType); break;
               }
               B.NewLine ();
            }
            B.Write ("}\n"u8);
            break;

         // Write out a list. This is delimited by [ ] and contains the actual objects written out
         // between these delimiters. We could just call Write recursively here to write out all the
         // elements, but we expand out the loop to improve performance. That is, if we know that
         // the elements are AuPrimitive, we can bypass some of the logic above by directly calling
         // WriteAuPrimitive as we do here
         case EAuTypeKind.List:
            B.Write ("[\n"u8);
            Type elemType = at.Type.IsArray ? at.Type.GetElementType ()! : at.Type.GetGenericArguments ()[0];
            AuType elemAuType = AuType.Get (elemType);
            // We could just call Write recursively here to write out all the elements. However,
            // to improve performance we expand that loop out here
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

         case EAuTypeKind.Dictionary:
            B.Write ("<\n"u8);
            var targs = at.GenericArgs;
            var idict = (IDictionary)obj;
            foreach (var key in idict.Keys) {
               Write (key, targs[0]); B.Write ('='); Write (idict[key], targs[1]); B.NewLine ();
            }
            B.Write (">\n"u8);
            break;

         // The AuPrimitive, Primitive and Enum kinds are written out by calling the
         // appropriate methods in the underlying AuType. Those methods use reflection to pick
         // up the corresponding write methods (which are cached) and then invoke them
         case EAuTypeKind.AuPrimitive: at.WriteAuPrimitive (B, obj); break;
         case EAuTypeKind.Primitive: at.WritePrimitive (B, obj); break;
         case EAuTypeKind.Enum: at.WriteEnum (B, obj); break;
         default: throw new NotImplementedException ();
      }
   }

   UTFWriter B = new ();
}
#endregion
