namespace Nori.Doc;

static class Extensions {
   public static bool AnyPublic (this PropertyInfo pi)
      => pi.GetGetMethod ()?.IsPublic == true || pi.GetSetMethod ()?.IsPublic == true;

   public static string ClassPrefix (this Type t) {
      if (t.IsInterface) return "interface";
      if (t.IsValueType) return "struct";
      return "class";
   }

   public static string HTML (this string s)
      => s.Replace ("<", "&lt;").Replace (">", "&gt;");

   public static bool IsBlank (this string? s)
      => string.IsNullOrWhiteSpace (s);

   public static string NiceName (this Type t) {
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
      ["Byte"] = "byte", ["SByte"] = "sbyte", ["Boolean"] = "bool"
   };

   /// <summary>
   /// Returns the key for this type (used to index into the XML documentation)
   /// </summary>
   public static string GetKey (this Type t)
      => (t.FullName ?? t.Name).Replace ('+', '.');

   public static string GetKey (this ConstructorInfo c) {
      var sb = new StringBuilder ("M:");
      sb.Append (c.DeclaringType!.GetKey ());
      sb.Append (".#ctor");
      if (c.GetParameters ().Length != 0) {
         sb.Append ('(');
         sb.Append (string.Join (',', c.GetParameters ().Select (a => a.ParameterType.GetKey ())));
         sb.Append (')');
      }
      return sb.ToString ();
   }

   public static string GetKey (this PropertyInfo p)
      => $"P:{p.DeclaringType!.GetKey ()}.{p.Name}";

   public static string GetKey (this FieldInfo f)
      => $"F:{f.DeclaringType!.GetKey ()}.{f.Name}";
}
