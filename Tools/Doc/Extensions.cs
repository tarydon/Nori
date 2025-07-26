namespace Nori.Doc;

static class Extensions {
   public static string ClassPrefix (this Type t) {
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
      return sb.ToString ();
   }

   /// <summary>
   /// Returns the key for this type (used to index into the XML documentation)
   /// </summary>
   public static string GetKey (this Type t) {
      var sb = new StringBuilder ("T:");
      if (t.DeclaringType != null) sb.Append ($"{GetKey (t.DeclaringType)}.");
      else if (t.Namespace != null) sb.Append ($"{t.Namespace}.");
      sb.Append (t.Name);
      return sb.ToString ();
   }

   public static string GetKey (this ConstructorInfo c) {
      var sb = new StringBuilder ("M:");
      sb.Append (c.DeclaringType!.FullName!.Replace ('+', '.'));
      sb.Append (".#ctor(");
      c.GetParameters ().Select (a => a.ParameterType.FullName!.Replace ('+', '.'));
      sb.Append (")");
      return sb.ToString ();
   }
}
