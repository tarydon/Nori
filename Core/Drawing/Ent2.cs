namespace Nori;

#region class E2Dimension --------------------------------------------------------------------------
/// <summary>Represents a dimension entity</summary>
public class E2Dimension : Ent2 {
   E2Dimension () => mEnts = [];
   public E2Dimension (Layer2 layer, IEnumerable<Ent2> ents) : base (layer) => mEnts = [.. ents];

   // Overrides ----------------------------------------------------------------
   public override Bound2 Bound
      => Bound2.Update (ref mBound, () => new (mEnts.Select (a => a.Bound)));
   Bound2 mBound = new ();

   public override Bound2 GetBound (Matrix2 xfm)
      => new (mEnts.Select (a => a.GetBound (xfm)));

   // The entities making up the dimension (in DXF, this is stored in a block, but since that
   // block is used exactly once, it makes more sense to just store the entities here and create
   // the block on the fly when the dimension is saved)
   public IReadOnlyList<Ent2> Ents => mEnts;
   Ent2[] mEnts;
}
#endregion

#region class E2Insert -----------------------------------------------------------------------------
/// <summary>Represents an INSERT entity (an instance of a block placed in a drawing)</summary>
public class E2Insert : Ent2 {
   // Constructors -------------------------------------------------------------
   E2Insert () => (mBlockName, mDwg) = ("", null!);
   public E2Insert (Dwg dwg, Layer2 layer, string blockName, Point2 pt, double angle, double xScale, double yScale) : base (layer)
      => (mDwg, mAngle, mBlockName, XScale, YScale, mPt) = (dwg, angle, blockName, xScale, yScale, pt);

   // Properties ---------------------------------------------------------------
   /// <summary>Rotation angle of the block, in radians</summary>
   public double Angle => mAngle;
   double mAngle;

   /// <summary>The Block this E2Insert is referencing</summary>
   public Block2 Block => mBlock ??= mDwg.GetBlock (mBlockName) ?? throw new Exception ($"Block {mBlockName} not found");
   Block2? mBlock;
   Dwg mDwg;

   /// <summary>Name of the block</summary>
   public string BlockName => mBlockName;
   string mBlockName;

   /// <summary>X and Y scaling factors for the block</summary>
   public readonly double XScale, YScale;

   /// <summary>Insertion position of the block</summary>
   public Point2 Pt => mPt;
   Point2 mPt;

   /// <summary>Computes the Xfm for the block (based on scale, rotation etc)</summary>
   public Matrix2 Xfm {
      get {
         if (_xfm != null) return _xfm;
         var (x, y) = (XScale, YScale);
         var xfm = Matrix2.Identity;
         Vector2 shift = -(Vector2)Block.Base;
         if (!shift.IsZero) xfm *= Matrix2.Translation (shift);
         if (x < 0) xfm *= Matrix2.HMirror;
         if (y < 0) xfm *= Matrix2.VMirror;
         (x, y) = (Math.Abs (x), Math.Abs (y));
         if (!(x.EQ (1) && y.EQ (1))) xfm *= Matrix2.Scaling (x, y);
         if (!mAngle.IsZero ()) xfm *= Matrix2.Rotation (mAngle);
         return _xfm = xfm * Matrix2.Translation ((Vector2)mPt);
      }
   }
   Matrix2? _xfm;

   // Overrides ----------------------------------------------------------------
   public override Bound2 Bound
      => Bound2.Update (ref mBound, () => new (Block.Ents.Select (a => a.GetBound (Xfm))));
   Bound2 mBound = new ();

   public override Bound2 GetBound (Matrix2 xfm) {
      var final = xfm * Xfm;
      return new (Block.Ents.Select (a => a.GetBound (final)));
   }

   // Methods ------------------------------------------------------------------
   public override bool IsCloser (Point2 worldPt, ref double threshold) {
      var xfmInv = Xfm.GetInverse ();
      var scale = xfmInv.ScaleFactor;
      var (isClose, localPt, localThreshold) = (false, worldPt * xfmInv, threshold * scale);

      // Check IsCloser against each entity in the block, using the 'scaled' threshold
      foreach (var ent in Block.Ents)
         isClose |= ent.IsCloser (localPt, ref localThreshold);

      // Unscale the threshold if we found a close entity
      if (isClose) threshold = localThreshold / scale;
      return isClose;
   }
}
#endregion

#region class E2Point ------------------------------------------------------------------------------
/// <summary>Represents a point in a drawing.</summary>
/// <param name="layer">Entity layer</param>
/// <param name="pos">Point position</param>
public class E2Point : Ent2 {
   E2Point () { }
   public E2Point (Layer2 layer, Point2 pos) : base (layer) => mPt = pos;

   // Properties ---------------------------------------------------------------
   /// <summary>The actual point</summary>
   public Point2 Pt => mPt;
   readonly Point2 mPt;

   /// <summary>Bound of the point</summary>
   public override Bound2 Bound => new (mPt.X, mPt.Y);
   /// <summary>Compute the bound, under a transform</summary>
   public override Bound2 GetBound (Matrix2 xfm) => new (mPt * xfm);

   // Methods ------------------------------------------------------------------
   public override bool IsCloser (Point2 pt, ref double threshold) {
      double dist = pt.DistTo (mPt);
      if (dist < threshold) { threshold = dist; return true; }
      return false;
   }
}
#endregion

