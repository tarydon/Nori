namespace Nori;

enum EAgToken {
   Null = 1,            // A null pointer or reference
   NominalType = 2,     // An object follows that is of the nominal or expected type
   DerivedType = 3,     // An object follows that is of a derived type
   ObjectRef = 4,       // Reference to an object that has already appeared in this stream
   PrimitiveType = 5,   // Type descriptor for a primitive type follows
   ClassType = 6,       // Type descriptor for a class type follows
   StructType = 7,      // Type descriptor for a struct type follows
   ListType = 8,        // Type descriptor for a list type follows
   DictType = 9,        // Type descriptor for a dictionary type follows
   EnumType = 10,       // Type descriptor for an enumeration type follows
   Epilogue = 11,       // File epilogue follows, including object count and EOF marker
}
