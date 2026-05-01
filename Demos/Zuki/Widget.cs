using System.Reactive.Linq;
using Nori;
namespace Zuki;
using static Lux;

#region class Widget -------------------------------------------------------------------------------
/// <summary>Base class for all widgets</summary>
class Widget {
   // Constructor --------------------------------------------------------------
   protected Widget () {
      Dwg = Hub.Dwg;
      mDisp.Add (Hub.MouseMoves.Subscribe (OnMouseMove));
      mDisp.Add (Hub.LeftClicks.Subscribe (OnLeftClick));
      mDisp.Add (HW.Keys.Where (a => a.IsPress ()).Subscribe (OnKey));

      if (sPrompts.Count == 0) {
         IniFile ini = new ("N:/Demos/Zuki/Res/Widget.txt", "Basic");
         List<string> prompts = [];
         foreach (var section in ini.Sections.ToList ()) {
            prompts.Clear ();
            ini.Section = section;
            var (clicks, desc) = (ini.GetN ("Phases"), ini.GetS ("Name"));
            for (int i = 0; i < clicks; i++) prompts.Add (ini.GetS (i.ToString ()));
            sPrompts[section] = new (desc, prompts);
         }
      }
      Info = sPrompts[GetType ().Name.Replace ("Maker", "")];
   }

   // Properties ---------------------------------------------------------------
   // Drawing we're working with
   protected Dwg2 Dwg;

   // Prompt information for this command
   // (command name, prompts for each phase etc)
   protected CmdInfo Info;

   // The current phase
   public int Phase => Pts.Count;

   // List of points (each click adds one)
   protected List<Point2> Pts = [Point2.Nil];

   // Methods ------------------------------------------------------------------
   // Called by the hub when this Widget becomes active
   public void Activate () {
      Hub.SetCmdName (Info.Name.ToUpper ());
      Hub.SetStatusText (Info.Prompts[0]);
   }

   // Called by the Hub when this widget becomes inactive
   public void Deactivate () => mDisp.Dispose ();

   // Can be called from derived classes to update widget feedback
   protected void Redraw () => WidgetVN.It.Redraw ();

   // Overrideables ------------------------------------------------------------
   // Override this to permit/block advancing to the next phase.
   // For example, a DimRadiusMaker can advance only when a curved segment is
   // selected.
   public virtual bool CanAdvance (Point2 pt) => true;

   // Override this (typically to create the Entity at the end)
   public virtual void Completed () { }

   // Override this to draw the actual feedback for the current phase.
   // Remember to call base.Draw() to set up the default color / linewidth
   // line-type scale etc.
   public virtual void Draw () 
      => (Color, LineWidth, LTScale) = (Color4.DarkGreen, 1.25f, 32);

   // Called each time the mouse is clicked.
   // On each click, this adds a new point into the Pts list (subsequent 
   // mouse-moves will keep updating that newly added point)
   public virtual void OnLeftClick (Point2 pt) {
      if (!CanAdvance (pt)) return;
      if (Pts.Count == Info.Phases) { Completed (); Pts.Clear (); }
      Pts.Add (pt); WidgetVN.It.Redraw ();
      Hub.SetStatusText (Info.Prompts[Pts.Count - 1]);
   }

   // Called each time the mouse moves.
   // This updates the last point in the Pts list, and then calls redraw by default.
   // You can override this if you want to do something more specific
   public virtual void OnMouseMove (Point2 pt) { Pts[^1] = pt; WidgetVN.It.Redraw (); }

   // Override this to handle key-presses (F5=toggle)
   public virtual void OnKey (KeyInfo key) { }

   // Nested types -------------------------------------------------------------
   // Maintains information about this prompt
   internal class CmdInfo {
      internal CmdInfo (string desc, IList<string> prompts) {
         Name = desc; Prompts = [.. prompts]; Phases = Prompts.Length;
      }

      public string Name;        // Description of the command
      public int Phases;         // How many clicks to complete
      public string[] Prompts;   // Set of prompts
   }
   protected static Dictionary<string, CmdInfo> sPrompts = [];

   // Private data -------------------------------------------------------------
   MultiDispose mDisp = new ();     // Everything we must dispose at end
}
#endregion

#region class EntMaker -----------------------------------------------------------------------------
/// <summary>Subtype of Widget used to make entities</summary>
abstract class EntMaker : Widget {
   public Layer2 Layer => Dwg.CurrentLayer;

   // Overridden to add the entity into the drawing
   public override void Completed () { if (MakeEnt () is { } ent) Dwg.Add (ent); }

   // While an entity is being created/modified, draws the entity being created
   public override void Draw () { base.Draw (); Hub.DrawEnt (MakeEnt ()); }

   // Override this in derived classes to make an entity with the inputs we've got
   // so far. 
   public virtual Ent2? MakeEnt () => null;
}
#endregion

#region class DimWidget ----------------------------------------------------------------------------
/// <summary>Subtype of EntWidget used to make dimensions</summary>
abstract class DimMaker : EntMaker {
   protected DimStyle2 DimStyle => Dwg.CurrentDimStyle;
}
#endregion