#region class E2Poly -------------------------------------------------------------------------------
/// <summary>A polyline drawing entity.</summary>
/// <param name="inLayer">Entity layer</param>
/// <param name="inPoly">The poly object this entity holds.</param>
public class E2Poly : Ent2 {
   // Constructors -------------------------------------------------------------
   E2Poly () => mPoly = null!;
   E2Poly (Ent2 template, Poly poly) : base (template) => mPoly = poly;
   public E2Poly (Layer2 layer, Poly poly) : base (layer) => mPoly = poly;

   // Properties ---------------------------------------------------------------
   public override Bound2 Bound => Bound2.Update (ref mBound, mPoly.GetBound);
   Bound2 mBound = new ();

   /// <summary>The Poly object that defines this entity's actual shape.</summary>
   public Poly Poly => mPoly;
   public readonly Poly mPoly;

   // Methods ------------------------------------------------------------------
   public override bool IsCloser (Point2 pt, ref double threshold) {
      if (!Bound.Contains (pt, threshold)) return false;
      double minDist = threshold;
      foreach (var seg in mPoly.Segs) {
         double dist = seg.GetDist (pt, cutoff: minDist);
         if (dist < minDist) minDist = dist;
      }
      if (minDist < threshold) { threshold = minDist; return true; }
      return false;
   }

   /// <summary>Compute the Bound of the E2Poly under a rotation</summary>
   public override Bound2 GetBound (Matrix2 xfm) => mPoly.GetBound (xfm);

   /// <summary>Makes a clone of this E2Poly, but just with a different polyline</summary>
   /// This copies the layer, color and flags from the existing poly
   public E2Poly With (Poly poly) => new (this, poly);
}
#endregion

#region class E2Solid ------------------------------------------------------------------------------
/// <summary>A solid drawing entity.</summary>
/// <param name="layer">Entity layer</param>
/// <param name="pts">List of points</param>
public class E2Solid : Ent2 {
   E2Solid () => mPts = [];
   public E2Solid (Layer2 layer, IEnumerable<Point2> pts) : base (layer) => mPts = [.. pts];

   #region Properties ------------------------------------------------
   /// <summary>This is the bound of a solid</summary>
   public override Bound2 Bound => new (mPts);

   public override Bound2 GetBound (Matrix2 xfm)
      => new (mPts.Select (a => a * xfm));

   /// <summary>The list of points in this solid</summary>
   public IReadOnlyList<Point2> Pts => mPts;
   readonly Point2[] mPts;
   #endregion
}
#endregion

#region class E2Text -------------------------------------------------------------------------------
/// <summary>A drawing text entity.</summary>
/// <param name="layer">Entity layer</param>
/// <param name="style">The text style (specifies the font)</param>
/// <param name="text">The text value</param>
/// <param name="pos">Poisition where the text is placed in the drawing.</param>
/// <param name="height">The text height</param>
/// <param name="angle">The text rotation angle in radians</param>
public class E2Text : Ent2 {
   // Constructors -------------------------------------------------------------
   E2Text () => (Text, Style, XScale) = ("", Style2.Default, 1);
   public E2Text (Layer2 layer, Style2 style, string text, Point2 pos, double height, double angle, double oblique, double xscale, ETextAlign align) : base (layer)
      => (Text, Style, Pt, Height, Angle, Oblique, XScale, Alignment) = (text, style, pos, height, angle, oblique, xscale, align);

   // Properties ---------------------------------------------------------------
   /// <summary>The text rotation angle in radians</summary>
   public readonly double Angle;

   /// <summary>The alignment of the text (specifies which corner is located at Pt)</summary>
   public readonly ETextAlign Alignment;

   /// <summary>The text height</summary>
   public readonly double Height;

   /// <summary>Spacing between lines for multiline text</summary>
   public double DYLine { get { Render (); return _DYLine; } }
   double _DYLine;

   /// <summary>Text obliquing angle, in radians</summary>
   public readonly double Oblique;

   /// <summary>Position of the text in the drawing - Alignment specifies which corner is aligned to this point</summary>
   public readonly Point2 Pt;

   /// <summary>Text Style to use (specifies font, height override etc)</summary>
   public readonly Style2 Style;

   /// <summary>The actual text value</summary>
   public readonly string Text;

   /// <summary>The X-stretch factor for the text (1=normal)</summary>
   public readonly double XScale;

   // Overrides ----------------------------------------------------------------
   /// <summary>The text bounding rectangle.</summary>
   public override Bound2 Bound
      => Bound2.Update (ref mBound, () => new (Polys.Select (x => x.GetBound ())));
   Bound2 mBound = new ();

   public override Bound2 GetBound (Matrix2 xfm)
      => new (Polys.Select (a => a.GetBound (xfm)));

   public override bool IsCloser (Point2 pt, ref double threshold) {
      // Quick reject if point is outside bounding box expanded by threshold
      if (!Bound.Contains (pt, threshold)) return false;

      double best = threshold;
      foreach (var seg in Polys.SelectMany (a => a.Segs))
         best = Math.Min (best, seg.GetDist (pt, best));
      if (best < threshold) { threshold = best; return true; }
      return false;
   }

   #region Implementation and Private stuff --------------------------
   public ImmutableArray<Poly> Polys => Render ();
   ImmutableArray<Poly> Render () {
      if (_Polys != null) return _Polys;
      List<Poly> polys = [];
      var font = LineFont.Get (Style.Font);
      font.Render (Text, Pt, Alignment, Oblique, XScale, Height, Angle, polys);
      _DYLine = font.VAdvance * Height / font.Ascender;
      return _Polys = [.. polys];
   }
   ImmutableArray<Poly> _Polys;
   #endregion
}
#endregion
