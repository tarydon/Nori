// ────── ╔╗
// ╔═╦╦═╦╦╬╣ AuType.cs
// ║║║║╬║╔╣║ Contains AuType and AuField metadata classes used by the Au system
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections;
namespace Nori;
using static BindingFlags;

#region class AuType -------------------------------------------------------------------------------
/// <summary>AuType is a wrapper around System.Type that holds additional information needed by the Au system</summary>
/// The first time a type is read or written, the corresponding AuType wrapper object is
/// constructed. If it is a struct or class type, then for each field in the type we get
/// the 'tactics' for it (how it is to be serialized). For example, the 'Skip' tactic will
/// mean we don't serialize that field. The 'ByName' tactic means we serialize only the
/// 'Name' field of the subobject etc. These tactics are read in from the AuManifest.txt
/// file for each field of each class that is to be serialized.
class AuType {
   // Constructor --------------------------------------------------------------
   // Public constructor, accessed using Get(name) or Get(Type)
   AuType (Type type) {
      mType = type;
      Kind = Classify (type);
      mDict[mType] = this;
      const BindingFlags bfInstance = Instance | Public | NonPublic | DeclaredOnly;
      switch (Kind) {
         // Constructing a Struct or Class AuType requires us to build AuField wrappers
         // for all the fields we're going to write
         case EAuTypeKind.Struct or EAuTypeKind.Class:
            // Get all the base types (all the way to System.Object), so we can gather all the
            // fields from all of them
            List<Type> ancestry = [type];
            for (; ; ) {
               Type? parent = ancestry[^1].BaseType;
               if (parent == null || parent == typeof (object) || parent == typeof (ValueType)) break;
               ancestry.Add (parent);
            }
            if (!KnownTypes.Contains (Lib.NiceName (type)))
               throw new AuException ($"No metadata for {type.FullName}");
            // Next, gather all the fields (walking through all the way from the hierarchy).
            // Some simple conventions we follow:
            // - If the field name starts with _ we skip it (it is a cached or computed field)
            // - If the field name starts with m that is a 'member' prefix and we remove that
            //   m when serializing the field's name
            List<AuField> fields = [];
            foreach (var t in ancestry) {
               string tname = Lib.NiceName (t);
               foreach (var fi in t.GetFields (bfInstance).Except (AuField.SkipMetadata)) {
                  string fname = fi.Name;
                  if (fname.StartsWith ('m')) fname = fname[1..];
                  if (Tactics.TryGetValue ($"{tname}.{fname}", out var data)) {
                     if (data.Tactic != ECurlTactic.Skip)
                        fields.Add (new AuField (this, fi, data.Tactic, data.Sort));
                  } else
                     throw new AuException ($"Tactic missing for {t.FullName}.{fname}");
               }
            }
            mFields = [.. fields.OrderBy (a => a.Sort)];
            break;

         // For an AuPrimitive field, we find the reader and writer methods using
         // reflection and hold onto them
         case EAuTypeKind.AuPrimitive:
            switch (Lib.NiceName (type)) {
               case "Color4": mSkipValue = Color4.Nil; break;
            }
            break;

         // For .Net primitive types, we set up the 'skip value' (the default value
         // that is not serialized but implicit)
         case EAuTypeKind.Primitive:
            mSkipValue = Lib.NiceName (type) switch {
               "double" => 0.0, "float" => 0f, "bool" => false,
               "int" => 0, "short" => (short)0, "long" => (long)0,
               "uint" => (uint)0, "ushort" => (ushort)0, "ulong" => (ulong)0,
               _ => null
            };
            break;

         case EAuTypeKind.List:
            IsImmutableArray = type.Name == "ImmutableArray`1";
            break;
      }
   }

   public MethodInfo IArrayFromArray =>
      mFromArray ??= typeof (ImmutableArray).GetMethods ()
      .Single( a => a.GetParameters ().Length == 1 && a.GetParameters ()[0].ParameterType.IsArray)
      .MakeGenericMethod (mType.GetGenericArguments ()[0]);
   MethodInfo? mFromArray;

   public AuType[] GenericArgs {
      get {
         if (mGenericArgs == null) {
            if (mType.IsArray) mGenericArgs = [Get (mType.GetElementType ()!)];
            else mGenericArgs = [.. mType.GetGenericArguments ().Select (Get)];
         }
         return mGenericArgs;
      }
   }
   AuType[]? mGenericArgs;

