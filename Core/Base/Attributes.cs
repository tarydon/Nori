// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Attributes.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region [Singleton] attribute ----------------------------------------------------------------------
/// <summary>[Singleton] attribute, used to auto-implement a singleton pattern</summary>
/// Decorate a class TClass with the [Singleton] attribute, and make sure it has a private, 
/// parameterless constructor. Then, the Nori.Gen code generator implements a static property:
/// `static TClass It { get; }`
/// This constructs a single instance of this type and returns it (this is thread-safe)
[AttributeUsage (AttributeTargets.Class)]
public class SingletonAttribute : Attribute { }
#endregion
