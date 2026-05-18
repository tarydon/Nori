// ────── ╔╗
// ╔═╦╦═╦╦╬╣ PolyBuild.cs
// ║║║║╬║╔╣║ Implements the PolyBuilder class (analogous to StringBuilder for Poly objects)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Buffers;
using static System.Math;
using static Nori.Lib;
namespace Nori;

#region class PolyBuilder --------------------------------------------------------------------------
/// <summary>Helper used to build Poly objects (since they are immutable once created)</summary>
public class PolyBuilder {
   // Methods ------------------------------------------------------------------
   /// <summary>Adds a slice of the Poly</summary>
   public PolyBuilder AddSlice (Poly poly, int a, double aLie, int b, double bLie, bool addLast) {
      int n = poly.Count;
      for (; ; ) {
         var seg = poly[a];
         Point2 pt = aLie == 0 ? seg.A : seg.GetPointAt (aLie);
         if (seg.IsArc) Arc (pt, seg.Center, seg.Flags & ~Poly.EFlags.Circle);
         else Line (pt);
         if (a == b) {
            if (addLast) Line (seg.GetPointAt (bLie));
            break;
         }
         a = (a + 1) % n; aLie = 0;
      }
      return this;
   }

   public PolyBuilder Add (Seg seg) {
      if (seg.IsLine) return Line (seg.A);
      else return Arc (seg.A, seg.Center, seg.Flags & ~Poly.EFlags.Circle);
   }

   /// <summary>Adds an Arc starting at the given point a and with center cen</summary>
   public PolyBuilder Arc (Point2 a, Point2 cen, Poly.EFlags flags) {
      PopBulge (a);
      while (mExtra.Count < mPts.Count) mExtra.Add (new ());
      mPts.Add (a); mExtra.Add (new (cen, flags));
      return this;
   }
   /// <summary>Adds an arc given starting point and center</summary>
   public PolyBuilder Arc (double x, double y, double xc, double yc, Poly.EFlags flags)
      => Arc (new (x, y), new (xc, yc), flags);

   /// <summary>Adds an arc given the starting point and DXF-style bulge</summary>
   public PolyBuilder Arc (Point2 a, double bulge) {
      PopBulge (a); mPts.Add (a); mBulge = bulge;
      return this;
   }
   /// <summary>Adds an arc given the starting point and DXF-style bulge</summary>
   public PolyBuilder Arc (double x, double y, double bulge)
      => Arc (new (x, y), bulge);

   /// <summary>This is called finally to complete the build process to a Poly</summary>
   public Poly Build () {
      PopBulge (mPts[0]);
      Poly.EFlags flags = mClosed ? Poly.EFlags.Closed : 0;
      if (mClosed && mPts.Count > 1 && mPts[0].EQ (mPts[^1])) mPts.RemoveLast ();
      var extra = ImmutableArray<Poly.ArcInfo>.Empty;
      if (mExtra.Count > 0) {
         extra = [.. mExtra];
         flags |= Poly.EFlags.HasArcs;
         if (extra[0].Flags.HasFlag (Poly.EFlags.Circle))
            flags |= Poly.EFlags.Circle;
      }
      var poly = new Poly ([.. mPts], extra, flags);
      Reset ();
      return poly;
   }

   /// <summary>This constructor makes a Pline from a Pline mini-language encoded string</summary>
   /// See Poly.Parse for details
   internal Poly Build (string s) => Build (new UTFReader (Encoding.UTF8.GetBytes (s)));