   /// <summary>Get an AuType given the name (used during read serialization)</summary>
   /// When we see the name of a type in a Curl file, in parentheses, like "(Dwg2)",
   /// this method is called to fetch the AuType matching that name
   public static AuType Get (ReadOnlySpan<byte> name) {
      if (mByName.TryGetValue (name, out var aut)) return aut;
      var sname = Encoding.UTF8.GetString (name);
      foreach (var assy in Lib.Assemblies)
         foreach (var ns in Lib.Namespaces) {
            Type? type = assy.GetType ($"{ns}{sname}");
            if (type != null) {
               mByName.Add (sname, aut = new AuType (type));
               return aut;
            }
         }
      if (!Lib.Assemblies.Any ()) throw new AuException ("Nori.Lib.init() not called");
      throw new AuException ($"No metadata for '{sname}'");
   }
   static SymTable<AuType> mByName = new ();

   /// <summary>Get an AuType given the System.Type</summary>
   /// We maintain a static dictionary so each AuType is constructed only once during the
   /// lifetime of the application
   public static AuType Get (Type type) {
      AuType? aut = mDict.GetValueOrDefault (type);
      if (aut == null) {
         aut = new AuType (type);
         mByName.Add (Lib.NiceName (type), aut);
      }
      return aut;
   }
   static Dictionary<Type, AuType> mDict = [];

   // Properties --------------------------------------------------------------
   /// <summary>Set of fields in this type</summary>
   public ReadOnlySpan<AuField> Fields => mFields.AsSpan ();
   readonly ImmutableArray<AuField> mFields = [];

   /// <summary>What 'kind' of type is this? (Primmitive / List / Dict / Class etc)</summary>
   public readonly EAuTypeKind Kind;

   /// <summary>The underlying System.Type this is wrapped around</summary>
   public Type Type => mType;
   readonly Type mType;

   /// <summary>Is this an immutable array type?</summary>
   public readonly bool IsImmutableArray;

   /// <summary>The default value for this type (if field value equals this, we don't need to write it out)</summary>
   public object? SkipValue => mSkipValue;
   object? mSkipValue;     // If set, the 'default' value that we can skip writing out

   /// <summary>The list of all 'uplink' fields of this type</summary>
   public IReadOnlyList<AuField> Uplinks => mUplinks ??= [.. mFields.Where (a => a.Tactic == ECurlTactic.Uplink)];
   List<AuField>? mUplinks;

   // Methods ------------------------------------------------------------------
   /// <summary>Creates an instance of the object using its parameterless constructor</summary>
   public object CreateInstance () {
      try {
         return Activator.CreateInstance (mType)!;
      } catch (MissingMethodException) {
         throw new AuException ($"No parameterless constructor found for {mType.FullName}");
      }
   }

   /// <summary>Get a field of an object, given its name</summary>
   public AuField? GetField (ReadOnlySpan<byte> name) {
      if (mFieldDict == null) {
         mFieldDict = new ();
         foreach (var field in mFields) mFieldDict.Add (field.Name, field);
      }
      return mFieldDict.GetValueOrDefault (name);
   }
   SymTable<AuField>? mFieldDict;

   // Read methods -------------------------------------------------------------
   /// <summary>This is called if this type is an [AuPrimitive] type, reads the value in using the Read(UTFReader) method</summary>
   /// Classes tagged with [AuPrimitive] must implement a method like this:
   /// `static Poly Read (UTFReader R)`
   /// This routine finds this method and calls it. If the method does not exist in the type, an
   /// exception is thrown
   public object? ReadAuPrimitive (UTFReader stm) {
      const BindingFlags bfStatic = Static | Public | NonPublic | DeclaredOnly;
      mMIReader ??= mType.GetMethods (bfStatic).FirstOrDefault (a => a.Name == "Read"
         && a.GetParameters ().Length == 1
         && a.GetParameters ()[0].ParameterType == typeof (UTFReader)) ??
         throw new AuException ($"Missing {mType.FullName}.Read({nameof (UTFReader)})");
      return mMIReader.Invoke (null, [stm]);
   }
   MethodInfo? mMIReader;  // Pointer to the Read(UTFReader) method

