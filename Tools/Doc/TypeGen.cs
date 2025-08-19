// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TypeGen.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
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
      var bf = Instance | Public | DeclaredOnly | (docPrivate ? NonPublic : 0);
      var cons = t.GetConstructors (bf).Where (Included).ToList ();
      if (cons.Count > 0) {
         H2 ("Constructors");
         for (int i = 0; i < cons.Count; i++)
            OutConstructor (cons[i], i == cons.Count - 1);
      }

      // Document the properties and fields
      List<MemberInfo> mi = [];
      bf = Instance | Static | Public | DeclaredOnly | (docPrivate ? NonPublic : 0);
      mi.AddRange (t.GetProperties (bf).Where (Included));
      mi.AddRange (t.GetFields (bf));
      mi.Sort ((a, b) => a.Name.CompareTo (b.Name));
      if (mi.Count > 0) {
         H2 ("Properties");
         for (int i = 0; i < mi.Count; i++)
            OutProperty (mi[i], i == mi.Count - 1);
      }

      // Document the methods
      bf = Instance | Static | Public | DeclaredOnly |  (docPrivate ? NonPublic : 0);
      var methods = t.GetMethods (bf).Where (Included).OrderBy (a => a.Name).ToList ();
      if (methods.Count > 0) {
         H2 ("Methods");
         for (int i = 0; i < methods.Count; i++) 
            OutMethod (methods[i], i == methods.Count - 1);
      }

      // Document the operators
      bf = Static | Public | DeclaredOnly;
      var operators = t.GetMethods (bf).Where (a => a.IsSpecialName && a.Name.StartsWith ("op_")).ToList ();
      if (operators.Count > 0) {
         H2 ("Operators");
         for (int i = 0; i < operators.Count; i++)
            OutOperator (operators[i], i == operators.Count - 1);
      }

      // Finish up
      Out ("</body>\n</html>\n");
   }
   readonly Project mProject;
   readonly IReadOnlyDictionary<string, string> mDict;
   readonly Type mT;

   // Is this constructor to be included? 
   // We avoid adding notes for undocumented, paramterless constructors
   bool Included (ConstructorInfo ci) {
      if (ci.GetParameters ().Length > 0) return true;
      if (mDict.ContainsKey (ci.GetKey ())) return true;
      return false;
   }

   bool Included (MethodInfo mi) {
      if (mi.IsSpecialName) return false;
      string key = mi.GetKey ();
      if (mDict.ContainsKey (key)) return true;
      if (mi.Name == "ToString" && mi.GetParameters ().Length == 0) return false;
      if (mProject.Exclude.Any (a => a.Match (key).Success)) return false;
      return true;
   }

   bool Included (PropertyInfo pi) {
      var key = pi.GetKey ();
      if (mDict.ContainsKey (key)) return true;
      if (mProject.Exclude.Any (a => a.Match (key).Success)) return false;
      return true; 
   }

   void OutConstructor (ConstructorInfo cons, bool last) {
      Out ($"<p class=\"declaration\">");
      Out ($"<span class=\"moniker\">");
      OutType (cons.DeclaringType!);
      Out ("</span>");
      OutParams (cons.GetParameters ());
      Out ("</p>\n");
      OutBlock (cons.GetKey ());
      if (!last) Out ("<hr/>");
   }

   void OutMethod (MethodInfo mi, bool last) {
      Out ($"<p class=\"declaration\">{mi.MemberPrefix ()}");
      OutType (mi.ReturnType);
      Out ($" <span class=\"moniker\">{mi.Name}</span>");
      OutParams (mi.GetParameters ());
      Out ("</p>\n");
      OutBlock (mi.GetKey ());
      if (!last) Out ("<hr/>");
      Out ("\n\n");
   }

   void OutOperator (MethodInfo mi, bool last) {
      Out ($"<p class=\"declaration\">");
      if (mi.Name is "op_Implicit" or "op_Explicit") {
         Out ($"{mi.Name[3..].ToLower ()} operator <span class=\"moniker\">");
         OutType (mi.ReturnType);
         Out ("</span>");
      } else {
         OutType (mi.ReturnType);
         string name = sOperators.GetValueOrDefault (mi.Name, mi.Name);
         Out ($" <span class=\"moniker\">operator {name.HTML ()}</span>");
      }
      OutParams (mi.GetParameters ());
      Out ("</p>\n");
      OutBlock (mi.GetKey ());
      if (!last) Out ("<hr/>");
      Out ("\n\n");
   }
   static Dictionary<string, string> sOperators = new Dictionary<string, string> () {
      ["op_UnaryPlus"] = "+", ["op_UnaryNegation"] = "-", ["op_LogicalNot"] = "!",
      ["op_OnesComplement"] = "~", ["op_Increment"] = "++", ["op_Decrement"] = "--",
      ["op_True"] = "true", ["op_False"] = "false", ["op_Addition"] = "+",
      ["op_Subtraction"] = "-", ["op_Multiply"] = "*", ["op_Division"] = "/",
      ["op_Modulus"] = "%", ["op_BitwiseAnd"] = "&", ["op_BitwiseOr"] = "|",
      ["op_ExclusiveOr"] = "^", ["op_LeftShift"] = "<<", ["op_RightShift"] = ">>",
      ["op_Equality"] = "==", ["op_Inequality"] = "!=", ["op_LessThan"] = "<",
      ["op_LessThanOrEqual"] = "<=", ["op_GreaterThan"] = ">", ["op_GreaterThanOrEqual"] = ">="
   };

   void OutProperty (MemberInfo mi, bool last) {
      Out ($"<p class=\"declaration\">");
      Type type; bool get = false, set = false;
      string key;
      ParameterInfo[]? pars = null;
      switch (mi) {
         case PropertyInfo pi:
            type = pi.PropertyType;
            if (pi.GetGetMethod () is MethodInfo mig) {
               pars = mig.GetParameters ();
               if (pars.Length == 0) pars = null;
               if (mig.IsPublic || mProject.DocPrivate) get = true;
            }
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
      Out ($"<span class=\"moniker\">");
      if (pars != null) { Out ("this</span>"); OutParams (pars, true); } 
      else Out ($"{mi.Name}</span>"); 
      Out (" {");
      if (get) Out (" get;");
      if (set) Out (" set;");
      Out (" }</p>\n");
      OutBlock (key);
      if (!last) Out ("<hr/>");
      Out ("\n\n");
   }

   void OutType (Type t)
      => Out (t.NiceName ().HTML ());

   void OutName (string name)
      => Out (name);

   void OutParams (ParameterInfo[] pars, bool square = false) {
      Out (square ? " [" : " (");
      for (int i = 0; i < pars.Length; i++) {
         var par = pars[i];
         if (i > 0) Out (", ");
         OutType (par.ParameterType); Out (" ");
         OutName (par.Name!);
      }
      Out (square ? "]\n" : ")\n");
   }

   void OutBlock (string target) {
      if (!mDict.TryGetValue (target, out var s)) {
         P (target, "class", "missing");
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
