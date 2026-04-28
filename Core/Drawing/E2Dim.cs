namespace Nori;

#region class E2Dim --------------------------------------------------------------------------------
/// <summary>E2Dim is the base class for all 2D dimensions</summary>
public abstract partial class E2Dim : Ent2 {
   // Constructors -------------------------------------------------------------
   // Default constructor used during streaming
   protected E2Dim () => (mStyle, mText) = (null!, null);
   /// <summary>Called by derived classes when an E2Dim is built</summary>
   /// <param name="layer">Layer this entity lives in</param>
   /// <param name="kind">Which kind of dimension is this?</param>
   /// <param name="style">The DimStyle2 that provides dimension settings</param>
   /// <param name="pts">Definition points </param>
   /// <param name="text">Dimension text</param>
   /// The interpretation of definition points varies based on the actual kind of dimension
   /// this is. Typically, the set of points is in the same order in which one would click
   /// to input them when creating the dimension dynamically.
   public E2Dim (Layer2 layer, EDim kind, DimStyle2 style, IList<Point2> pts, string? text) : base (layer) {
      (mKind, mStyle, mText) = (kind, style, text is null or "<>" or "" ? null : text);
      if (mText == null) mFlags |= E2Flags.AutoText;
      mPts.AddRange (pts);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The Bound of the dimension</summary>
   public override Bound2 Bound => Bound2.Cached (ref mBound, () => new (Ents.Select (a => a.Bound)));
   Bound2 mBound = new ();

   /// <summary>The entities making up the dimension</summary>
   /// When loading a Dimension from a DXF file, these are stored in Block - we explode
   /// that block and gather the Ents here. When a dimension is created dynamically in the UI,
   /// we call the MakeEnts routine that is overridden for each type of dimension to create the
   /// actual dimension
   public IReadOnlyList<Ent2> Ents {
      get {
         if (mEnts.Count == 0) MakeEnts ();
         return mEnts;
      }
   }
   protected List<Ent2> mEnts = [];

   /// <summary>Has the text been auto-generated (based on measurement)</summary>
   public bool IsAutoText => Get (E2Flags.AutoText);

   /// <summary>Force dimension line to be drawn (to center / between extension lines etc)</summary>
   public bool ForceDimLine => Get (E2Flags.ForceDimLin);

   /// <summary>Which kind of dimension is this?</summary>
   public EDim Kind => mKind;
   EDim mKind;

   /// <summary>Set of points defining the dimension (interpretation depends on the kind)</summary>
   public IReadOnlyList<Point2> Pts => mPts;
   protected List<Point2> mPts = [];

   /// <summary>The DimStyle used by this dimension entity</summary>
   /// This is stored primarily in the DimStyles list of the drawing
   public DimStyle2 Style => mStyle;
   protected DimStyle2 mStyle;

   /// <summary>The text of the dimension, if not blank</summary>
   /// If this is "<>" or "" or null, the default text is used (based on the actual 
   /// measurement). If this is " ", then the text is suppressed.
   public string? Text => mText;
   protected string? mText;

   // Methods ------------------------------------------------------------------
   /// <summary>Get the transformed bound of the dimension</summary>
   public override Bound2 GetBound (Matrix2 xfm)
      => new (Ents.Select (a => a.GetBound (xfm)));

   /// <summary>Gets the 'definition points' of this dimension (for saving to DXF)</summary>
   /// Each tuple in the list is a DXF group code (10,11,12 etc) for the X
   /// coordinate (the Y coordinates are stored at that value + 10)
   public abstract void GetDefPoints (List<(int, Point2)> defPoints);

   /// <summary>Internal routine to load the entities from a block</summary>
   /// Since blocks are not used by any dimension other than this one, we can load the entities
   /// here and later discard that block
   internal Block2? LoadEnts (Dwg2 dwg, string name) {
      if (dwg.Blocks.FirstOrDefault (a => a.Name == name) is { } block) {
         mEnts.AddRange (block.Ents);
         return block;
      }
      return null;
   }

   /// <summary>Check if the Dimension is close to the given point (closer than the given threshold)</summary>
   public override bool IsCloser (Point2 pt, ref double threshold) {
      if (!Bound.Contains (pt, threshold)) return false;
      foreach (var ent in Ents)
         if (ent.IsCloser (pt, ref threshold)) return true;
      return false;
   }

   // Nested types -------------------------------------------------------------
   // What lies on the 'inside' of the dimension (between the two extension lines)
   // The arrows and/or text can lie on the inside or the outside
   [Flags]
   protected enum EInside { None = 0, Text = 1, Arrows = 2, Both = 3 };
}
#endregion   

#region class E2DimGeneric -------------------------------------------------------------------------
/// <summary>E2DimGeneric is a 'fallback' dimension used when we don't have a suitable type</summary>
/// An E2DimGeneric loads and displays all the entities (looks fine visually), but cannot be 
/// edited nor exported to DXF
class E2DimGeneric : E2Dim {
   public E2DimGeneric (Layer2 layer, DimStyle2 style, IList<Point2> pts, string? text)
      : base (layer, EDim.Generic, style, pts, text) { }