   /// <summary>Handles the reading-in of objects that have been serialized "by name"</summary>
   /// Some fields (like the Ent2.Layer field, for example) are serialized "ByName". This
   /// means that when we write these out to a Curl file, we just write out the name of the layer.
   /// The writing out is fairly trivial, it is accomplished by calling the AuField.WriteByName
   /// method on the corresponding field.
   /// The reading back is a bit more complex. Given the name of an object, _looking up_ that object
   /// will need a different approach each time. For example, to fetch Layer2 object given its name,
   /// we need ot go to the enclosing Dwg2, get its Layers list and find the layer with the matching
   /// name. This method handles that process.
   /// Since the actual lookup cannot be generalized, we expect a type that will be serialized
   /// by name (like Layer2) to implement a method like this:
   /// `static Layer2 ByName(IReadOnlyList(object) stack, string name)`
   /// The 'stack' that is passed in is the list objects objects that are currently being read
   /// in from the Curl file (that is, objects where we have passed the opening { but not yet hit
   /// the closing }. The ByName routine will search in this stack to find a Dwg2, and then use
   /// that to get the Layer2 matching the name.
   public object? ReadByName (IReadOnlyList<object> stack, string name) {
      // If we don't have a pointer to the "ByName" method yet, fetch it
      const BindingFlags bfStatic = Static | Public | NonPublic | DeclaredOnly;
      mMIByName ??= mType.GetMethods (bfStatic).FirstOrDefault (
         a => a.Name == "ByName" &&
         a.GetParameters ().Length == 2 &&
         a.GetParameters ()[0].ParameterType == typeof (IReadOnlyList<object>) &&
         a.GetParameters ()[1].ParameterType == typeof (string)) ??
         throw new AuException ($"Missing {mType.FullName}.ByName(IReadOnlyList<object>,string)");
      return mMIByName.Invoke (null, [stack, name]);
   }
   MethodInfo? mMIByName;

   /// <summary>Handles the reading-in of an enum value from a Curl file</summary>
   /// For each Enum type, we build a symbol table that maps the enum tags to actual Enum values.
   /// This table is then used for a fast lookup
   public object ReadEnum (UTFReader stm) {
      if (mEnumMap == null) {
         mEnumMap = new ();
         var (names, values) = (Enum.GetNames (mType), Enum.GetValues (mType));
         for (int i = 0; i < names.Length; i++) mEnumMap.Add (names[i], NormalizedEnumInteger (mType, values.GetValue (i)!));
      }
      var desc = stm.TakeUntil (CurlReader.NameStop, true);
      if (desc[0] is >= (byte)'0' and <= (byte)'9') // Outlier enum value
         return ReconstructEnumObject (ulong.Parse (desc));
      if (mEnumMap.TryGetValue (desc, out var value))
         return ReconstructEnumObject (value);
      if (mType.HasAttribute<FlagsAttribute> ()) {
         ulong bits = 0;
         foreach (var r in desc.Split ((byte)','))
            bits |= mEnumMap[desc[r]];
         return ReconstructEnumObject (bits);
      }
      throw new NotImplementedException (mType.FullName!);

      // Reconstructs enum's backing-type compatible integral value object, given normalized value
      object ReconstructEnumObject (ulong v) {
         return Type.GetTypeCode (mType) switch {
            TypeCode.Int32 => Enum.ToObject (mType, (int)v), // Default backing type
            TypeCode.UInt32 => Enum.ToObject (mType, (uint)v),
            TypeCode.Int64 => Enum.ToObject (mType, (long)v),
            TypeCode.UInt64 => Enum.ToObject (mType, v),
            TypeCode.Int16 => Enum.ToObject (mType, (short)v),
            TypeCode.UInt16 => Enum.ToObject (mType, (ushort)v),
            TypeCode.Byte => Enum.ToObject (mType, (byte)v),
            TypeCode.SByte => Enum.ToObject (mType, (sbyte)v),
            _ => throw new BadCaseException (mType.FullName!)
         };
      }
   }
   SymTable<ulong>? mEnumMap;

