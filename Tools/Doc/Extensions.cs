// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Extensions.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Doc;

static class Extensions {
   public static bool AnyPublic (this PropertyInfo pi)
      => pi.GetGetMethod ()?.IsPublic == true || pi.GetSetMethod ()?.IsPublic == true;

   public static string ClassPrefix (this Type t) {
      if (t.IsInterface) return "interface";
      if (t.IsValueType) return "struct";
      return "class";
   }

   public static string MemberPrefix (this MethodInfo m) {
      if (m.IsStatic) return "static ";
      return "";
   }

   public static string HTML (this string s)
      => s.Replace ("<", "&lt;").Replace (">", "&gt;");

   public static bool IsBlank (this string? s)
      => string.IsNullOrWhiteSpace (s);

   public static string NiceName (this Type t) {
      if (t.IsGenericParameter) return t.Name;
      var sb = new StringBuilder ();
      if (t.DeclaringType != null) sb.Append ($"{NiceName (t.DeclaringType)}.");
      else if (t.Namespace != null && !Project.Namespaces.Contains (t.Namespace)) sb.Append ($"{t.Namespace}.");
      if (t.IsGenericType) {
         sb.Append ($"{t.Name.Split ('`')[0]}<");
         sb.Append (string.Join (',', t.GetGenericArguments ().Select (a => a.Name)));
         sb.Append ('>');
      } else
         sb.Append (t.Name);
      var s = sb.ToString ();
      return sNiceNames.GetValueOrDefault (s, s);
   }
   static Dictionary<string, string> sNiceNames = new() {
      ["Double"] = "double", ["Single"] = "float", ["Int32"] = "int", ["UInt32"] = "uint",
      ["Int16"] = "short", ["UInt16"] = "ushort", ["Int64"] = "long", ["UInt64"] = "ulong",
      ["Byte"] = "byte", ["SByte"] = "sbyte", ["Boolean"] = "bool", ["Void"] = "void",
      ["String"] = "string", ["Object"] = "object"
   };

   /// <summary>Returns the key for this type (used to index into the XML documentation)</summary>
   public static string GetKey (this Type t) {
      if (t.IsArray) {
         // If this is an array, but the element type is a generic parameter (for example, 
         // like the first parameter to AList<T>.CopyTo(T[] array, ...) then we should return
         // a typename like "`0[]"
         var et = t.GetElementType ()!;
         if (et.IsGenericParameter) {
            var s = $"`{et.GenericParameterPosition}[";
            for (int i = 1; i < t.GetArrayRank (); i++) s += ',';
            return s + ']';
         }
      }
      if (t.IsGenericParameter) {
         // If this is a generic parameter type (like the parameter to AList<T>.Add(T), then
         // we should returna string like `0 (where 0 is the generic parameter position)
         return $"`{t.GenericParameterPosition}";
      }
      if (t.IsConstructedGenericType) {
         // If this is a fully constructed generic type (like AList<int>) then we should
         // return a string like Nori.AList{System.Int32} - this substitution is done since
         // we cannot use angle brackets as the key (that would mess up the XML documentation
         // file)
         var sb = new StringBuilder (t.GetGenericTypeDefinition ().FullName ?? "");
         sb.Remove (sb.Length - 2, 2); sb.Append ('{');
         for (int i = 0; i < t.GetGenericArguments ().Length; i++) {
            if (i > 0) sb.Append (',');
            sb.Append (GetKey (t.GetGenericArguments ()[i]));
         }
         sb.Append ('}');
         return sb.ToString ();
      }
      return (t.FullName ?? t.Name).Replace ('+', '.');
   }

   public static string GetKey (this ConstructorInfo c) {
      var sb = new StringBuilder ("M:");
      sb.Append (c.DeclaringType!.GetKey ());
      sb.Append (".#ctor");
      AppendParams (sb, c.GetParameters ());
      return sb.ToString ();
   }

   public static string GetKey (this MethodInfo m) {
      var sb = new StringBuilder ("M:");
      sb.Append (m.DeclaringType!.GetKey ());
      sb.Append ('.'); sb.Append (m.Name);
      AppendParams (sb, m.GetParameters ());
      return sb.ToString ();
   }

   static void AppendParams (StringBuilder sb, ParameterInfo[] pars) {
      if (pars.Length != 0) {
         sb.Append ('(');
         sb.Append (string.Join (',', pars.Select (a => a.ParameterType.GetKey ())));
         sb.Append (')');
      }
   }

   public static string GetKey (this PropertyInfo p) {
      var sb = new StringBuilder ("P:");
      sb.Append (p.DeclaringType!.GetKey ());
      sb.Append ('.'); sb.Append (p.Name);
      if (p.GetGetMethod () is MethodInfo mi)
         AppendParams (sb, mi.GetParameters ());
      return sb.ToString ();
   }

   public static string GetKey (this FieldInfo f)
      => $"F:{f.DeclaringType!.GetKey ()}.{f.Name}";
}
