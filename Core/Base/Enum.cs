// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Enum.cs
// ║║║║╬║╔╣║ Implements various Enum types used by the Lux renderer
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region Enums --------------------------------------------------------------------------------------
/// <summary>These represent the 3 cardinal axes, used for indicating a rotation for example</summary>
public enum EAxis { X, Y, Z }

/// <summary>The 4 cardinal directions</summary>
public enum EDir { E, N, W, S }

/// <summary>Types of joints used in a mechanism</summary>
public enum EJoint { None, Translate, Rotate };

/// <summary>Different linetypes (supported by the Pix renderer, PDF writer etc)</summary>
public enum ELineType { Continuous, Dot, Dash, DashDot, DashDotDot, Center, Border, Hidden, Dash2, Phantom }

/// <summary>Various 'well-known' properties</summary>
public enum EProp {
   /// <summary>The transform of an entity (and its subtree) has changed</summary>
   Xfm = 1,
   /// <summary>An entity's geometry has changed (needs new geomery-gather)</summary>
   Geometry,
   /// <summary>An entity's attributes have changed (like color, typeface etc)</summary>
   Attributes,
   /// <summary>An entity's visibility has changed</summary>
   Visibility,
   /// <summary>The selection state is changed</summary>
   Selected,
   /// <summary>
   /// The joint value of a mechanism
   /// </summary>
   JValue,

   Grid, FillInterior, CurrentLayer
}

/// <summary>The possible values for text-alignment within a box</summary>
public enum ETextAlign {
   TopLeft = 1, TopCenter = 2, TopRight = 3,
   MidLeft = 4, MidCenter = 5, MidRight = 6,
   BotLeft = 7, BotCenter = 8, BotRight = 9,
   BaseLeft = 10, BaseCenter = 11, BaseRight = 12
}

/// <summary>Various render-targets for Lux.Panel</summary>
public enum ETarget { Screen, Image, Pick }
#endregion
