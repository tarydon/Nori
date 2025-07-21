namespace Nori.Doc;

// TypeGen is used to generate a documentation page for a particular type
class TypeGen : HTMLGen {
   public TypeGen (Type t, string project) {
      mT = t;
      Console.WriteLine ($"Documenting {t.FullName}");

      HEAD ($"{project}: {t.NiceName ()}");
      H1 ($"{t.ClassPrefix ()} {t.NiceName ()}");
      mS.AppendLine ("</body>\n</html>");
   }
   readonly Type mT;

   public void Generate (string outDir) {
      File.WriteAllText ($"{outDir}/type.{mT.FullName}.html", mS.ToString ().Replace ("\r\n", "\n"));
   }
}
