using System.Buffers;
using System.Collections;
namespace Nori;

public class AuReader {
   public static object? Load (string file) {
      AuReader r = new (new (file));
      return r.ReadClass (AuType.Get (typeof (object)));
   }

   AuReader (UTFReader r) {
      R = r;
      while (R.Peek == ';') R.SkipTo ('\n');
   }
   readonly UTFReader R;

   object? Read (AuType type) {
      switch (type.Kind) {
         case EAuTypeKind.Class or EAuTypeKind.Struct: return ReadClass (type);
         case EAuTypeKind.List: return ReadList (type);
         case EAuTypeKind.Primitive: return ReadPrimitive (type);
         case EAuTypeKind.AuPrimitive: return ReadAuPrimitive (type);
         case EAuTypeKind.Enum: return type.ReadEnum (R);
         default: throw new NotImplementedException ();
      }
   }

   object? ReadClass (AuType auType) {
      if (R.TryMatch ('(')) auType = AuType.Get (R.TakeUntil (')'));
      object owner = auType.CreateInstance ();
      mStack.Add (owner);
      R.Match ('{');
      for (; ; ) {
         if (R.TryMatch ('}')) break;     // Finished reading all the fields
         var name = R.TakeUntil (mNameStop, true);
         var field = auType.GetField (name)!;
         R.Match (':');
         object? value = null;
         switch (field.Tactic) {
            case EAuCurl.ByName:
               R.Read (out string str);
               value = field.AuType.ByName (mStack, str);
               break;
            default: value = Read (field.AuType); break;
         }
         field.SetValue (owner, value);
      }
      return mStack.RemoveLast ();
   }
   List<object> mStack = [];

   object? ReadList (AuType auType) {
      var type = auType.Type;
      IList list = type.IsArray ? new List<object> () : (IList)auType.CreateInstance ();
      var elemType = AuType.Get (type.IsArray ? type.GetElementType ()! : type.GetGenericArguments ()[0]);
      R.Match ('[');
      for (; ; ) {
         if (R.TryMatch (']')) break;
         list.Add (Read (elemType));
      }
      if (type.IsArray) {
         var array = Array.CreateInstanceFromArrayType (type, list.Count);
         list.CopyTo (array, 0);
         return array;
      }
      return list;
   }

   object? ReadAuPrimitive (AuType auType)
      => auType.ReadAuPrimitive (R);

   object ReadPrimitive (AuType auType)
      => R.ReadPrimitive (Type.GetTypeCode (auType.Type));

   public static SearchValues<byte> NameStop => mNameStop;
   static readonly SearchValues<byte> mNameStop
      = SearchValues.Create (Encoding.UTF8.GetBytes (" :(\n\t\f[{"));
}
