// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Enum.cs
// ║║║║╬║╔╣║ Implements various Enum types used by the Lux renderer
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region Enums --------------------------------------------------------------------------------------
/// <summary>These represent the 3 cardinal axes, used for indicating a rotation for example</summary>
public enum EAxis { X, Y, Z }

/// <summary>The 4 cardinal directions</summary>
public enum EDir { E, N, W, S };

/// <summary>Different linetypes (supported by the Pix renderer, PDF writer etc)</summary>
public enum ELineType { Continuous, Dot, Dash, DashDot, DashDotDot, Center, Border, Hidden, Dash2, Phantom };

/// <summary>Various render-targets for Lux.Panel</summary>
public enum ETarget { Screen, Image, Pick }
#endregion
