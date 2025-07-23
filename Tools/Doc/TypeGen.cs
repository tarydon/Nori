using System.Xml;
namespace Nori.Doc;

// TypeGen is used to generate a documentation page for a particular type
class TypeGen : HTMLGen {
   public TypeGen (Type t, Project project) {
      mT = t;
      var dict = project.Notes;
      string nicename = t.NiceName ();
      if (nicename != "Nori.RBTree<TVal,TKey>") return;

      // Output the level 1 heading, and the class name and description
      HEAD ($"{project.Name}: {nicename}");
      H1 ($"{t.ClassPrefix ()} {nicename.HTML ()}");
      string key = t.GetKey ();
      if (dict.TryGetValue (key, out var text)) {
         OutBlock (text, key);
      } else
         Console.WriteLine ($"No documentation for type {t.FullName}");

      // Finish up
      mS.AppendLine ("</body>\n</html>");
   }
   readonly Type mT;

   void OutBlock (string s, string target) {
      // First, extract the summary block
      int n1 = s.IndexOf ("<summary>"), n2 = s.IndexOf ("</summary>", n1 + 8);
      string summary = s[(n1 + 9)..n2].Trim ();
      if (!summary.EndsWith ('.') && !summary.EndsWith ('?')) summary += ".";
      P (summary, "class", "summary");

      // Then, extract the actual content block (other than the summary), and clean it up
      // so that it has no blank lines at the top or bottom,
      s = s[(n2 + 10)..].TrimStart (' ').TrimStart ('\n');
      var lines = s.Split ('\n').ToList ();
      while (lines.Count > 0 && lines[0].IsBlank ()) lines.RemoveAt (0);
      while (lines[^1].IsBlank ()) lines.RemoveAt (lines.Count - 1);
      if (lines.Count == 0) return;

      // Further cleanup to ensure that the entire block is left-aligned at column 0
      int n = int.MaxValue;
      foreach (var line in lines) {
         if (line.IsBlank ()) continue;
         n = Math.Min (n, line.TakeWhile (a => a == ' ').Count ());
      }
      for (int i = 0; i < lines.Count; i++) {
         if (lines[i].IsBlank ()) lines[i] = "";
         else lines[i] = lines[i][n..];
      }

      // Now, we are ready to output the main documentation block. This stack tracks
      // all the 'open blocks' we have
      Stack<E> stack = [];
      foreach (var line in lines) {
         int level = 0;
         if (!line.IsBlank ())
            level = (line.TakeWhile (a => a == ' ').Count () / 4) + 1;
         PopStack (level);
         if (level == 0) continue;

         switch (level - stack.Count) {
            case 0:        // Continuing at the same level
               var type1 = stack.Peek ();
               switch (type1) {
                  case E.ol or E.ul: mS.Append ("</li><li>"); break;
                  default: mS.Append ($"</{type1}><{type1}>"); break;
               }
               break;       // Continuing at the same level
            case 1:
               E type = line.TrimStart ()[0] switch {
                  '-' or '*' => E.ul,
                  '+' => E.ol,
                  _ => level == 1 ? E.p : E.pre
               };
               mS.Append ($"<{type}>");
               if (type is E.ul or E.ol) mS.Append ("<li>");
               stack.Push (type);
               break;
            default:
               Program.Fatal ($"Invalid documentation nesting for {target}");
               break;
         }
         mS.AppendLine (line);
      }
      PopStack (0);

      void PopStack (int level) {
         if (stack.Count <= level) return;
         while (stack.Count > level) mS.Append ($"</{stack.Pop ()}>");
         mS.AppendLine ();
      }
   }

   public void Generate (string outDir) {
      File.WriteAllText ($"{outDir}/type.{mT.FullName}.html", mS.ToString ().Replace ("\r\n", "\n"));
   }

   enum E { p, ul, ol, pre };
}
