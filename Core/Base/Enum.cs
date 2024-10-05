// ────── ╔╗ Nori.Core
// ╔═╦╦═╦╦╬╣ Copyright © 2024 Arvind
// ║║║║╬║╔╣║ Enum.cs ~ Implements various Enum types used by the Pix renderer
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region Enums --------------------------------------------------------------------------------------
/// <summary>These represent the 3 cardinal axes, used for indicating a rotation for example</summary>
public enum EAxis { X, Y, Z }

/// <summary>The 4 cardinal directions</summary>
public enum EDir { E, N, W, S };

/// <summary>Various render-targets for Pix.Panel</summary>
public enum ETarget { Screen, Image, Pick }
#endregion