#region class Dim3PAngleMaker ----------------------------------------------------------------------
/// <summary>Widget that makes 3-P angular dimensions</summary>
class Dim3PAngleMaker : DimMaker {
   public override void Draw () {
      base.Draw ();
      Points (Pts.Select (a => (Vec2F)a).ToArray ());
      LineType = ELineType.Dot;
      if (Phase is > 1 and < 4) Lines ([Pts[0], Pts[1]]);
      if (Phase is > 2 and < 4) Lines ([Pts[0], Pts[2]]);
   }

   public override Ent2? MakeEnt () {
      if (Phase == 4) return new E2Dim3PAngle (Dwg.CurrentLayer, DimStyle, Pts);
      return null;
   }
}
#endregion

#region class DimRadMaker --------------------------------------------------------------------------
/// <summary>Widget that makes E2DimRad (radius dimension) entities</summary>
class DimRadMaker : DimMaker {
   // We can advanced to the next phase if we have selected a curved segment
   public override bool CanAdvance (Point2 pt) {
      if (mSeg != null) { Pts[0] = mSeg.Value.Center; return true; }
      return false;
   }

   // In the draw routine, we highlight the seg if we are near a curved seg
   public override void Draw () {
      base.Draw ();
      if (Phase == 1) {
         mSeg = null;
         if (Dwg.PickPoly (Pts[0], Hub.PickAperture, out var p)) 
            if (p.Poly[p.Seg] is { IsArc: true } seg) Hub.HighlightSeg (mSeg = seg);
      }
   }

   // When F5 is pressed,, turn TOFL on/off
   public override void OnKey (KeyInfo ki) {
      if (ki.Key == EKey.F5) { mTOFL = !mTOFL; Redraw (); }
   }

   // To make the arc dimension, clamp the second point (positioning the arc dimension)
   // to lie within the span of the arc (but we shouldn't snap it to lie ON the arc itself,
   // - we need to preserve the distance from the center as supplied by the user input)
   public override Ent2? MakeEnt () {
      if (Phase == 2 && mSeg is { } seg) {
         Point2 pt = seg.Center.Polar (seg.Radius, seg.Center.AngleTo (Pts[1]));
         double lie = seg.GetLie (pt).Clamp (); pt = seg.GetPointAt (lie);
         Pts[1] = seg.Center.Polar (seg.Center.DistTo (Pts[1]), seg.Center.AngleTo (pt));
         return new E2DimRad (Dwg.CurrentLayer, DimStyle, seg.Radius, mTOFL, Pts);
      }
      return null;
   }

   // Private data -------------------------------------------------------------
   Seg? mSeg = null;
   static bool mTOFL;
}
#endregion

#region class DimDiaMaker --------------------------------------------------------------------------
/// <summary>Widget used to make diameter dimensions</summary>
class DimDiaMaker : DimMaker {
   // We can advance to the next phase if we have selected a full circle segment
   public override bool CanAdvance (Point2 pt) {
      if (mSeg is { } s) { Pts[0] = s.Center; return true; }
      return false;
   }

   // In the draw routine, we highlight the seg if we are near a circle
   public override void Draw () {
      base.Draw ();
      if (Phase == 1) {
         mSeg = null;
         if (Dwg.PickPoly (Pts[0], Hub.PickAperture, out var p))
            if (p.Poly.IsCircle) Hub.HighlightSeg (mSeg = p.Poly[0]);
      }
   }

   // When F5 is pressed,, turn TOFL on/off
   public override void OnKey (KeyInfo ki) {
      if (ki.Key == EKey.F5) { mTOFL = !mTOFL; Redraw (); }
   }

   // Make the diameter dimension
   public override Ent2? MakeEnt () {
      if (Phase == 2 && mSeg is { } seg) 
         return new E2DimDia (Dwg.CurrentLayer, DimStyle, seg.Radius, mTOFL, Pts);
      return null;
   }

   // Private data -------------------------------------------------------------
   Seg? mSeg = null;
   static bool mTOFL;
}
#endregion

#region class DimAngleMaker ------------------------------------------------------------------------
class DimAngleMaker : DimMaker {
   public override bool CanAdvance (Point2 pt) => Phase == 3 || mSegs.Count >= Phase;

   public override void Draw () {
      base.Draw ();
      while (mSegs.Count >= Phase) mSegs.RemoveLast ();
      if (Dwg.PickPoly (Pts[Phase - 1], Hub.PickAperture, out var p))
         if (p.Poly[p.Seg] is { IsLine: true } seg) {
            Span<Point2> buffer = stackalloc Point2[2];
            if (mSegs.Count == 0 || mSegs[0].Intersect (seg, buffer, false).Length > 0)
               mSegs.Add (seg);
         }
      mSegs.ForEach (a => Hub.HighlightSeg (a));
   }

   public override Ent2? MakeEnt () {
      if (Phase == 3) {
         List<Point2> pts = [];
         foreach (var seg in mSegs.Take (2)) { pts.Add (seg.A); pts.Add (seg.B); }
         pts.Add (Pts[2]);
         return new E2DimAngle (Layer, DimStyle, pts, "45\u00b0");
      }
      return null;
   }

   // Private data -------------------------------------------------------------
   List<Seg> mSegs = [];
}
#endregion
