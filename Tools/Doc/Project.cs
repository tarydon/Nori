// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Project.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Text.RegularExpressions;

namespace Nori.Doc;

class Project {
   // Constructor --------------------------------------------------------------
   public Project (string file) {
      string lastKey = "";
      foreach (var line in File.ReadAllLines (file).Select (a => a.Trim ())) {
         if (line.StartsWith ('#')) continue;
         string[] w = line.Split (['='], 2, StringSplitOptions.TrimEntries);
         if (w.Length != 2) continue;
         string key = w[0].ToUpper ();
         if (key == "") key = lastKey; else lastKey = key;
         switch (key) {
            case "DOCUMENTPRIVATE": mDocPrivate = GetBool (w[1]); break;
            case "INPUT": mInput.Add (w[1]); break;
            case "OUTPUTDIRECTORY": mOutDir = w[1]; break;
            case "PROJECT": mName = w[1]; break;
            case "NAMESPACE": mNamespaces.Add (w[1]); break;
            case "EXCLUDE": mExclude.Add (new Regex (w[1], RegexOptions.Compiled)); break;
            default: Console.WriteLine ($"Unknown key {key} in {file}"); break;
         }
      }
      mNamespaces = [.. mNamespaces.OrderByDescending (a => a.Length)];
      if (mOutDir == "") Program.Fatal ($"OUTPUTDIRECTORY setting missing in {file}");
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Project title</summary>
   public string Name => mName;
   readonly string mName = "Untitled";    // Project title

   /// <summary>List of 'known' namespaces</summary>
   public static IReadOnlyList<string> Namespaces => mNamespaces;
   static List<string> mNamespaces = ["System", "System.Collections.Generic"];

   /// <summary>The documentation blocks for each type, method, property etc</summary>
   public IReadOnlyDictionary<string, string> Notes => mNotes;
   Dictionary<string, string> mNotes = [];

   /// <summary>Exclude documentation for elements whose keys match these</summary>
   public IReadOnlyList<Regex> Exclude => mExclude;
   static List<Regex> mExclude = [];

   // Methods ------------------------------------------------------------------
   public void Process () {
      // First, load all the input files (XML and DLL)
      foreach (var file in mInput) {
         var ext = Path.GetExtension (file).ToLower ();
         switch (ext) {
            case ".xml": LoadXML (file); break;
            case ".dll": LoadDLL (file); break;
            default: Program.Fatal ($"Unknown file type {file}"); break;
         }
      }

      // Create the output directory, and copy the resource files there
      try { Directory.CreateDirectory (mOutDir); }
      catch { Program.Fatal ($"Could not create directory {mOutDir}"); }
      CopyResources ();

      // Output one page for each type
      foreach (var t in mTypes)
         new TypeGen (t, this).Generate (mOutDir);
   }

   // Implementation -----------------------------------------------------------
   void CopyResources () {
      Stream stm = Assembly.GetExecutingAssembly ().GetManifestResourceStream ("Nori.Doc.Res.doc.css")!;
      byte[] data = new byte[(int)stm.Length];
      stm.ReadExactly (data);
      // File.WriteAllBytes ($"{mOutDir}/doc.css", data);
   }

   bool GetBool (string s)
      => s.ToUpper () is "YES" or "1" or "TRUE";

   // Loads an XML file, and parses each 'member' element
   void LoadXML (string file) {
      if (!File.Exists (file)) Program.Fatal ($"File {file} missing");
      Console.WriteLine ($"Parsing {file}");
      foreach (var member in XDocument.Load (file).Descendants ("member")) {
         string key = member.Attribute ("name")?.Value ?? "";
         var reader = member.CreateReader ();
         reader.MoveToContent ();
         mNotes.Add (key, reader.ReadInnerXml ());
      }
   }

   // Loads a DLL file, and fetches all the types from that
   void LoadDLL (string file) {
      Console.WriteLine ($"Parsing {file}");
      var assy = Assembly.LoadFrom (file);
      foreach (var type in assy.GetTypes ()) {
         if (TypeInfo.Skip (type, mDocPrivate)) continue;
         mTypes.Add (type);
      }
   }
   List<Type> mTypes = [];

   // Private data -------------------------------------------------------------
   public bool DocPrivate => mDocPrivate;
   readonly bool mDocPrivate;        // Should private methods be documented

   readonly string mOutDir = "";          // Output folder
   readonly List<string> mInput = [];     // Set of input files (XML, DLL)
}
