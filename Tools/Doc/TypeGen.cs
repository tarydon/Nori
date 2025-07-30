namespace Nori.Doc;
using static System.Reflection.BindingFlags;

// TypeGen is used to generate a documentation page for a particular type
class TypeGen : HTMLGen {
   public TypeGen (Type t, Project project) {
      mT = t;
      mDict = (mProject = project).Notes;
      var docPrivate = project.DocPrivate;
      string nicename = t.NiceName ();

      // Output the level 1 heading, and the class name and description
      HEAD ($"{project.Name}: {nicename}");
      H1 ($"{t.ClassPrefix ()} {nicename.HTML ()}");
      OutBlock ($"T:{t.GetKey ()}");

      // Document the constructors
      var bf = Instance | Public | NonPublic;
      var cons = t.GetConstructors (bf).Where (a => docPrivate || a.IsPublic).ToList ();
      if (cons.Count > 0) {
         H2 ("Constructors");
         cons.ForEach (OutConstructor);
      }

      // Document the properties and fields
      bf = Instance | Static | Public | NonPublic;
      List<MemberInfo> mi = [];
      mi.AddRange (t.GetProperties (bf).Where (a => docPrivate || a.AnyPublic ()));
      mi.AddRange (t.GetFields (bf).Where (a => docPrivate || a.IsPublic));
      mi.Sort ((a, b) => a.Name.CompareTo (b.Name));
      if (mi.Count > 0) {
         H2 ("Properties");
         mi.ForEach (OutProperty);
      }

      // Finish up
      Out ("</body>\n</html>");
   }
   readonly Project mProject;
   readonly IReadOnlyDictionary<string, string> mDict;
   readonly Type mT;

   void OutConstructor (ConstructorInfo cons) {
      Out ($"<p class=\"declaration\">");
      Out ($"<span class=\"moniker\">");
      OutType (cons.DeclaringType!);
      Out ("</span>");
      OutParams (cons.GetParameters ());
      Out ("</p>\n");
      OutBlock (cons.GetKey ());
      Out ("<br/><hr/>");
   }

   void OutProperty (MemberInfo mi) {
      Out ($"<p class=\"declaration\">");
      Type type; bool get = false, set = false;
      string key;
      switch (mi) {
         case PropertyInfo pi:
            type = pi.PropertyType;
            if (pi.GetGetMethod () is MethodInfo mig)
               if (mig.IsPublic || mProject.DocPrivate) get = true;
            if (pi.GetSetMethod () is MethodInfo mis)
               if (mis.IsPublic || mProject.DocPrivate) set = true;
            key = pi.GetKey ();
            break;
         case FieldInfo fi:
            type = fi.FieldType;
            get = true; set = !fi.IsInitOnly;
            key = fi.GetKey ();
            break;
         default:
            throw new NotImplementedException ();
      }

      OutType (type); Out (" ");
      Out ($"<span class=\"moniker\">{mi.Name}</span>");
      Out (" {");
      if (get) Out (" get;");
      if (set) Out (" set;");
      Out (" }</p\n>");
      OutBlock (key);
      Out ("<br/><hr/>");
   }

   void OutType (Type t)
      => Out ($"<span class=\"type\">{t.NiceName ().HTML ()}</span>");

   void OutName (string name)
      => Out ($"<span class=\"name\">{name}</span>");

   void OutParams (ParameterInfo[] pars) {
      Out (" (");
      for (int i = 0; i < pars.Length; i++) {
         var par = pars[i];
         if (i > 0) Out (", ");
         OutType (par.ParameterType); Out (" ");
         OutName (par.Name!);
      }
      Out (")\n");
   }

   void OutBlock (string target) {
      if (!mDict.TryGetValue (target, out var s)) {
//         Console.WriteLine ($"No documentation for {target}");
         return;
      }

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
      if (lines.Count == 0) return;
      while (lines[^1].IsBlank ()) lines.RemoveAt (lines.Count - 1);

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
      for (int i = 0; i < lines.Count; i++) {
         int level = 0;
         string line = lines[i];
         if (!line.IsBlank ())
            level = (line.TakeWhile (a => a == ' ').Count () / 4) + 1;
         PopStack (level);    // Pop off nesting until we reach target level
         if (level == 0) continue;

         var sline = line.TrimStart ();
         switch (level - stack.Count) {
            case 0:        // Continuing at the same level
               var t1 = stack.Peek ();
               switch (t1) {
                  case E.ol or E.ul:
                     if ("-+*".Contains (sline[0])) {
                        mS.Append ("</li><li>");
                        line = sline[1..].TrimStart ();
                     }
                     break;
                  case E.p: line = sline; break;
               }
               break;
            case 1:        // Nesting one level deeper
               E t2 = sline[0] switch {
                  '-' or '*' => E.ul,
                  '+' => E.ol,
                  _ => level == 1 ? E.p : E.pre
               };
               mS.Append ($"<{t2}>");
               if (t2 is E.ul or E.ol) {
                  mS.Append ("<li>");
                  line = sline[1..].TrimStart ();
               }
               stack.Push (t2);
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

   void Warn (string s)
      => Console.WriteLine (s);

   void Out (string s) => mS.Append (s);

   enum E { p, ul, ol, pre };
}
