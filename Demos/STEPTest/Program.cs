using System.Diagnostics;
using System.Text;
using Nori;
namespace STEPTest;

class Program {
   static void Main (string[] args) {
      Lib.Init ();
      Lib.Tessellate = Tess2D.Process;
      var files = Directory.GetFiles (mSrcDir, "*.stp")
                           .Select (a => new FileInfo (a))
                           .OrderBy (a => a.Length)
                           .Select (a => Path.GetFileNameWithoutExtension (a.FullName))
                           .ToArray ();
      Console.Title = $"Testing {files.Length} files";
      for (int i = 0; i < files.Length; i++) {
         string name = files[i];
         Console.Write ($"{i}. {name}");
         Test (name);
         Console.WriteLine (); 
      }
   }

   static void Test (string name) {
      var file = $"{mSrcDir}\\{name}.stp";
      var model = new STEPReader (file).Load ();
      CheckObj (model, $"{mTarget}\\{name}.A.curl");

      var shmodel = new SheetMetalizer (model).Process ().Value;
      CheckObj (shmodel, $"{mTarget}\\{name}.B.curl");

      var dwg = new Unfolder (shmodel).Process ().Value;
      CheckObj (dwg, $"{mTarget}\\{name}.C.curl");
   }

   static void CheckObj (object obj, string curlfile) {
      var s1 = Encoding.UTF8.GetString (CurlWriter.SaveToByteArray (obj));
      if (!File.Exists (curlfile)) File.WriteAllText (curlfile, s1);         
      var s0 = File.ReadAllText (curlfile);
      if (s0 == s1) return;

      string target = "N:\\TDATA\\tmpfile.curl";
      File.WriteAllText (target, s1);
      Process.Start ("winmergeu.exe", $"{curlfile} {target}").WaitForExit ();
   }

   static void Move (string name, string dir) {
      Directory.CreateDirectory ($"W:\\STEP\\{dir}");
      File.Move ($"{mSrcDir}\\{name}.stp", $"W:\\STEP\\{dir}\\{name}.stp");
   }

   static string mSrcDir = "W:\\STEP\\PERFECT";
   static string mTarget = "W:\\STEP\\TEST";
}
