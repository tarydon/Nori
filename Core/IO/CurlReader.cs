// ────── ╔╗
// ╔═╦╦═╦╦╬╣ CurlReader.cs
// ║║║║╬║╔╣║ CurlReader: reads an object from a curl file
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Buffers;
using System.Collections;
namespace Nori;

#region class CurlReader ---------------------------------------------------------------------------
/// <summary>CurlReader is used to read an object from an AuCurl file</summary>
public class CurlReader {
   // Methods ------------------------------------------------------------------
   /// <summary>Load an object from an array of bytes</summary>
   public static object FromByteArray (byte[] bytes) {
      CurlReader r = new (new (bytes));
      return r.ReadClass (AuType.Get (typeof (object)));
   }

   /// <summary>Load an object from a file</summary>
   public static object FromFile (string file)
      => FromByteArray (File.ReadAllBytes (file));

   // Properties ---------------------------------------------------------------
   /// <summary>These are the characters that stop parsing an identifier</summary>
   public static SearchValues<byte> NameStop => mNameStop;
   static readonly SearchValues<byte> mNameStop = SearchValues.Create ("\r\n\t\f :([{}])="u8);

   // Implementation -----------------------------------------------------------
   // Construct a UTFReader, and skips past any leading comments
   CurlReader (UTFReader r) { R = r; while (R.Peek == ';') R.SkipTo ('\n'); }
   readonly UTFReader R;

   // Top level routine used to read an object given the AuType, just switches based
   // on the Kind of type to the appropriate low level read routine
   object? Read (AuType type) => type.Kind switch {
      EAuTypeKind.Class or EAuTypeKind.Struct => ReadClass (type),
      EAuTypeKind.List => ReadList (type),
      EAuTypeKind.Primitive => ReadPrimitive (type),
      EAuTypeKind.AuPrimitive => ReadAuPrimitive (type),
      EAuTypeKind.Enum => type.ReadEnum (R),
      EAuTypeKind.Dictionary => ReadDictionary (type),
      EAuTypeKind.Object => ReadObject (),
      _ => throw new NotImplementedException ()
   };

   object? ReadObject () {
      R.Match ('('); var auType = AuType.Get (R.TakeUntil (')'));
      return Read (auType);
   }

   // Reads a class or a struct
   object ReadClass (AuType auType) {
      // Optionally, there may be a type override before the actual object data starts,
      // so read that first
      if (R.TryMatch ('(')) auType = AuType.Get (R.TakeUntil (')'));
      if (auType.Kind is not EAuTypeKind.Class and not EAuTypeKind.Struct)
         return Read (auType)!;

      // Create an object, and push it on the stack of objects being partially read,
      // we will need that to handle 'uplink' fields (and sometimes 'byname' fields)
      object owner = auType.CreateInstance ();
      foreach (var upf in auType.Uplinks) {
         var uptype = upf.FieldType.Type;
         var uplink = mStack.LastOrDefault (a => a?.GetType ().IsAssignableFrom (uptype) ?? false);
         if (!upf.IsNullable && uplink == null) throw new AuException ($"{auType.Type.FullName}.{upf.Name} cannot be set to null");
         upf.SetValue (owner, uplink);
      }

      mStack.Add (owner);
      R.Match ('{');
      for (; ; ) {
         if (R.TryMatch ('}')) break;     // Finished reading all the fields
         var fieldName = R.TakeUntil (mNameStop, true);
         var field = auType.GetField (fieldName) ?? throw new AuException ($"Field {Encoding.UTF8.GetString (fieldName)} not found in {auType.Type.FullName}");
         R.Match (':');
         object? value;
         switch (field.Tactic) {
            case ECurlTactic.ByName:
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
      bool makeArray = type.IsArray || auType.IsImmutableArray;
      IList list = makeArray ? new List<object> () : (IList)auType.CreateInstance ();
      var elemType = auType.GenericArgs[0];
      R.Match ('[');
      for (; ; ) {
         if (R.TryMatch (']')) break;
         list.Add (Read (elemType));
      }
      if (!makeArray) return list;
      var array = Array.CreateInstance (elemType.Type, list.Count);
      list.CopyTo (array, 0);
      if (type.IsArray) return array;
      return auType.IArrayFromArray.Invoke (null, [array]);
   }

   object ReadDictionary (AuType auType) {
      var type = auType.Type;
      IDictionary dict = (IDictionary)auType.CreateInstance ();
      R.Match ('<');
      AuType keyType = auType.GenericArgs[0], valueType = auType.GenericArgs[1];
      for (; ; ) {
         if (R.TryMatch ('>')) break;
         object key = Read (keyType)!;
         R.Match ('=');
         object? value = Read (valueType);
         dict.Add (key, value);
      }
      return dict;
   }

   // Reads an [AuPrimitive] from the curl file
   // The underlying auType implements the ReadAuPrimitive that has a cached pointer
   // to the Read(UTFReader) method on the type and that is used to read the primitive
   object? ReadAuPrimitive (AuType auType) => auType.ReadAuPrimitive (R);

   // Reads a .Net primitve type from the curl file
   // This is handled by the UTFReader.ReadPrimitive core method
   object ReadPrimitive (AuType auType) => R.ReadPrimitive (auType.Type);
}
#endregion
