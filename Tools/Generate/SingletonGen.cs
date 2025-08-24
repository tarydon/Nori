// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Singleton.cs
// ║║║║╬║╔╣║ A source-code generator that implements the [Singleton] pattern
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Gen;

#region class SingletonGenerator -------------------------------------------------------------------
/// <summary>Implements a safe singleton pattern, for classes decorated with [Singleton]</summary>
/// If you decorate a class TClass with the [Singleton] attribute, and that class has a private
/// parameterless constructor, this code generator implements a 'static TClass It { get; }'
/// property that constructs exactly one instance of this type when first called.
/// This is thread-safe - we use the System.Lazy type to construct it.
[Generator]
class SingletonGenerator : IIncrementalGenerator {
   /// <summary>Implement the IIncrementalGenerator interface</summary>
   public void Initialize (IncrementalGeneratorInitializationContext context) {
      // We just seach for classes decorated with [Singleton] by using the
      // highly performant ForAttributeWithMetadataName filter
      IncrementalValuesProvider<string?> className = context.SyntaxProvider
         .ForAttributeWithMetadataName ("Nori.SingletonAttribute", Always, GetClassName);
      context.RegisterSourceOutput (className, GenerateSource);
   }

   // Helper that always returns true (no further filtering needed on the node)
   static bool Always (SyntaxNode _, CancellationToken __) => true;

   // This transformer extracts the fully qualified class name from the ClassDeclarationSyntax
   // node. If the attribute is (mistakenly) applied on any other C# construct, this returns null
   static string? GetClassName (GeneratorAttributeSyntaxContext context, CancellationToken _) =>
      context.TargetNode switch {
         ClassDeclarationSyntax cd => context.SemanticModel.GetDeclaredSymbol (cd)?.ToString (),
         _ => null
      };

   // Given a class name (extracted above), this generates a code fragment that implements the
   // singleton pattern
   static void GenerateSource (SourceProductionContext context, string? className) {
      if (className == null) return;
      int n = className.LastIndexOf ('.');
      // Handle the case where the class has no namespace now
      string nsName = "", typeName = className;
      if (n != -1) {
         nsName = className.Substring (0, n);
         typeName = className.Substring (n + 1);
      }
      // Generate the text
      string text = $$"""
         partial class {{typeName}} {
            /// <summary>{{typeName}} singleton, created lazily</summary>
            public static {{typeName}} It => sLazy.Value;
            static readonly Lazy<{{typeName}}> sLazy = new (() => new ());
         };
         """;
      if (n != -1) text = $"namespace {nsName};\n{text}";
      context.AddSource ($"Singleton.{className}.g.cs", text);
   }
}
#endregion
