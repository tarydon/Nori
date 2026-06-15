// ────── ╔╗
// ╔═╦╦═╦╦╬╣ AgType.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections;
namespace Nori;

class AgType {
   // Construtors --------------------------------------------------------------
   /// <summary>Returns the AgType corresponding to a current live type</summary>
   public static AgType Get (Type type) 
      => mByType.GetValueOrDefault (type) ?? new AgType (type);

   AgType (Type type) {
      Name = (Type = type).FullName!;
      mByName.Add (Name, this); mByType.Add (type, this);
      var baseType = type.BaseType;
      if (baseType != typeof (object) && baseType != typeof (ValueType) && baseType != null)
         Base = Get (baseType);
      _ = Kind;
   }

   public AgType (string name, int version) {
      Name = name; Version = version; 
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The base-type (only if this class has a base type)</summary>
   public AgType? Base;

   /// <summary>
   /// What kind of type of this (each has its own serialization mechanism)
   /// </summary>
   public EKind Kind {
      get {
         if (mKind == EKind.Unknown) {
            mKind = ComputeKind (Type!);
            if (mKind is EKind.Class or EKind.Dict or EKind.List) mFlags |= EFlags.ReferenceType;
         }
         return mKind;

         // Helper .........................................
         static EKind ComputeKind (Type t) {
            if (t.IsEnum) return EKind.Enum;
            if (t.IsPrimitive) return EKind.Primitive;
            if (t == typeof (string) || t == typeof (Guid) || t == typeof (DateTime)) return EKind.Primitive;
            if (t.GetInterfaces ().Contains (typeof (ICollection))) return EKind.List;
            if (t.GetInterfaces ().Contains (typeof (IDictionary))) return EKind.Dict;
            if (t.GetCustomAttribute<AuPrimitiveAttribute> () is { }) return EKind.AuPrimitive;
            return t.IsValueType ? EKind.Struct : EKind.Class;
         }
      }
   }
   EKind mKind;

   public bool IsReferenceType => (mFlags & EFlags.ReferenceType) != 0;

   public static AgType Object => mObject ??= new (typeof (object));
   static AgType? mObject;

   /// <summary>The full name of the type</summary>
   public readonly string Name;

   /// <summary>The underlying Type (maybe null if this is a stub type)</summary>
   public readonly Type? Type;

   /// <summary>The version number of this type</summary>
   public readonly int Version;

   // Nested types -------------------------------------------------------------
   public enum EKind {
      Unknown,       // Unknown (this is the case when the type is just created)
      Class,         // A class (reference type) with fields
      Struct,        // A struct (value type) with fields
      Primitive,     // A primitive .Net type
      AuPrimitive,   // A domain type that is tagged with [AuPrimitive] attribute
      List,          // This is a list or array (or any one-dimensional vector type)
      Dict,          // This is a dictionary (associative array with key / value types)
      Enum,          // An enumeration type
   }

   [Flags]
   enum EFlags {
      ReferenceType = 0x1
   }
   EFlags mFlags;

   // Private data -------------------------------------------------------------
   static Dictionary<Type, AgType> mByType = [];
   static Dictionary<string, AgType> mByName = [];
}
