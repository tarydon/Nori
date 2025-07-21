namespace Nori.Doc;

static class Extensions {
   public static string ClassPrefix (this Type t) {
      return "class";
   }

   public static string NiceName (this Type t) {
      string s = t.FullName ?? "?";
      foreach (var sn in Project.Namespaces)
         if (s.StartsWith (sn)) s = s[sn.Length..];
      return s; 
   }
}
