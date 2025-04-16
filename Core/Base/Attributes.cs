// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Attributes.cs
// ║║║║╬║╔╣║ Defines attributes used by Nori.Core, and by Nori source generators
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using JetBrains.Annotations;
namespace Nori;

#region [EPropClass] attribute ---------------------------------------------------------------------
/// <summary>[EPropClass] attribute, used to auto-implement IObservable(EProp) for a type.</summary>
/// The class then will have one or more fields decorated with [EPropField], and when
/// any of those fields are modified, observers watching this class are notified. 
[AttributeUsage (AttributeTargets.Class), UsedImplicitly]
public sealed class EPropClassAttribute : Attribute;
#endregion

#region [EPropField] attribute ---------------------------------------------------------------------
/// <summary>[EPropField] attribute, used to mark a field as an 'active' field</summary>
/// A field decorated with [EPropField] can appear within a class decorated with 
/// [EPropClass]. Then, the generator writes a wrapper property around that field,
/// and when that property is written to, it notifies the observers. It's a bit like
/// INotifyPropertyChanged, except that instead of the property name, we use one of
/// the EProp enumerated values instead. 
/// For this, you just write the field, name it with an initial m prefix and decorate it 
/// with this attribute. The property is generated with the same name as the field, but 
/// without the leading 'm' character. For example:
///   [EPropClass]
///   class Circle {
///      [EPropField (EProp.Xfm)] Point2 mCenter;
///      [EPropField (EProp.Geometry)] double mRadius;
///      [EPropField (EProp.Attributes)] Color4 mColor;
///   }
/// This will create properties called Center, Radius and Color, and when these properties
/// are modified, they will raise observer notifications using the corresponding EProp values.
[AttributeUsage (AttributeTargets.Field)]
public class EPropFieldAttribute (EProp prop) : Attribute {
   public readonly EProp Prop = prop;
}
#endregion

#region [RedrawOnZoom] attribute -------------------------------------------------------------------
/// <summary>Attach [RedrawOnZoom] to a widget to have it be automatically redrawn when the scene is zoomed or panned</summary>
[AttributeUsage (AttributeTargets.Class)]
public class RedrawOnZoomAttribute : Attribute { }
#endregion

#region [Singleton] attribute ----------------------------------------------------------------------
/// <summary>[Singleton] attribute, used to auto-implement a singleton pattern</summary>
/// Decorate a class TClass with the [Singleton] attribute, and make sure it has a private, 
/// parameterless constructor. Then, the Nori.Gen code generator implements a static property:
/// `static TClass It { get; }`
/// This constructs a single instance of this type and returns it (this is thread-safe)
[AttributeUsage (AttributeTargets.Class)]
public class SingletonAttribute : Attribute;
#endregion
