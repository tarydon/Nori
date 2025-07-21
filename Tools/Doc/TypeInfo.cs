namespace Nori.Doc;

class TypeInfo {
   // Should this type be skipped during documentation generation?
   public static bool Skip (Type type, bool includePrivate) {
      if (!includePrivate && !type.IsPublic) return true;
      if (type.BaseType?.Name == "System.MultiCastDelegate") return true;
      string name = type.FullName ?? "";
      if (name.Contains ('<') && name.Contains ('>')) return true;
      if (type.GetCustomAttribute<ObsoleteAttribute> () != null) return true; 
      return false;
   }
}
