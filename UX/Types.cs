// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Types.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System;
using System.Threading;
using System.Collections.Generic;

namespace Nori;

/// <summary>Axis metrics</summary>
/// This contains all axis-specific information like the desired size, grow mode,
/// computed size and position etc. We maintain one of these for X and Y 
public struct AxisDef {
   /// <summary>The sizing mode for this axis</summary>
   public EUSizing Mode;
   /// <summary>Minimum, maximum size for this axis (Max=0 is same as Max=int.MaxValue)</summary>
   public int Min, Max;

   /// <summary>Padding at the start (Left/Top)</summary>
   public short PadStart;
   /// <summary>Padding at the end (Right/Bottom)</summary>
   public short PadEnd;
   /// <summary>Alignment of children in this axis</summary>
   public EUAlign ChildAlign;
   /// <summary>Total padding on this axis (start + end)</summary>
   public readonly short TotalPad => (short)(PadStart + PadEnd);

   /// <summary>The start position along this axis (X/Y)</summary>
   public int V;
   /// <summary>The span along this axis (DX/DY) extent is the semi-open interval [V, V+DV)</summary>
   public int DV;

   public void Set (Size size) { Mode = size.Mode; Min = size.Min; Max = size.Max; }
}

public readonly struct Size {
   // Constructors -------------------------------------------------------------
   public Size (EUSizing mode, int min, int max) => (Mode, Min, Max) = (mode, min, max);

   public static Size Grow () => new (EUSizing.Grow, 0, 0);
   public static Size Grow (int n) => new (EUSizing.Grow, n, 0);
   public static Size Grow (int n0, int n1) => new (EUSizing.Grow, n0, n1);
   public static Size Fit () => new (EUSizing.Fit, 0, 0);    // <-- This is the default
   public static Size Fit (int n) => new (EUSizing.Fit, n, 0);
   public static Size Fit (int n0, int n1) => new (EUSizing.Fit, n0, n1);
   public static Size Fixed (int n) => new (EUSizing.Fixed, n, n);

   public static implicit operator Size (int n) => new (EUSizing.Fixed, n, n);

   // Properties ---------------------------------------------------------------
   public readonly EUSizing Mode;
   public readonly int Min;
   public readonly int Max;
}

struct NodeMemo {
   // Properties ---------------------------------------------------------------
   /// <summary>
   /// Additional data (class-specific)
   /// </summary>
   public object Data;

   /// <summary>Is the mouse currently over this node?</summary>
   public bool IsMouseOver {
      readonly get => mIsMouseOver;
      set {
         if (mIsMouseOver == value) return;
         if (mIsMouseOver = value)
            MouseEnterTime = (uint)Environment.TickCount;
         else {
            MouseLeaveTime = (uint)Environment.TickCount;
            Timers.Stop (UId);
         }
      }
   }
   bool mIsMouseOver;

   /// <summary>Tick-count at which the mouse entered this node</summary>
   public uint MouseEnterTime;
   /// <summary>Tick-count at which the mouse left this node</summary>
   public uint MouseLeaveTime;

   /// <summary>Bounding rectangle of the node as last laid out</summary>
   public RectS Rect {
      readonly get => mRect;
      set { mRect = value; IsMouseOver = mRect.Contains (UXEngine.MousePos); }
   }
   RectS mRect;

   /// <summary>
   /// Current and maximum scroll position and child size
   /// </summary>
   public int ScrollPos, MaxScrollPos, ChildSize;

   /// <summary>The UID of the node owning this memo</summary>
   public uint UId;

   // Methods ------------------------------------------------------------------
   // TODO: NodeMemo.Dispose is never called!
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