   /// <summary>This constructor makes a Pline from a Pline mini-language encoded string</summary>
   /// See Poly.Parse for details
   internal Poly Build (UTFReader R, bool fromCurl = false) {
      var mode = 'M';
      Point2 a = Point2.Zero;
      if (R.Peek () is not (byte)'M' and not (byte)'C') throw new ParseException ("Poly should start with 'M' or 'C'");
      for (; ; ) {
         char ch = GetMode ();
         switch (ch) {
            case 'C': a = GetP (); double r = GetD (); return Poly.Circle (a, r);
            case 'M': a = GetP (); break;
            case 'L': Line (a); a = GetP (); break;
            case 'H': Line (a); a = new (GetD (), a.Y); break;
            case 'V': Line (a); a = new (a.X, GetD ()); break;
            case 'Z': Line (a); Close (); return Build ();
            case '.': Line (a); return Build ();
            case 'Q':
               var (b, q) = (GetP (), GetD ());    // q is the number of quarter-turns
               if (q.IsZero ()) {
                  Line (a); a = b;
               } else {
                  double opp = a.DistTo (b) / 2, slope = a.AngleTo (b);
                  double adj = opp / Tan (q * QuarterPI);
                  Point2 cen = a.Polar (opp, slope).Polar (adj, slope + HalfPI);
                  Arc (a, cen, q > 0 ? Poly.EFlags.CCW : Poly.EFlags.CW);
                  a = b;
               }
               break;
            default: throw new ParseException ($"Unexpected mode '{ch}' in Poly.Parse");
         }
      }

      // Helpers ...........................................
      // Read the current mode character (like M, L, V, H etc). Since repeated modes can
      // be elided, this simply returns the 'current mode' if we see a number instead
      char GetMode () {
         if (!R.TryPeek (out var b)) return '.';
         if (fromCurl && sCurlSpl.Contains (R.Peek ())) return '.';
         char ch = (char)b; if (char.IsLetter (ch)) { R.Skip (); return mode = char.ToUpper (ch); }
         return mode;
      }

      // Expecting two doubles (separated by whitespace or commas) to make a Point
      Point2 GetP () => new (GetD (), GetD ());
      // Expecting a double, prefixed possibly by whitespace
      double GetD () { R.Skip (sSpaceAndComma).Read (out double v); return v; }
   }
   static readonly SearchValues<byte> sSpaceAndComma = SearchValues.Create (" \r\n\f\t,"u8);
   static readonly SearchValues<byte> sCurlSpl = SearchValues.Create (" }]"u8);

   /// <summary>Marks the Pline as closed</summary>
   public PolyBuilder Close () { mClosed = true; return this; }

   /// <summary>Adds the given end-point as the last node, makes a Poly and returns it</summary>
   public Poly End (Point2 e) { Line (e); return Build (); }
   /// <summary>Adds the given end-point as the last node, makes a Poly and returns it</summary>
   public Poly End (double x, double y) => End (new (x, y));

   /// <summary>Add a line starting at the given point</summary>
   public PolyBuilder Line (Point2 a) { PopBulge (a); mPts.Add (a); return this; }
   /// <summary>Add a line starting at the given point</summary>
   public PolyBuilder Line (double x, double y) => Line (new (x, y));

   // Helpers ------------------------------------------------------------------
   void PopBulge (Point2 b) {
      // The bulge is the tangent of one quarter of the turn angle
      if (mBulge.IsNan) return;
      double bulge = mBulge; mBulge = double.NaN;
      if (bulge > 1e6 || bulge.IsZero ()) return;  // Only a Line

      bool ccw = bulge > 0; bulge = Abs (bulge);
      bool large = bulge > 1;
      double shift = large ? 1 / Tan (Lib.PI - Atan (bulge) * 2) : 1 / Tan (Atan (bulge) * 2);
      if (large == ccw) shift = -shift;
      Point2 a = mPts.RemoveLast ();
      double dx = (b.X - a.X) / 2, dy = (b.Y - a.Y) / 2;
      Point2 cen = new (a.X + dx - dy * shift, a.Y + dy + dx * shift);
      Arc (a, cen, ccw ? Poly.EFlags.CCW : Poly.EFlags.CW);
   }

   void Reset () {
      mPts.Clear (); mExtra.Clear (); mClosed = false; mBulge = double.NaN;
   }

   // Property -----------------------------------------------------------------
   /// <summary>Returns true if no Poly is built</summary>
   public bool IsNull => mPts.Count == 0;

   public static PolyBuilder It => sIt ??= new ();
   [ThreadStatic]
   static PolyBuilder? sIt;

   // Private data -------------------------------------------------------------
   readonly List<Point2> mPts = [];
   readonly List<Poly.ArcInfo> mExtra = [];
   double mBulge = double.NaN;
   bool mClosed;
}
#endregion
