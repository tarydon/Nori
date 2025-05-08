// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Fixture.cs
// ║║║║╬║╔╣║ [Fixture] and [Test] attributes, and support classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static BindingFlags;

#region [Fixture] attribute ------------------------------------------------------------------------
/// <summary>Attribute to attach to a type to label it as a test fixture</summary>
[AttributeUsage (AttributeTargets.Class)]
public class FixtureAttribute (int id, string name, string module) : Attribute {
   /// <summary>Id for this fixture (use Nori.Con NextID to generate the next one)</summary>
   public readonly int Id = id;
   /// <summary>Description for this fixture (keep this to within 80 chars)</summary>
   public readonly string Description = name;
   /// <summary>The test fixture's 'module', like "Fold" or "Bend.Cell"</summary>
   public readonly string Module = module;
   /// <summary>If set, all tests in this fixture are skipped</summary>
   public bool Skip { get; set; }
}
#endregion

#region class Fixture ------------------------------------------------------------------------------
/// <summary>Represents a test fixture (that contains a number of tests)</summary>
public class Fixture {
   internal Fixture (Type type, FixtureAttribute fa) {
      const BindingFlags bf = Instance | Public | NonPublic | DeclaredOnly;
      (Type, Id, Description, Module, Skip) = (type, fa.Id, fa.Description, fa.Module, fa.Skip);
      Constructor = type.GetConstructor (bf, [])
         ?? throw new Exception ($"No parameterless constructor found for {type.FullName}");
      foreach (var mi in type.GetMethods (Instance | Public | NonPublic)) {
         TestAttribute? ta = mi.GetCustomAttribute<TestAttribute> ();
         if (ta != null) mTests.Add (new (mi, ta, this));
      }
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The fixture ID</summary>
   public readonly int Id;
   /// <summary>The description of this fixture</summary>
   public readonly string Description;
   /// <summary>The module for this fixture</summary>
   public readonly string Module;
   /// <summary>The C# type that implements this Fixture</summary>
   public readonly Type Type;
   /// <summary>The set of tests in this Fixture</summary>
   public IReadOnlyList<Test> Tests => mTests;
   readonly List<Test> mTests = [];

   // Implementation -----------------------------------------------------------
   internal ConstructorInfo Constructor;
   internal readonly bool Skip;
}
#endregion

#region [Test] attribute ---------------------------------------------------------------------------
/// <summary>Attribute to attach to a method to label it as a test</summary>
/// The method should take no parameters, should be an instance method, and should
/// return void. It need not be public.
[AttributeUsage (AttributeTargets.Method)]
public class TestAttribute (int id, string name) : Attribute {
   /// <summary>The Id for this test (use nori.con nextid to generate a candidate)</summary>
   public readonly int Id = id;
   /// <summary>Description of this test (keep this to within 80 chars)</summary>
   public readonly string Description = name;
   /// <summary>If set, this test is skipped</summary>
   public bool Skip { get; set; }
}
#endregion

#region class Test ---------------------------------------------------------------------------------
/// <summary>Represents a test method (in a test fixture)</summary>
public class Test {
   internal Test (MethodInfo mi, TestAttribute ta, Fixture fixture)
      => (Method, Id, Description, Fixture, Skip) = (mi, ta.Id, ta.Description, fixture, ta.Skip);

   // Properties ---------------------------------------------------------------
   /// <summary>The Id for this test</summary>
   public readonly int Id;
   /// <summary>The description for this test</summary>
   public readonly string Description;
   /// <summary>The fixture this test belongs in</summary>
   public readonly Fixture Fixture;
   /// <summary>The method that implements this test</summary>
   public readonly MethodInfo Method;

   // Implementation -----------------------------------------------------------
   internal bool Skip { get; set; }
}
#endregion

#region TestException ------------------------------------------------------------------------------
/// <summary>Exception that is thrown when a test fails</summary>
public class TestException (string message) : Exception (message);
#endregion
