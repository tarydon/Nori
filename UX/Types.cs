// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Types.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System;
using System.Threading;
using System.Collections.Generic;

namespace Nori.UX;

struct NodeMemo {
   // Properties ---------------------------------------------------------------
   /// <summary>Additional data (class-specific)</summary>
   public object Data;

   /// <summary>
   /// Returns true if the mouse has been hovering over this element for ms milliseconds
   /// </summary>
   /// This is often used to open a tooltip when the mouse has been hovering over
   /// an element for about 0.3 seconds or so. 
   public readonly bool IsHovered (int ms) {
      if (!IsMouseOver) return false;
      Timers.Start (UId, ms + 1, Lux.Redraw);
      return (uint)Environment.TickCount >= MouseEnterTime + ms;
   }
   static int n;

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

   /// <summary>Current and maximum scroll position and child size</summary>
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
