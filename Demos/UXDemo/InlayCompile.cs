using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
namespace Nori.UX;

public class InlayCompiler {
   public InlayCompiler (string text) => mText = text;
   readonly string mText;

   public Action? Compile () {
      using var bt = new BlockTimer ("Compile");
      var tree = CSharpSyntaxTree.ParseText (mText);

      var refs = ((string)AppContext.GetData ("TRUSTED_PLATFORM_ASSEMBLIES")!)
          .Split (Path.PathSeparator)
          .Select (p => MetadataReference.CreateFromFile (p)).ToList ();
      refs.Add (MetadataReference.CreateFromFile (typeof (Nori.Inlay.InlayHub).Assembly.Location));
      refs.Add (MetadataReference.CreateFromFile (typeof (Nori.UX.UXFrame).Assembly.Location));

      var compilation = CSharpCompilation.Create ("GeneratedAssembly", [tree], refs, 
         new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary));
      using var ms = new MemoryStream ();
      
      EmitResult result = compilation.Emit (ms);
      if (!result.Success) {
         mDiags.AddRange (result.Diagnostics);
         return null;
      }

      ms.Position = 0;
      var ctx = new AssemblyLoadContext (null, true);
      Assembly asm = ctx.LoadFromStream (ms);
      Type type = asm.GetTypes ()[0];
      MethodInfo mi = type.GetMethod ("Generate", BindingFlags.Static | BindingFlags.Public)!;
      return () => mi.Invoke (null, null);
   }

   public IEnumerable<string> Diagnostics => mDiags.Select (a => a.ToString ());
   List<Diagnostic> mDiags = [];
}
