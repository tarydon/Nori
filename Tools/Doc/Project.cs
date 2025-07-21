namespace Nori.Doc;

class Project {
   public Project (string file) {
      foreach (var line in File.ReadAllLines (file).Select (a => a.Trim ())) {
         if (line.StartsWith ('#')) continue;
         string[] w = line.Split (['='], 2, StringSplitOptions.TrimEntries);
         if (w.Length != 2) continue;
         string key = w[0].ToUpper ();
         switch (key) {
            case "DOCUMENTPRIVATE": DocumentPrivate = GetBool (w[1]); break;
            case "INPUT": mInput.Add (w[1]); break;
            case "OUTPUTDIRECTORY": OutDir = w[1]; break;
            case "PROJECT": Name = w[1]; break;
            case "NAMESPACE": mNamespaces.Add (w[1]); break;
            default: Console.WriteLine ($"Unknown key {key} in {file}"); break;
         }
      }
      for (int i = 0; i < mNamespaces.Count; i++) {
         string s = mNamespaces[i]; if (!s.EndsWith ('.')) s += ".";
         mNamespaces[i] = s;
      }
      mNamespaces = mNamespaces.OrderByDescending (a => a.Length).ToList ();
      if (OutDir == "") Fatal ($"OUTPUTDIRECTORY setting missing in {file}");
   }

   public void Process () {
      // First, load all the input files (XML and DLL)
      foreach (var file in mInput) {
         var ext = Path.GetExtension (file).ToLower ();
         switch (ext) {
            case ".xml": LoadXML (file); break;
            case ".dll": LoadDLL (file); break;
            default: Fatal ($"Unknown file type {file}"); break;
         }
      }

      // Create the output directory, and copy the resource files there
      try { Directory.CreateDirectory (OutDir); } catch { Fatal ($"Could not create directory {OutDir}"); }
      CopyResources ();

      // Output one page for each type
      foreach (var t in mTypes)
         new TypeGen (t, Name).Generate (OutDir);
   }

   public readonly string Name = "Untitled";

   public readonly string OutDir = "";

   public readonly bool DocumentPrivate = false;

   public IReadOnlyList<string> Input => mInput;
   List<string> mInput = [];

   public static IReadOnlyList<string> Namespaces => mNamespaces;
   static List<string> mNamespaces = ["System", "System.Collections.Generic"];

   // Private data -------------------------------------------------------------
   void CopyResources () {
      Stream stm = Assembly.GetExecutingAssembly ().GetManifestResourceStream ("Nori.Doc.Res.std.css")!;
      byte[] data = new byte[(int)stm.Length];
      stm.ReadExactly (data);
      File.WriteAllBytes ($"{OutDir}/std.css", data);
   }

   // Prints error and stops
   [DoesNotReturn]
   void Fatal (string s) {
      Console.WriteLine (s);
      Environment.Exit (-1);
   }

   bool GetBool (string s)
      => s.ToUpper () is "YES" or "1" or "TRUE";

   // Loads an XML file, and parses each 'member' element
   void LoadXML (string file) {
      if (!File.Exists (file)) Fatal ($"File {file} missing");
      Console.WriteLine (file);
      foreach (var member in XDocument.Load (file).Descendants ("member")) {
         string key = member.Attribute ("name")?.Value ?? "";
         var reader = member.CreateReader ();
         reader.MoveToContent ();
         // Make sure each 'summary' is terminated by a period or a question mark
         var value = reader.ReadInnerXml ().Replace ("</summary>", ".</summary>")
                                           .Replace ("..</summary>", ".</summary>")
                                           .Replace ("?.</summary>", "?</summary>");
         mNotes.Add (key, value);
      }
   }
   Dictionary<string, string> mNotes = [];

   // Loads a DLL file, and fetches all the types from that
   void LoadDLL (string file) {
      var assy = Assembly.LoadFrom (file);
      int n = 0;
      foreach (var type in assy.GetTypes ()) {
         if (TypeInfo.Skip (type, DocumentPrivate)) continue;
         Console.WriteLine ($"{++n}. {type.FullName}");
         mTypes.Add (type);
      }
   }
   List<Type> mTypes = [];
}
