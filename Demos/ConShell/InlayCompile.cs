namespace UXDemo;

public class InlayCompiler {
   public InlayCompiler (string infile) => Text = File.ReadAllLines (infile);

   public void Compile (string outfile) {
      S.Add ("static class Inlay0 {");
      S.Add ("static void Generate () {");
      foreach (var line0 in Text) {
         var line = line0.Trim ().Replace ('\t', ' ') + " ";
         if (line.Length <= 1) continue;
         string[] words = GetWords (line).ToArray ();
         if (!ProcessElement (words))
            ProcessCode (words);
      }
      S.Add ("}"); S.Add ("}");
      int level = 0, indent = 1;
      for (int i = 0; i < S.Count; i++) {
         if (S[i] is "}" or "End ();") level--;
         var tmp = new string (' ', level * indent) + S[i];
         if (S[i].EndsWith ('{')) level++;
         else if (S[i].StartsWith ("Begin")) level++;
         S[i] = tmp;
      }
      File.WriteAllLines (outfile, S);
   }

   // Implementation -----------------------------------------------------------
   IEnumerable<string> GetWords (string line) {
      for (int i = 0; i < line.Length; i++) {
         if (char.IsWhiteSpace (line[i])) continue;
         int j;
         if (line[i] == '"') {
            j = line.IndexOf ('"', i + 1);
            yield return line[(i + 1)..j];
         } else if (line[i] == '$') {
            j = line.IndexOf ('"', i + 2);
            yield return line[i..(j + 1)];
         } else {
            j = line.IndexOf (' ', i + 1);
            yield return line[i..j];
         }
         i = j;
      }
   }

   bool ProcessElement (string[] a) {
      var elem = a[0];
      if (elem == "}") return false;
      if (elem.ToUpper () != elem) return false;
      if (elem == ">") { S.Add ("End ();"); return true; }
      if (a[^1] == "<") S.Add ($"Begin{elem} (");
      else S.Add ($"{elem} (");
      for (int i = 1; i < a.Length; i++) {
         if (a[i] == "<") continue; 
         if (i > 1) Append (", ");
         if (a[i][0] == '$') Append (a[i]);
         else Append ($"\"{a[i]}\"");
      }
      Append (");");
      return true;
   }

   void ProcessCode (string[] a) {
      S.Add (string.Join (' ', a));
   }

   void Append (string s) => S[^1] += s;

   // Private data -------------------------------------------------------------
   List<string> S = [];
   readonly string[] Text;
   int N;
}
