// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Enums.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System;
namespace Nori;

/// <summary>What kind of UX node is this?</summary>
public enum EUKind {
   Root, Rect, Panel,
}

/// <summary>Flags bits for a UXNode</summary>
[Flags]
public enum EUFlags {
   /// <summary>Children are laid out left-to-right (otherwise, top-to-bottom)</summary>
   Horizontal = 1 << 0,
   /// <summary>We need a 'wrap' pass (the contents cannot always fit in the horizontal axis)</summary>
   Wrap = 1 << 1,
   /// <summary>This behaves like a 'scroll container', the child can be scrolled</summary>
   Scrollable = 1 << 2,
   /// <summary>This is a floating / popup element</summary>
   Popup = 1 << 3,
   /// <summary>This popup is laid out relative to screen, not relative to parent</summary>
   ScreenRelative = 1 << 4,
   /// <summary>Draw a shadow for this</summary>
   Shadow = 1 << 5,
}

/// <summary>Various sizing modes for an axis</summary>
public enum EUSizing { Fit, Grow, Fixed, Percent }
/// <summary>Alignment aling X or Y axis (left..right or top..bottom)</summary>
public enum EUAlign { Start, Middle, End }

/// <summary>One of the nine corners of a node (used to align floating elements)</summary>
public enum EUCorner {
   TopLeft, Top, TopRight, Left, Center, Right, BotLeft, Bottom, BotRight
}
/// <summary>Which corners are rounded?</summary>
/// 'Left' means TL and BL corners are rounded
/// 'Top' means TL and RT corners are rounded etc
/// So this model allows one to round 0, 2 or 4 corners (which is all we should need
/// for UI)
public enum EUCorners { None, All, Left, Top, Right, Bottom }


/*

/// <summary>What does GetChildren enumerate</summary>
public enum EEnum { All, Children, Popups };

/// <summary>Persistent data about a node, keyed by Node.UID</summary>
public struct NodeMemo {
   const int HOVERTIME = 350;

   /// <summary>Bounding rect of the node as last laid out</summary>
   public RectS Rect {
      readonly get => mRect;
      set { mRect = value; IsMouseOver = mRect.Contains (UXSystem.MousePos); }
   }
   RectS mRect;

   /// <summary>The mouse has been hovering here for HOVERTIME</summary>
   public readonly bool IsHovered (int ms) {
      if (!IsMouseOver) return false;
      Timers.Start (UId, ms + 1, Lux.Redraw);
      return (uint)Environment.TickCount >= MouseEnterTime + ms;
   }
   static int n;

   /// <summary>Additional data (class-specific)</summary>
   public object Data;

   public int ScrollPos;
   public int MaxScrollPos;
   public short ChildSize;

   /// <summary>Tick-count at which the mouse entered the node</summary>
   public uint MouseEnterTime;
   /// <summary>Tick-count at which the mouse left the node</summary>
   public uint MouseLeaveTime;

   /// <summary>UID of the node owning this memo</summary>
   public uint UId;



   public readonly void Dispose () => Timers.Stop (UId);
}

public class Timers {
   public static void Start (uint uid, int ms, Action callback) {
      if (mTimers.ContainsKey (uid)) return;
      mTimers.Add (uid, new Timer (_ => callback (), null, ms, -1));
   }

   public static void Stop (uint uid) {
      if (mTimers.TryGetValue (uid, out var timer)) {
         timer.Dispose (); mTimers.Remove (uid);
      }
   }

   static Dictionary<uint, Timer> mTimers = [];
}
*/
