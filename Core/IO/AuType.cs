using System.Collections;
namespace Nori;
using static System.Reflection.BindingFlags;

// What 'kind' of type is represented by a given AuType (primitive / list / enum / class etc)
enum EAuTypeKind { Unknown, Primitive, AuPrimitive, Enum, List, Dictionary, Struct, Class };

enum EAuCurl { Std, Skip, ByName, Uplink, }

class AuType {
   AuType (Type type) {
      mDict[mType = type] = this;
      Kind = Classify (type);
      const BindingFlags bfi = Instance | Public | NonPublic | DeclaredOnly;
      const BindingFlags bfs = Static | Public | NonPublic | DeclaredOnly;
      switch (Kind) {
         case EAuTypeKind.Struct or EAuTypeKind.Class:
            // Get all the base types (all the way to System.Object), so we can gather all the
            // fields from all of them
            List<Type> ancestry = [type];
            for (; ; ) {
               Type? parent = ancestry[^1].BaseType;
               if (parent == null || parent == typeof (object) || parent == typeof (ValueType)) break;
               ancestry.Add (parent);
            }
            List<AuField> fields = [];
            foreach (var t in ancestry) {
               string tname = Lib.NiceName (t);
               foreach (var fi in t.GetFields (bfi).Where (AuField.Include)) {
                  string fname = fi.Name;
                  if (fname.StartsWith ('m')) fname = fname[1..];
                  if (Tactics.TryGetValue ($"{tname}.{fname}", out var data)) {
                     if (data.Tactic != EAuCurl.Skip)
                        fields.Add (new AuField (fi, data.Tactic, data.Sort));
                  } else
                     Except.Incomplete ($"Tactic missing for {tname}.{fname}");
               }
            }
            mFields = [.. fields.OrderBy (a => a.Sort)];
            break;
         case EAuTypeKind.AuPrimitive:
            mMIWriter = type.GetMethods (bfi).FirstOrDefault (a => a.Name == "Write"
               && a.GetParameters ().Length == 1
               && a.GetParameters ()[0].ParameterType == typeof (UTFWriter)) ??
               throw new Exception ($"Missing {type.FullName}.Write({nameof (UTFWriter)})");
            mMIReader = type.GetMethods (bfs).FirstOrDefault (a => a.Name == "Read"
               && a.GetParameters ().Length == 1
               && a.GetParameters ()[0].ParameterType == typeof (UTFReader)) ??
               throw new Exception ($"Missing {type.FullName}.Read({nameof (UTFReader)})");
            switch (Lib.NiceName (type)) {
               case "Color4": mSkipValue = Color4.Nil; break;
            }
            break;
         case EAuTypeKind.Primitive:
            switch (Lib.NiceName (type)) {
               case "double": mSkipValue = 0.0; break;
               case "float": mSkipValue = 0f; break;
               case "int": mSkipValue = 0; break;
               case "bool": mSkipValue = false; break;
            }
            break;
      }
   }
   MethodInfo? mMIWriter;
   MethodInfo? mMIReader;
   object? mSkipValue;

   public object CreateInstance () {
      if (mConstructor == null)
         mConstructor = mType.GetConstructor (Public | Instance | NonPublic, []) ??
            throw new Exception ($"No parameterless constructor found for {mType.FullName}");
      return mConstructor.Invoke ([]);
   }
   ConstructorInfo? mConstructor;

   public AuField? GetField (ReadOnlySpan<byte> name) {
      if (mFieldDict == null) {
         mFieldDict = new ();
         foreach (var field in mFields) mFieldDict.Add (field.Name, field);
      }
      return mFieldDict.GetValueOrDefault (name);
   }
   SymTable<AuField>? mFieldDict;

   public object? ByName (IReadOnlyList<object> stack, string name) {
      if (mMIByName == null) {
         const BindingFlags bfs = Static | Public | NonPublic | DeclaredOnly;
         mMIByName = mType.GetMethods (bfs).FirstOrDefault (
            a => a.Name == "ByName" &&
            a.GetParameters ().Length == 2 &&
            a.GetParameters ()[0].ParameterType == typeof (IReadOnlyList<object>) &&
            a.GetParameters ()[1].ParameterType == typeof (string)) ??
            throw new Exception ($"Missing {mType.FullName}.ByName(IReadOnlyList<object>,string)");
      }
      return mMIByName.Invoke (null, [stack, name]);
   }
   MethodInfo? mMIByName;

   public object? SkipValue => mSkipValue;

   static Dictionary<string, (EAuCurl Tactic, int Sort)> Tactics {
      get {
         if (mTactic == null) {
            mTactic = [];
            string tname = ""; int n = 0;
            foreach (var line in File.ReadAllLines ("A:/Wad/AuManifest.txt")) {
               if (line.IsBlank ()) continue;
               if (line.StartsWith (' ')) {
                  string[] words = line.Split (' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                  for (int i = 0; i < words.Length; i++) {
                     var (w, tactic) = (words[i], EAuCurl.Std);
                     if (w[0] == '-') (w, tactic) = (w[1..], EAuCurl.Skip);
                     else if (w[0] == '^') (w, tactic) = (w[1..], EAuCurl.Uplink);
                     else if (w.EndsWith (".Name")) (w, tactic) = (w[..^5], EAuCurl.ByName);
                     mTactic.Add ($"{tname}.{w}", (tactic, ++n));
                  }
               } else
                  tname = line.Trim ();
            }
         }
         return mTactic;
      }
   }
   static Dictionary<string, (EAuCurl Tactic, int Sort)>? mTactic;

