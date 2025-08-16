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
      if (t.IsGenericType) {
         var sb = new StringBuilder (t.GetGenericTypeDefinition ().FullName ?? "ABC");
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

   public static string GetKey (this PropertyInfo p)
      => $"P:{p.DeclaringType!.GetKey ()}.{p.Name}";

   public static string GetKey (this FieldInfo f)
      => $"F:{f.DeclaringType!.GetKey ()}.{f.Name}";
}
