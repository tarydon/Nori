using System.IO;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using Nori;
namespace Zuki;

static class Hub {
   // Properties ---------------------------------------------------------------
   /// <summary>The drawing currently being edited</summary>
   public static Dwg2 Dwg { 
      get => mDwg;
      set {
         Lux.UIScene = mScene = new DwgScene (mDwg = value);
         if (!mHooked) {
            mHooked = true;
            MouseMoves.Subscribe (a => CursorVN.It.Pt = a);
            Widget = new Dim3PAngularMaker ();
         }
         SetTitle ();
      }
   }
   static Dwg2 mDwg = new ();
   static DwgScene mScene = null!;
   static bool mHooked;

   public static bool FillDrawing {
      get;
      set {
         if (!Lib.Set (ref field, value)) return;
         if (value) Root.Add (new DwgFillVN (Dwg));
         else if (Root.Children.FirstOrDefault (a => a is DwgFillVN) is { } fillVN)
            Root.Remove (fillVN);
      }
   } = true;

   /// <summary>Current PixelScale (how many world units per pixels)</summary>
   public static double PixelScale => mScene.PixelScale;

   /// <summary>The Root GroupVN displaying all the content</summary>
   public static GroupVN Root { get => mRoot; set => mRoot = value;  }
   static GroupVN mRoot = null!;

   public static TextBlock? Status;
   public static TextBlock? Command;

   public static string StatusText { set => Status?.Text = value; }
   public static string CommandText { set => Command?.Text = value; }

   /// <summary>The current widget that is running</summary>
   public static Widget? Widget { 
      get => mWidget;
      set { mWidget?.Deactivate (); (mWidget = value)?.Activate ();  }
   }
   static Widget? mWidget;

   public static Window? MainWindow;

   // Event handlers -----------------------------------------------------------
   static void OnMouseMove (Vec2S vec) {
      CursorVN.It.Pt = PixelToWorld (vec);
   }

   // Observables --------------------------------------------------------------
   /// <summary>Returns mouse-moves, in Point2 coordinates</summary>
   public static IObservable<Point2> MouseMoves 
      => HW.MouseMoves.Select (PixelToWorld);

   /// <summary>Returns left mouse-clicks in Point2 coordinates</summary>
   public static IObservable<Point2> LeftClicks
      => HW.MouseClicks.Where (a => a.IsLeftPress).Select (a => PixelToWorld (a.Position));

   // Methods ------------------------------------------------------------------
   public static void DrawEnts (IReadOnlyList<Ent2> ents) {
      mPolys.Clear (); mQuads.Clear (); mPoints.Clear (); 
      // Draw E2Poly
      mPolys.AddRange (ents.OfType<E2Poly> ().Select (a => a.Poly));
      // Draw E2Text
      mPolys.AddRange (ents.OfType<E2Text> ().SelectMany (a => a.Polys));
      Lux.Polys (mPolys.AsSpan ());
      // Draw E2Solid
      foreach (var p in ents.OfType<E2Solid> ().Select (a => a.Pts)) mQuads.AddM (p[0], p[1], p[3], p[2]);
      Lux.Quads (mQuads.AsSpan ());
      // Draw points
      mPoints.AddRange (ents.OfType<E2Point> ().Select (a => (Vec2F)a.Pt));
      Lux.Points (mPoints.AsSpan ());
      // Handle dimensions
      ents.OfType<E2Dim> ().ForEach (d => DrawEnts (d.Ents));
   }
   static List<Poly> mPolys = [];
   static List<Vec2F> mQuads = [], mPoints = [];

   /// <summary>Draws one entity</summary>
   public static void DrawEnt (Ent2? ent) {
      if (ent is { }) DrawEnts ([ent]);
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

   /// <summary>Sets the title of the main window</summary>
   public static void SetTitle () {
      string s = $"Z U K I";
      if (!mDwg.Filename.IsBlank ()) s += $"  \u2022  {Path.GetFileNameWithoutExtension (mDwg.Filename)}";
      MainWindow?.Title = s;
   }
}