   protected override void MakeEnts () => throw new NotImplementedException ();
   public override void GetDefPoints (List<(int, Point2)> defPoints) => throw new NotImplementedException ();

   protected override Ent2 Xformed (Matrix2 xfm) {
      var dim = new E2DimGeneric (Layer, mStyle, [.. mPts.Select (a => a * xfm)], mText);
      dim.mEnts.AddRange (mEnts.Select (a => a * xfm));
      return dim;
   }
}
#endregion

#region class E2DimDia -----------------------------------------------------------------------------
/// <summary>E2DimDia implements a diameter dimension</summary>
/// This image file://N:/Doc/Img/DimDia.png shows the definition points of this type of dimension.
/// When the dimension is created the points a, b (a is the center of the circle , and b is the 
/// dimension definition point) are passed in, along with the radius of the circle. The dimension
/// is created based on whether the point b is inside or outside the circle. The point c (text location)
/// is computed automatically.
/// 
/// When outputting to DXF, the points indicated as (10,20), (15,25) and (11,21) are output as the
/// definition points. 
public class E2DimDia : E2Dim {
   /// <summary>Create a diameter dimension given the definition points as shown in the image below</summary>
   public E2DimDia (Layer2 layer, DimStyle2 style, double radius, bool tofl, IList<Point2> pts, string? text = null)
      : base (layer, EDim.Diameter, style, pts, text) {
      mRadius = radius; if (tofl) mFlags |= E2Flags.ForceDimLin;
   }
   E2DimDia () { }

   // Overrides ----------------------------------------------------------------
   public override void GetDefPoints (List<(int, Point2)> pts) {
      double angle = Pts[0].AngleTo (Pts[1]);
      Point2 pt15 = Pts[0].Polar (mRadius, angle), pt10 = Pts[0].Polar (-mRadius, angle);
      pts.Add ((10, pt10)); pts.Add ((11, Pts[2])); pts.Add ((15, pt15));
   }

   protected override Ent2 Xformed (Matrix2 xfm) {
      var dim = new E2DimDia (Layer, mStyle, mRadius, ForceDimLine, [.. mPts.Select (a => a * xfm)], mText);
      dim.mEnts.AddRange (mEnts.Select (a => a * xfm));
      return dim;
   }

   // Private data -------------------------------------------------------------
   public double Radius => mRadius;
   readonly double mRadius;
}
#endregion

#region class E2DimRad -----------------------------------------------------------------------------
/// <summary>E2DimRad implements a radius dimension</summary>
/// This image file://N:/Doc/Img/DimRad.png shows the definition points of this type of dimension. 
/// When the dimension is created the points a, b are passed in (a is the center of the arc, and 
/// b is the dimension definition point), along with the exact radius of the arc. The dimension is 
/// created based on whether the point b is inside or outside the arc. The point c 
/// (text location) is computed automatically.
/// 
/// When outputting to DXF, the points indicated as (10,20), (15,25) and (11,21) are output 
/// as the definition points. 
public class E2DimRad : E2Dim {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a radius dimension given the definition points as shown in the image below</summary>
   public E2DimRad (Layer2 layer, DimStyle2 style, double radius, bool tofl, IList<Point2> pts, string? text = null)
      : base (layer, EDim.Radius, style, pts, text) {
      mRadius = radius; if (tofl) mFlags |= E2Flags.ForceDimLin;
   }
   E2DimRad () { }