   public override string ToString () => $"AuType {mType.FullName}";

   // Writes out a type override like "(E2Poly)"
   public void WriteOverride (UTFWriter buffer) => buffer.Write ('(').Write (BName).Write (')');
   byte[] BName => mBName ??= Encoding.UTF8.GetBytes (Lib.NiceName (Type));
   byte[]? mBName;

   public void WriteAuPrimitive (UTFWriter stm, object value)
      => mMIWriter!.Invoke (value, [stm]);

   public object? ReadAuPrimitive (UTFReader stm)
      => mMIReader!.Invoke (null, [stm]);

   public object? ReadEnum (UTFReader stm) {
      if (mEnumMap == null) {
         mEnumMap = new ();
         var names = Enum.GetNames (mType);
         var values = Enum.GetValues (mType);
         for (int i = 0; i < names.Length; i++) mEnumMap.Add (names[i], values.GetValue (i)!);
      }
      return mEnumMap[stm.TakeUntil (AuReader.NameStop, true)];
   }
   SymTable<object>? mEnumMap;

   public void WritePrimitive (UTFWriter stm, object value) {
      switch (Type.GetTypeCode (mType)) {
         case TypeCode.Boolean: stm.Write ((bool)value); break;
         case TypeCode.Int32: stm.Write ((int)value); break;
         case TypeCode.String: stm.Write ((string)value); break;
         case TypeCode.Double: stm.Write ((double)value); break;
         case TypeCode.Single: stm.Write ((float)value); break;
         default: throw new NotImplementedException ();
      }
   }

   public void WriteEnum (UTFWriter stm, object value) {
       stm.Write (value.ToString ()!);  // REMOVETHIS
   }

   static EAuTypeKind Classify (Type type) {
      if (type == typeof (string) || type.IsPrimitive) return EAuTypeKind.Primitive;
      if (type.IsEnum) return EAuTypeKind.Enum;
      if (type.HasAttribute<AuPrimitiveAttribute> ()) return EAuTypeKind.AuPrimitive;
      if (type.IsAssignableTo (typeof (IList))) return EAuTypeKind.List;
      if (type.IsAssignableTo (typeof (IDictionary))) return EAuTypeKind.Dictionary;
      if (type.IsClass) return EAuTypeKind.Class;
      return EAuTypeKind.Struct;
   }

   public readonly EAuTypeKind Kind;

   public static IEnumerable<AuType> All => mDict.Values;

   public ReadOnlySpan<AuField> Fields => mFields.AsSpan ();
   readonly ImmutableArray<AuField> mFields = [];

   public Type Type => mType;
   readonly Type mType;

   public static AuType Get (Type type) => mDict.GetValueOrDefault (type) ?? new AuType (type);
   static Dictionary<Type, AuType> mDict = [];

   public static AuType Find (ReadOnlySpan<byte> name) {
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
      throw new Exception ($"Type {sname} not found");
   }
   static SymTable<AuType> mByName = new ();
}

class AuField {
   internal AuField (FieldInfo fi, EAuCurl tactic, int sort) {
      Name = (mFI = fi).Name;
      Tactic = tactic; Sort = sort;
      if (Name.StartsWith ('m')) Name = Name[1..];
      var ftype = mFI.FieldType;
      mType = AuType.Get (ftype);
      const BindingFlags bf = Instance | Public | NonPublic;
      if (tactic == EAuCurl.ByName)
         mFIName = ftype.GetField ("Name", bf) ?? ftype.GetField ("mName", bf);
   }
   public override string ToString ()
      => $"{Lib.NiceName (mFI.FieldType)} {Name}";

   public bool Skip ([NotNullWhen (false)] object? value) {
      if (value == null || Tactic == EAuCurl.Uplink) return true;
      if (Equals (value, mType.SkipValue)) return true;
      return false;
   }

   internal static bool Include (FieldInfo fi) {
      if (fi.Name.StartsWith ('_')) return false;
      if (fi.FieldType.Name == "Subject`1") return false;
      return true;
   }

   /// <summary>Writes out the field name followed by a colon, like "Center:"</summary>
   public void WriteLabel (UTFWriter buf) => buf.Write (BName).Write (':');
   byte[] BName => mBName ??= Encoding.UTF8.GetBytes (Name);
   byte[]? mBName;

   public void WriteByName (UTFWriter buf, object obj)
      => buf.Write (mFIName!.GetValue (obj)?.ToString () ?? "");
   FieldInfo? mFIName;

   public Type Type => mType.Type;

   public readonly EAuCurl Tactic;
   public readonly int Sort;

   public AuType AuType => mType;
   readonly AuType mType;

   public object? GetValue (object parent) => mFI.GetValue (parent);
   readonly FieldInfo mFI;

   public void SetValue (object parent, object? value) => mFI.SetValue (parent, value);

   public readonly string Name;
}