   /// <summary>Normalizes given enum's (integral) value to UInt64</summary>
   static ulong NormalizedEnumInteger (Type enumType, object o) {
      return Type.GetTypeCode (enumType) switch { // Note: Unboxing needs exact cast
         TypeCode.Int32 => (ulong)(int)o, // Default backing type
         TypeCode.UInt32 => (uint)o,
         TypeCode.Int64 => (ulong)(long)o,
         TypeCode.UInt64 => (ulong)o,
         TypeCode.Int16 => (ulong)(short)o,
         TypeCode.UInt16 => (ushort)o,
         TypeCode.Byte => (byte)o,
         TypeCode.SByte => (ulong)(sbyte)o,
         _ => throw new BadCaseException (enumType.FullName!)
      };
   }

   // Write methods ------------------------------------------------------------
   /// <summary>Writes an object of a type that is tagged as [AuPrimitive]</summary>
   /// Such a type will implement the `Write(UTFWriter)` method that this code below
   /// will find and invoke.
   public void WriteAuPrimitive (UTFWriter stm, object value) {
      const BindingFlags bfInstance = Instance | Public | NonPublic | DeclaredOnly;
      mMIWriter ??= mType.GetMethods (bfInstance).FirstOrDefault (a => a.Name == "Write"
         && a.GetParameters ().Length == 1
         && a.GetParameters ()[0].ParameterType == typeof (UTFWriter)) ??
         throw new AuException ($"Missing {mType.FullName}.Write({nameof (UTFWriter)})");
      mMIWriter.Invoke (value, [stm]);
   }
   MethodInfo? mMIWriter;  // Pointer to the Write(UTFWriter) method

   /// <summary>Writes out a type override to Curl file, like "(E2Poly)"</summary>
   /// We will often have polymorphic collections that is declared with a base type, but
   /// stores objects of derived types. For example: Dwg.Ents is a List(Ent2) but stores
   /// types derived from Ent2. To deserialize such collections correctly, we need to
   /// write out the _actual_ type of each object before writing out the object. This type
   /// name is then used as hint to construct the correct type of object during reading.
   public void WriteOverride (UTFWriter buffer) {
      mBName ??= Encoding.UTF8.GetBytes (Lib.NiceName (Type));
      buffer.Write ('(').Write (mBName).Write (')');
   }
   byte[]? mBName;

   /// <summary>Writes out an object that is a .Net primitive</summary>
   public void WritePrimitive (UTFWriter stm, object value) {
      switch (Type.GetTypeCode (mType)) {
         case TypeCode.Char: stm.Write ((char)value); break;
         case TypeCode.Boolean: stm.Write ((bool)value); break;
         case TypeCode.Int16: stm.Write ((short)value); break;
         case TypeCode.Int32: stm.Write ((int)value); break;
         case TypeCode.Int64: stm.Write ((long)value); break;
         case TypeCode.String: stm.Write ((string)value); break;
         case TypeCode.Double:
            double f = (double)value; if (Lib.Testing) f = f.Round (6);
            stm.Write (f);
            break;
         case TypeCode.Single: stm.Write ((float)value); break;
         case TypeCode.UInt16: stm.Write ((ushort)value); break;
         case TypeCode.UInt32: stm.Write ((uint)value); break;
         case TypeCode.UInt64: stm.Write ((ulong)value); break;
         case TypeCode.DateTime: stm.Write ((DateTime)value); break;
         default:
            if (value is Guid guid) { stm.Write (guid); break; }
            if (value is TimeSpan tspan) { stm.Write (tspan); break; }
            throw new BadCaseException (mType.FullName!);
      }
   }

