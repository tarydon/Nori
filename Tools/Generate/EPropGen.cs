// ────── ╔╗
// ╔═╦╦═╦╦╬╣ EProp.cs
// ║║║║╬║╔╣║ Source generator to implement [EPropClass] and [EPropField] behavior
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Linq;
using System.Text;
namespace Nori.Gen;

#region class EPropGenerator -----------------------------------------------------------------------
/// <summary>EPropClass implements something similar to an INotifyPropertyChanged, for EProps</summary>
/// You can implement IObservable(EProp) by adding the [EPropClass] attribute on top of
/// a class. Then, each field tagged with the [EPropField] attrribute within that class 
/// becomes an 'active field' and the source generator will create a corresponding property
/// that wraps around that field. When the property is written to, it will raise the 
/// notification (through the enclosing class's IObservable interface). So you just write
/// something like this:
/// 
///   [EPropField (EProp.Xfm)] Vector2 mPos;
///   
/// and the source generator will auto-generate a property like this:
/// 
///   public Vector2 Pos {
///      get => mPos;
///      set { mPos = value; mSubject?.OnNext (EProp.Xfm); }
///   }
///   
/// This code is simplified - it uses Lib.Set to check if the property has actually been
/// changed before raising the notification. 
[Generator]
class EPropClassGenerator : IIncrementalGenerator {
   /// <summary>Implement the IIncrementalGenerator interface</summary>
   public void Initialize (IncrementalGeneratorInitializationContext context) {
      // We search for class decorated with the [EPropClass] attribute, using the
      // highly performant ForAttributeWithMetadataname method. For more on this, 
      // see the series on Source Generators in .Net by Pawel Gerr:
      // https://www.thinktecture.com/net/roslyn-source-generators-introduction/
      IncrementalValuesProvider<string?> className = context.SyntaxProvider
         .ForAttributeWithMetadataName ("Nori.EPropClassAttribute", Always, GetClassName);
      context.RegisterSourceOutput (className, GenerateSource);
   }

   // Helper that always returns true (no further filtering needed on the node)
   static bool Always (SyntaxNode _, CancellationToken __) => true;

   // This transformer extracts the fully qualified class name from the ClassDeclarationSyntax
   // node. If the attribute is (mistakenly) applied on any other C# construct, this returns null
   static string? GetClassName (GeneratorAttributeSyntaxContext context, CancellationToken _) {
      if (context.TargetNode is not ClassDeclarationSyntax cd) return null;
      if (context.SemanticModel.GetDeclaredSymbol (cd) is not INamespaceOrTypeSymbol sym) return null;

      // Here, we're going to go through all the fields of this type decorated with the
      // [EPropField] attribute. For example, we may have a field like this:
      //    [EPropField (EProp.Xfm)] Vector2 mPosition;
      // For this, we will write a corresponding "Position" property, which when written 
      // to will send a notification to observers with the EProp.Xfm tag. To make all this
      // possible, we will create here a multi-line string whose first line is the class name
      // decorated with [EPropClass] and whose subsequent lines each correspond to a field,
      // in the format "Nori.Vector2 mPosition Nori.EProp.Xfm". 
      // That is, each line is a tuple with 3 components - the data type of the property, 
      // the name of the underlying field, and the EProp enum that must be raised when this
      // property is changed. 
      var sb = new StringBuilder (sym.ToString ());
      foreach (var m in sym.GetMembers ().OfType<IFieldSymbol> ()) {
         string? eprop = null;
         foreach (var attr in m.GetAttributes ()) {
            var s = attr.ToString ();
            if (s.StartsWith ("Nori.EPropFieldAttribute(")) {
               int n = s.IndexOf (')', 27);
               eprop = s.Substring (25, n - 25);
            }
         }
         if (eprop != null) sb.Append ($"\n{m.Type} {m.Name} {eprop}");
      }
      return sb.ToString ();
   }

   // Given a class name (extracted above), this generates a code fragment that implements the 
   // singleton pattern
   static void GenerateSource (SourceProductionContext context, string? words) {
      if (words == null) return;
      var lines = words.Split (['\n']);
      string fullTypeName = lines[0];
      // Split the className into namespace and type name
      int n = fullTypeName.LastIndexOf ('.'); 
      string nsName = fullTypeName.Substring (0, n);
      string typeName = fullTypeName.Substring (n + 1);

      StringBuilder sb = new ();
      // Generate the property definitions, one for each field. The setter
      // raises the appropriate notification call
      foreach (var field in lines.Skip (1)) {
         string[] fwords = field.Split ();
         string name = fwords[1], typename = fwords[0], eprop = fwords[2];
         if (typename.StartsWith ("Nori.")) typename = typename.Substring (5);
         sb.Append ($$"""
               public {{typename}} {{name.Substring (1)}} {
                  get => {{name}};
                  set { if (Lib.Set (ref {{name}}, value)) Notify ({{eprop.Substring (5)}}); }
               }

            """);
      }

      // Generate the text
      string text = $$"""
         #nullable enable
         using Nori;
         using System.Reactive.Subjects;
         namespace {{nsName}};

         partial class {{typeName}} : IObservable<EProp> {
         {{sb}}
            void Notify (EProp prop) => mSubject?.OnNext (prop);
            public IDisposable Subscribe (IObserver<EProp> observer) => (mSubject ??= new ()).Subscribe (observer);
            Subject<EProp>? mSubject;
         };
         """;
      context.AddSource ($"EProp.{fullTypeName}.g.cs", text);
   }
}
#endregion
