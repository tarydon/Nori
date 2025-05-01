// ────── ╔╗
// ╔═╦╦═╦╦╬╣ AuReader.cs
// ║║║║╬║╔╣║ AuReader: reads an object from a curl file
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Buffers;
using System.Collections;
namespace Nori;

#region class AuReader -----------------------------------------------------------------------------
/// <summary>AuReader is used to read an object from an AuCurl file</summary>
public class AuReader {
   // Methods ------------------------------------------------------------------
   /// <summary>Load an object from a file</summary>
   public static object Load (string file)
      => Load (File.ReadAllBytes (file));

   /// <summary>Load an object from an array of bytes</summary>
   public static object Load (byte[] bytes) {
      AuReader r = new (new (bytes));
      return r.ReadClass (AuType.Get (typeof (object)));
   }

   // Properties ---------------------------------------------------------------
   /// <summary>These are the characters that stop parsing an identifier</summary>
   public static SearchValues<byte> NameStop => mNameStop;
   static readonly SearchValues<byte> mNameStop = SearchValues.Create ("\n\t\f :([{}])"u8);

   // Implementation -----------------------------------------------------------
   // Construct a UTFReader, and skips past any leading comments
   AuReader (UTFReader r) { R = r; while (R.Peek == ';') R.SkipTo ('\n'); }
   readonly UTFReader R;

   // Top level routine used to read an object given the AuType, just switches based
   // on the Kind of type to the appropriate low level read routine
   object? Read (AuType type) => type.Kind switch {
      EAuTypeKind.Class or EAuTypeKind.Struct => ReadClass (type),
      EAuTypeKind.List => ReadList (type),
      EAuTypeKind.Primitive => ReadPrimitive (type),
      EAuTypeKind.AuPrimitive => ReadAuPrimitive (type),
      EAuTypeKind.Enum => type.ReadEnum (R),
      _ => throw new NotImplementedException (),
   };

   // Reads a class or a struct
   object ReadClass (AuType auType) {
      // Optionally, there may be a type override before the actual object data starts,
      // so read that first
      if (R.TryMatch ('(')) auType = AuType.Get (R.TakeUntil (')'));

      // Create an object, and push it on the stack of objects being partially read,
      // we will need that to handle 'uplink' fields (and sometimes 'byname' fields)
      object owner = auType.CreateInstance ();
      mStack.Add (owner);
      R.Match ('{');
      for (; ; ) {
         if (R.TryMatch ('}')) break;     // Finished reading all the fields
         var field = auType.GetField (R.TakeUntil (mNameStop, true))!;
         R.Match (':');
         object? value;
         switch (field.Tactic) {
            case EAuCurlTactic.ByName:
               R.Read (out string str);
               value = field.FieldType.ReadByName (mStack, str);
               break;
            default: value = Read (field.FieldType); break;
         }
         field.SetValue (owner, value);
      }
      // Pop off the stack of partially read objects and return
      return mStack.RemoveLast ();
   }
   List<object> mStack = [];

   // Reads a list (or any one-dimensional collection) from the curl file
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

   // Reads an [AuPrimitive] from the curl file
   // The underlying auType implements the ReadAuPrimitive that has a cached pointer
   // to the Read(UTFReader) method on the type and that is used to read the primitive
   object? ReadAuPrimitive (AuType auType) => auType.ReadAuPrimitive (R);

   // Reads a .Net primitve type from the curl file
   // This is handled by the UTFReader.ReadPrimitive core method
   object ReadPrimitive (AuType auType) => R.ReadPrimitive (Type.GetTypeCode (auType.Type));
}
#endregion
