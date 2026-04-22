using System.Reactive.Linq;
using Nori;
namespace Zuki;

static class Hub {
   // Properties ---------------------------------------------------------------
   /// <summary>
   /// The drawing currently being edited
   /// </summary>
   public static Dwg2 Dwg { 
      get => mDwg;
      set {
         Lux.UIScene = mScene = new DwgScene (mDwg = value);
         if (!mHooked) {
            mHooked = true;
            MouseMoves.Subscribe (a => CursorVN.It.Pt = a);
            Widget = new Dim3PAngularMaker ();
         }
      }
   }
   static Dwg2 mDwg = new ();
   static DwgScene mScene = null!;
   static bool mHooked;

   /// <summary>
   /// Current PixelScale (how many world units per pixels)
   /// </summary>
   public static double PixelScale => mScene.PixelScale;

   /// <summary>
   /// The Root GroupVN displaying all the content
   /// </summary>
   public static GroupVN Root { get => mRoot; set => mRoot = value;  }
   static GroupVN mRoot = null!;

   public static Widget? Widget;

   // Event handlers -----------------------------------------------------------
   static void OnMouseMove (Vec2S vec) {
      CursorVN.It.Pt = PixelToWorld (vec);
   }

   // Observables --------------------------------------------------------------
   /// <summary>
   /// Returns mouse-moves, in Point2 coordinates
   /// </summary>
   public static IObservable<Point2> MouseMoves 
      => HW.MouseMoves.Select (PixelToWorld);

   /// <summary>
   /// Returns left mouse-clicks in Point2 coordinates
   /// </summary>
   public static IObservable<Point2> LeftClicks
      => HW.MouseClicks.Where (a => a.IsLeftPress).Select (a => PixelToWorld (a.Position));

   // Methods ------------------------------------------------------------------
   /// <summary>
   /// Loads a DXF file and mounts it for editing
   /// </summary>
   public static void LoadDXF (string file) {
      var dr = new DXFReader (file);
      dr.WhiteToBlack = dr.DarkenColors = dr.RelayerDimensions = true;
      dr.StitchThreshold = 0.001;
      Dwg = dr.Load ();
   }

   /// <summary>
   /// Converts pixel coordinates to world coordinates
   /// </summary>
   public static Point2 PixelToWorld (Vec2S pix)
      => (Point2)mScene.PixelToWorld (pix);
}
