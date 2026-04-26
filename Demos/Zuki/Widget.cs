using System.Reactive.Linq;
using Nori;
namespace Zuki;
using static Lux;

class Widget {
   protected Widget () {
      Dwg = Hub.Dwg;
      mDisp.Add (Hub.MouseMoves.Subscribe (OnMouseMove));
      mDisp.Add (Hub.LeftClicks.Subscribe (OnLeftClick));
      mDisp.Add (HW.Keys.Where (a => a.IsPress ()).Subscribe (OnKey));

      if (sPrompts.Count == 0) {
         IniFile ini = new ("N:/Demos/Zuki/Res/Widget.txt", "Basic");
         List<string> prompts = [];
         foreach (var section in ini.Sections.ToList ()) {
            ini.Section = section; 
            int clicks = ini.GetN ("Phases");
            string desc = ini.GetS ("Name");
            prompts.Clear ();
            for (int i = 0; i < clicks; i++) prompts.Add (ini.GetS (i.ToString ()));
            sPrompts[section] = new (desc, prompts);
         }
      }

      Info = sPrompts[GetType ().Name.Replace ("Maker", "")];
   }
   protected Dwg2 Dwg;

   public void Activate () {
      Hub.SetCmdName (Info.Name.ToUpper ());
      Hub.SetStatusText (Info.Prompts[0]);
   }

   public virtual void Completed () { }

   public void Deactivate () => mDisp.Dispose ();

   protected CmdInfo Info;

   public virtual bool CanAdvance (Point2 pt) => true;

   public virtual void Draw () {
      (Color, LineWidth, LTScale) = (Color4.DarkGreen, 1.25f, 32);
   }

   public virtual void OnKey (KeyInfo key) { }

   public virtual void OnMouseMove (Point2 pt) { 
      Pts[^1] = pt; WidgetVN.It.Redraw ();
   }

   public virtual void OnLeftClick (Point2 pt) {
      if (!CanAdvance (pt)) return;
      if (Pts.Count == Info.Phases) { Completed (); Pts.Clear (); }
      Pts.Add (pt); WidgetVN.It.Redraw ();
      Hub.SetStatusText (Info.Prompts[Pts.Count - 1]);
   }

   public int Phase => Pts.Count;

   protected List<Point2> Pts = [Point2.Nil];

   // Nested types -------------------------------------------------------------
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

/// <summary>Subtype of Widget used to make entities</summary>
abstract class MakerWidget : Widget {
   public override void Completed () {
      if (MakeEnt () is { } ent) Dwg.Add (ent);
   }

   public override void Draw () { 
      base.Draw (); Hub.DrawEnt (MakeEnt ());
   }

   public virtual Ent2? MakeEnt () => null;
}

/// <summary>Subtype of widget used to make dimensions</summary>
abstract class DimMakerWidget : MakerWidget {
   protected DimMakerWidget () => DimStyle = Dwg.CurrentDimStyle;
   protected DimStyle2 DimStyle;
}

class Dim3PAngularMaker : DimMakerWidget {
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

class DimRadiusMaker : DimMakerWidget {
   public override void Draw () {
      base.Draw ();
      if (Phase == 1) {
         mSeg = null;
         if (Dwg.PickPoly (Pts[0], Hub.PickAperture, out var p)) 
            if (p.Poly[p.Seg] is { IsArc: true } seg) mSeg = seg;
         if (mSeg is { } s) Hub.HighlightSeg (s);
      }
   }

   public override void OnKey (KeyInfo ki) {
      if (ki.Key == EKey.F5) { mTOFL = !mTOFL; WidgetVN.It.Redraw (); }
   }

   public override bool CanAdvance (Point2 pt) {
      if (mSeg != null) { Pts[0] = mSeg.Value.Center; return true; }
      return false;
   }

   public override Ent2? MakeEnt () {
      if (Phase == 2 && mSeg is { } seg) {
         Point2 pt = seg.Center.Polar (seg.Radius, seg.Center.AngleTo (Pts[1]));
         double lie = seg.GetLie (pt).Clamp (); pt = seg.GetPointAt (lie);
         Pts[1] = seg.Center.Polar (seg.Center.DistTo (Pts[1]), seg.Center.AngleTo (pt));
         return new E2DimRad (Dwg.CurrentLayer, DimStyle, seg.Radius, mTOFL, Pts);
      }
      return null;
   }

   Seg? mSeg = null;
   static bool mTOFL;
}

class DimDiaMaker : DimMakerWidget {
}