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
            HW.MouseMoves.Subscribe (OnMouseMove);
         }
      }
   }
   static Dwg2 mDwg = new ();
   static DwgScene mScene = null!;
   static bool mHooked;

   /// <summary>
   /// The Root GroupVN displaying all the content
   /// </summary>
   public static GroupVN Root { get => mRoot; set => mRoot = value;  }
   static GroupVN mRoot = null!;

   // Event handlers -----------------------------------------------------------
   static void OnMouseMove (Vec2S vec) {
   }

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
}