   /// <summary>Writes out an enumeration value</summary>
   /// To avoid calling ToString() on the value and building a short-lived temporary string,
   /// we build a map that maps each enumeration value (integer) into a byte[] that is a
   /// UTF8 encoding of the string.
   public void WriteEnum (UTFWriter stm, object value) {
      mEnumDescs = [];
      var (names, values) = (Enum.GetNames (mType), Enum.GetValues (mType));
      for (int i = 0; i < names.Length; i++)
         mEnumDescs.TryAdd (NormalizedEnumInteger (mType, values.GetValue (i)!), Encoding.UTF8.GetBytes (names[i]));

      if (mEnumDescs.TryGetValue (NormalizedEnumInteger (mType, value), out var desc)) {
         stm.Write (desc);
         return;
      }
      if (mType.HasAttribute<FlagsAttribute> ()) {
         ulong bits = NormalizedEnumInteger (mType, value);
         int cBit = Type.GetTypeCode (mType) switch {
            TypeCode.Int32 or TypeCode.UInt32 => 32, // Default backing type
            TypeCode.Int16 or TypeCode.UInt16 => 16,
            TypeCode.Byte or TypeCode.SByte => 8,
            TypeCode.Int64 or TypeCode.UInt64 => 64,
            _ => throw new BadCaseException (mType.FullName!)
         };
         // Valid bit-field value: Composed of some combination of defined bits.
         //    Everything else is considered an arbirary value, which is serialized as is.
         bool gotValid = Enumerable.Range (0, cBit).Select (i => (ulong)1 << i).Where (b => (bits & b) != 0).All (k => mEnumDescs.ContainsKey (k));
         if (!gotValid) {
            stm.Write (bits);
            return;
         }
         bool comma = false;
         for (int i = 0; i < cBit; i++) {
            if ((bits & ((ulong)1 << i)) != 0) {
               if (comma) stm.Write (',');
               stm.Write (mEnumDescs[(ulong)1 << i]);
               comma = true;
            }
         }
         return;
      }
      if (!Enum.IsDefined (mType, value)) {
         stm.Write (NormalizedEnumInteger (mType, value));
         return;
      }
      throw new NotImplementedException ();
   }
   Dictionary<ulong, byte[]>? mEnumDescs;

   // Implementation -----------------------------------------------------------
   // Classifies this type (computes the Kind property)
   static EAuTypeKind Classify (Type type) {
      if (type == typeof (object)) return EAuTypeKind.Object;
      if (type == typeof (string) || type.IsPrimitive) return EAuTypeKind.Primitive;
      if (type.IsEnum) {
         var utype = Type.GetTypeCode (type.GetEnumUnderlyingType ());
         if (utype is TypeCode.Int64 or TypeCode.UInt64) throw new AuException ("64-bit enums are not supported");
         return EAuTypeKind.Enum;
      }
      if (type.HasAttribute<AuPrimitiveAttribute> ()) return EAuTypeKind.AuPrimitive;
      if (type.IsAssignableTo (typeof (IList))) return EAuTypeKind.List;
      if (type.IsAssignableTo (typeof (IDictionary))) return EAuTypeKind.Dictionary;
      if (type == typeof (DateTime) || type == typeof (TimeSpan) || type == typeof (Guid)) return EAuTypeKind.Primitive;
      if (type.IsClass) return EAuTypeKind.Class;
      return EAuTypeKind.Struct;
   }

   // Tactics for each field of each type (loaded from the AuManifest first time it is accesssed)
   static Dictionary<string, (ECurlTactic Tactic, int Sort)> Tactics {
      get {
         if (mTactic == null) {
            mTactic = []; mKnownTypes = ["object"];
            AddTactics (Lib.ReadLines ("nori:AuManifest.txt"));
            mByName.Add ("Single", Get (typeof (float)));
         }
         return mTactic;
      }
   }
   static Dictionary<string, (ECurlTactic Tactic, int Sort)>? mTactic;

   static HashSet<string> KnownTypes { get { _ = Tactics; return mKnownTypes; } }
   static HashSet<string> mKnownTypes = [];

   public static void AddTactics (string[] text) {
      string tname = ""; int n = 0;
      var tactics = Tactics;
      foreach (var line in text) {
         if (line.IsBlank ()) continue;
         if (line.StartsWith (' ')) {
            string[] words = line.Split (' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var word in words) {
               var (w, tactic) = (word, ECurlTactic.Std);
               if (w[0] == '-') (w, tactic) = (w[1..], ECurlTactic.Skip);
               else if (w[0] == '^') (w, tactic) = (w[1..], ECurlTactic.Uplink);
               else if (w.EndsWith (".Name")) (w, tactic) = (w[..^5], ECurlTactic.ByName);
               tactics.Add ($"{tname}.{w}", (tactic, ++n));
            }
         } else {
            tname = line.Trim ();
            mKnownTypes.Add (tname);
         }
      }
   }

   public override string ToString () => $"AuType {Lib.NiceName (mType)}";
}
#endregion