   // Overrides ----------------------------------------------------------------
   public override void GetDefPoints (List<(int, Point2)> pts) {
      pts.Add ((10, Pts[0])); pts.Add ((11, Pts[2]));
      pts.Add ((15, Pts[0].Polar (mRadius, Pts[0].AngleTo (Pts[1]))));
   }

   protected override Ent2 Xformed (Matrix2 xfm) {
      var dim = new E2DimRad (Layer, mStyle, mRadius, ForceDimLine, [.. mPts.Select (a => a * xfm)], mText);
      dim.mEnts.AddRange (mEnts.Select (a => a * xfm));
      return dim;
   }

   // Private data -------------------------------------------------------------
   public double Radius => mRadius;
   readonly double mRadius;
}
#endregion

#region class E2Dim3PAngle -------------------------------------------------------------------------
/// <summary>E2Dim3PAngle is a '3-point angular' dimension</summary>
/// This image file://N:/Doc/Img/Dim3PAngle1.png shows the definition of this type of dimension.
/// The points a, b, c, d are enough to define the dimension (with point e being the optional
/// text positioning point). The values in parentheses are the group codes from which these values
/// are loaded from the DXF file. 
public class E2Dim3PAngle : E2Dim {
   // Constructors -------------------------------------------------------------
   E2Dim3PAngle () { }
   /// <summary>Creates a 3P-Angular dimension given the definition points as shown in the image above</summary>
   /// The point e (text position) is not necessary to be supplied, and will be computed by 
   /// the MakeEnts routine automatically
   public E2Dim3PAngle (Layer2 layer, DimStyle2 style, IList<Point2> pts, string? text = null)
      : base (layer, EDim.Angular3P, style, pts, text) { }

   // Overrides ----------------------------------------------------------------
   public override void GetDefPoints (List<(int, Point2)> pts) {
      pts.Add ((10, mPts[3])); pts.Add ((11, mPts[4]));
      pts.Add ((13, mPts[1])); pts.Add ((14, mPts[2])); pts.Add ((15, mPts[0]));
   }

   // Creates a transformed version of the 3P Angular dimension
   protected override Ent2 Xformed (Matrix2 xfm) {
      var dim = new E2Dim3PAngle (Layer, mStyle, [.. mPts.Select (a => a * xfm)], mText);
      dim.mEnts.AddRange (mEnts.Select (a => a * xfm));
      return dim;
   }
}
#endregion

public class E2DimAngular : E2Dim {
   // Constructors -------------------------------------------------------------
   E2DimAngular () { }
   public E2DimAngular (Layer2 layer, DimStyle2 style, IList<Point2> pts, string? text = null)
      : base (layer, EDim.Angular, style, pts, text) { }

   // Overrides ----------------------------------------------------------------
   public override void GetDefPoints (List<(int, Point2)> defPoints) => throw new NotImplementedException ();

   protected override void MakeEnts () {
      // Get the center point based on the intersection of the first 4 points
      Point2 cen = Geo.LineXLine (Pts[0], Pts[1], Pts[2], Pts[3]).ExceptNil (Point2.Zero), pick = Pts[4];
      if (cen.DistToSq (Pts[0]) > cen.DistToSq (Pts[1])) mPts.Swap (0, 1);
      if (cen.DistToSq (Pts[2]) > cen.DistToSq (Pts[3])) mPts.Swap (2, 3);

      double a0 = cen.AngleTo (mPts[1]), a1 = cen.AngleTo (mPts[2]), rad = cen.DistTo (pick);
      Point2 p0 = cen.Polar (rad, a0), p1 = cen.Polar (rad, a1);
      var seg = Poly.Arc (p0, pick, p1)[0];

      string text = Text ?? "";
      if (IsAutoText) {
         double span = Math.Abs (seg.AngSpan).R2D ().Round (mStyle.AngDecimal);
         text = $"{span}\u00b0";
      }
      text = "45\u00b0";
      BuildEnts (seg, pick, text, mPts.AsSpan (), true, false);
   }

   protected override Ent2 Xformed (Matrix2 xfm) => throw new NotImplementedException ();
}
