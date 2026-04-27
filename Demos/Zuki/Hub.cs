using System.IO;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using Nori;
namespace Zuki;

#region class Hub ----------------------------------------------------------------------------------
/// <summary>Most of the logic of drawing editing is implemented in this Hub class</summary>
/// It maintains state like the current drawing being edited, the current widget etc. 
/// It also provides a number of helper properties and functions
static class Hub {
   // Properties ---------------------------------------------------------------
   /// <summary>UI element displaying the current command name</summary>
   public static TextBlock? CmdName;

   /// <summary>The drawing currently being edited</summary>
   public static Dwg2 Dwg { 
      get => mDwg;
      set {
         Lux.UIScene = mScene = new DwgScene (mDwg = value);
         if (!mHooked) {
            mHooked = true;
            MouseMoves.Subscribe (a => CursorVN.It.Pt = a);
         }
         SetTitle ();
      }
   }
   static Dwg2 mDwg = null!;
   static DwgScene mScene = null!;
   static bool mHooked;

   /// <summary>Toggle to turn on/off drawing fill</summary>
   public static bool FillDrawing {
      get;
      set {
         if (!Lib.Set (ref field, value)) return;
         if (value) Root.Add (new DwgFillVN (Dwg));
         else if (Root.Children.FirstOrDefault (a => a is DwgFillVN) is { } fillVN)
            Root.Remove (fillVN);
      }
   } = true;

   /// <summary>The main window of the application</summary>
   public static Window? MainWindow;

   /// <summary>Current PixelScale (how many world units per pixels)</summary>
   public static double PixelScale => mScene.PixelScale;
   /// <summary>
   /// Current pick aperture
   /// </summary>
   public static double PickAperture => 16 * PixelScale;

   /// <summary>The Root GroupVN displaying all the content</summary>
   public static GroupVN Root { get => mRoot; set => mRoot = value;  }
   static GroupVN mRoot = null!;

   /// <summary>UI element displaying the current command status</summary>
   public static TextBlock? Status;

   /// <summary>The current widget that is running</summary>
   public static Widget? Widget { 
      get => mWidget;
      set { mWidget?.Deactivate (); (mWidget = value)?.Activate ();  }
   }
   static Widget? mWidget;

   // Methods ------------------------------------------------------------------
   public static void DrawEnts (IReadOnlyList<Ent2> ents) {
      mPolys.Clear (); mQuads.Clear (); mPts.Clear ();
      // Draw E2Poly
      mPolys.AddRange (ents.OfType<E2Poly> ().Select (a => a.Poly));
      // Draw E2Text
      mPolys.AddRange (ents.OfType<E2Text> ().SelectMany (a => a.Polys));
      Lux.Polys (mPolys.AsSpan ());
      // Draw E2Solid
      foreach (var p in ents.OfType<E2Solid> ().Select (a => a.Pts)) mQuads.AddM (p[0], p[1], p[3], p[2]);
      Lux.Quads (mQuads.AsSpan ());
      // Draw points
      mPts.AddRange (ents.OfType<E2Point> ().Select (a => (Vec2F)a.Pt));
      Lux.Points (mPts.AsSpan ());
      // Handle dimensions
      ents.OfType<E2Dim> ().ForEach (d => DrawEnts (d.Ents));
   }
   static List<Poly> mPolys = [];
   static List<Vec2F> mQuads = [], mPts = [];

   /// <summary>Draws one entity</summary>
   public static void DrawEnt (Ent2? ent) {
      if (ent is { }) DrawEnts ([ent]);
   }

   /// <summary>
   /// Highlights the current segment in transparent blue
   /// </summary>
   public static void HighlightSeg (Seg seg) {
      mPts.Clear (); 
      Lux.LineWidth = 3f; Lux.Color = new (128, 0, 0, 255); Lux.ZLevel = 100;
      if (seg.IsArc) { seg.ToBeziers (mPts); Lux.Beziers (mPts.AsSpan ()); }
      else { mPts.AddM (seg.A, seg.B); Lux.Lines (mPts.AsSpan ()); }
   }

   /// <summary>Loads a DXF file and mounts it for editing</summary>
   public static void LoadDXF (string file) {
      var dr = new DXFReader (file);
      dr.WhiteToBlack = dr.DarkenColors = dr.RelayerDimensions = true;
      dr.StitchThreshold = 0.001;
      Dwg = dr.Load ();
   }

   /// <summary>Converts pixel coordinates to world coordinates</summary>
   public static Point2 PixelToWorld (Vec2S pix)
      => (Point2)mScene.PixelToWorld (pix);

   /// <summary>Sets the current command name</summary>
   public static void SetCmdName (string text) => CmdName?.Text = text;

   /// <summary>Sets the status text</summary>
   public static void SetStatusText (string text) => Status?.Text = text;

   /// <summary>Sets the title of the main window</summary>
   public static void SetTitle () {
      string s = $"Z U K I";
      if (!mDwg.Filename.IsBlank ()) s += $"  \u2022  {Path.GetFileNameWithoutExtension (mDwg.Filename)}";
      MainWindow?.Title = s;
   }

   // Observables --------------------------------------------------------------
   /// <summary>Returns mouse-moves, in Point2 coordinates</summary>
   public static IObservable<Point2> MouseMoves 
      => HW.MouseMoves.Select (PixelToWorld);

   /// <summary>Returns left mouse-clicks in Point2 coordinates</summary>
   public static IObservable<Point2> LeftClicks
      => HW.MouseClicks.Where (a => a.IsLeftPress).Select (a => PixelToWorld (a.Position));
}
#endregion