#region class AuField ------------------------------------------------------------------------------
/// <summary>A wrapper around System.FieldInfo that holds additional information needed by the Au system</summary>
/// For example, this holds the serialization _tactic_ for this particular field
class AuField {
   // Constructor --------------------------------------------------------------
   public AuField (AuType owner, FieldInfo fi, ECurlTactic tactic, int sort) {
      Name = (mFI = fi).Name;
      mOwner = owner; Tactic = tactic; Sort = sort;
      if (Name.StartsWith ('m')) Name = Name[1..];
      mFieldType = AuType.Get (mFI.FieldType);
      IsNullable = mFI.HasAttribute<NullableAttribute> ();
      IsAngle = mFI.HasAttribute<RadianAttribute> ();
   }
   readonly AuType mOwner;

   // properties ---------------------------------------------------------------
   /// <summary>Is this field an angle in radians?</summary>
   public readonly bool IsAngle;

   /// <summary>The AuType wrapper for the underlying type of this field</summary>
   public AuType FieldType => mFieldType;
   readonly AuType mFieldType;

   /// <summary>Name of this field</summary>
   public readonly string Name;

   /// <summary>It this field nullable?</summary>
   public readonly bool IsNullable;

   /// <summary>Sort order of this field within the enclosing type</summary>
   public readonly int Sort;

   /// <summary>The tactics used for this field when writing to curl file (Skip / Std / ByName etc)</summary>
   public readonly ECurlTactic Tactic;

   // Methods ------------------------------------------------------------------
   /// <summary>Reads the value from this field (given the container object)</summary>
   public object? GetValue (object parent) => mFI.GetValue (parent);
   readonly FieldInfo mFI;

   /// <summary>Sets the value into this field (given the parent object, and the value to write)</summary>
   public void SetValue (object parent, object? value) => mFI.SetValue (parent, value);

   /// <summary>Can a given field be skipped when gathering metadata?</summary>
   /// This method is called for each FieldInfo in a type when we are building up the
   /// AuType wrapper for that type. At that point, we decide to skip fields that never have
   /// to be serialized
   /// - Field names that start with _ are skipped by convention
   /// - Field name that are delegate types, events, observable pattern implementation helpers
   ///   etc are skipped
   public static bool SkipMetadata (FieldInfo fi) {
      if (fi.Name.StartsWith ('_')) return true;
      if (fi.FieldType.Name == "Subject`1") return true;
      return false;
   }

   /// <summary>Returns true if this field can be skipped when writing</summary>
   /// A field is skipped if its value is null, or if the value matches the 'default skipvalue'
   /// of the underlying type (for example, Color4.Nil is the skip value for the Color4 type)
   public bool SkipWriting ([NotNullWhen (false)] object? value) {
      if (value == null || Tactic == ECurlTactic.Uplink) return true;
      if (Equals (value, mFieldType.SkipValue)) return true;
      return false;
   }

   /// <summary>Writes out the field name followed by a colon, like "Center:"</summary>
   public void WriteLabel (UTFWriter buf)
      => buf.Write (mBName ??= Encoding.UTF8.GetBytes (Name)).Write (':');
   byte[]? mBName;

   /// <summary>Writes out this field by name</summary>
   /// This finds the Name property / field of the underlying object and writes it out
   public void WriteByName (UTFWriter buf, object obj) {
      const BindingFlags bf = Public | NonPublic | DeclaredOnly | Instance;
      mFIName ??= mFieldType.Type.GetField ("Name", bf)
         ?? mFieldType.Type.GetField ("mName", bf)
         ?? throw new AuException ($"Missing field {Lib.NiceName (mFieldType.Type)}.Name");
      buf.Write ((string)mFIName.GetValue (obj)!);
   }
   FieldInfo? mFIName;

   // Implementation -----------------------------------------------------------
   public override string ToString ()
      => $"AuField {Lib.NiceName (mFI.FieldType)} {Lib.NiceName (mOwner.Type)}.{Name}";
}
#endregion

#region enum EAuTypeKind ---------------------------------------------------------------------------
/// <summary>What 'Kind' of type is represented by a given AuType (primitive / list / enum / dict / class etc)</summary>
enum EAuTypeKind { Unknown, Object, Primitive, AuPrimitive, Enum, List, Dictionary, Struct, Class }
#endregion

#region enum ECurlTactic ---------------------------------------------------------------------------
/// <summary>The curl tactics to be used for a particular field</summary>
enum ECurlTactic { Std, Skip, ByName, ById, Uplink }
#endregion
