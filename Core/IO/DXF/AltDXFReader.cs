namespace Nori.Alt;
using static EDXF;
using static DXFCore;
using System.ComponentModel.DataAnnotations;

public class DXFReader {
   // Constructors -------------------------------------------------------------
   /// <summary>Initialize a DXFReader with a byte-array containing DXF data</summary>
   public DXFReader (byte[] data) => mR = new (mD = data);
   /// <summary>Initialize a DXFReader form a file</summary>
   public DXFReader (string file) : this (Lib.ReadBytes (file)) { }

   // Methods ------------------------------------------------------------------
   /// <summary>
   /// Parse the file, build the DXF and return it
   /// </summary>
   public Dwg2 Load () {
      EDXF type;
      while ((type = NextObject ()) != EOF) {
         switch (type) {
            case > _FIRSTIGNORE and < _LASTIGNORE: break;
            case > _FIRSTSIMPLE and < _LASTSIMPLE: LoadSimple (type); break;

            case DIMENSION: LoadDimension (); break;
            case LINE: LoadLine (); break;
            case LWPOLYLINE: LoadLWPolyline (); break;
            case SECTION: LoadSection (); break;
            case SPLINE: LoadSpline (); break;
            case NIL:
               Console.ForegroundColor = ConsoleColor.Yellow;
               Console.Write ($"{S (0)} ");
               Console.ResetColor ();
               break;
            default: 
               if (sReported.Add (type)) Console.Write ($"{type} "); 
               break;
         }
      }

      LinkDimensions ();
      CurlWriter.Save (mDwg, "c:/etc/test.curl", "Testing AltDXFReader");
      DXFWriter.Save (mDwg, "c:/etc/test.dxf");
      return mDwg;
   }
   HashSet<EDXF> sReported = [];

   // Implementation -----------------------------------------------------------
   // Adds an entity into the drawing (or into the current block being constructed,
   // if there is one)
   void Add (Ent2 ent) {
      if (N (60).IsOdd ()) return;     // Entity is invisible
      ent.Color = CLR ();
      if (mBlock != null) mBlock.Add (ent);
      else mDwg.Add (ent);
   }

   // Adds multiple entities into the drawing
   void Add (IEnumerable<Ent2> ents) => ents.ForEach (Add);

   // Adds a Poly into the drawing (wrapping it up in an E2Poly entity)
   void Add (Poly poly) => Add (new E2Poly (LYR (), poly));

   // Called at the end of a LWPOLYLINE or POLYLINE entity to build a polyline
   void AddPolyline (bool closed) {
      if (mVertex.Count <= 1) return;
      // If there are any curve fit vertices here, remove the others
      if (mVertex.Any (a => (a.Flags & 8) > 0))
         mVertex = [.. mVertex.Where (a => (a.Flags & 8) > 0)];
      foreach (var (pt, flags, bulge) in mVertex) {
         if (Math.Abs (bulge) > 1e6 || bulge.IsZero ()) mPB.Line (pt);
         else mPB.Arc (pt, bulge);
      }
      if (closed) mPB.Close ();
      Add (mPB.Build ());
   }
   PolyBuilder mPB = new ();

   // Load the dimensions
   void LinkDimensions () {
      List<Block2> blocks = [];
      foreach (var (dim, name) in mDimMap)
         if (dim.LoadEnts (mDwg, name) is { } block) blocks.Add (block);
      mDwg.RemoveBlocks (blocks);
   }

   // Loads a DIMENSION entity
   void LoadDimension () {
      NextAll ();
      var dim = new E2Dimension (LYR ());
      dim.SetDimSettings (mDwg.DimSettings);
      mDimMap.Add (dim, S (2)); Add (dim);
   }

   // Loads a line - this is written as a special routine, since lines may contain multiple
   // 1000 group entries containing bend information
   void LoadLine () {
      var (ba, radius, kfactor) = (double.NaN, 0.0, 0.42);
      for (; ; ) {
         int before = mR.Pos;
         if (!Next (true)) break;
         if (G == 0) { mR.Pos = before; break; }
         if (G == 1000) {
            var s = mEncoding.GetString (mD.AsSpan (mStart, mLength)).ToUpper ();
            if (s.StartsWith ("BEND_ANGLE:")) ba = s[11..].ToDouble ().D2R ();
            else if (s.StartsWith ("BEND_RADIUS:")) radius = s[12..].ToDouble () * Scale;
            else if (s.StartsWith ("K_FACTOR:")) kfactor = s[9..].ToDouble ();
         }
      }
      var line = Poly.Line (PT (10), PT (11));
      if (ba.IsNan) Add (line);
      else Add (new E2Bendline (mDwg, line.Pts, ba, radius, kfactor));
   }

   // Handles an LWPOLYLINE entity.
   // This is handed as a special case here, since this entity has multiple repeats
   // of group 10, group 11 and group 42 codes (for X, Y, Bulge respectively). Also
   // the group 42 codes can be omitted and will default to 0
   void LoadLWPolyline () {
      mVertex.Clear ();
      double x = double.NaN, y = 0, bulge = 0; 
      for (; ; ) {
         int before = mR.Pos;
         if (!Next () || G == 0) { mR.Pos = before; break; }
         switch (G) {
            case 10:
               if (!x.IsNan) mVertex.Add ((new (x, y), 0, bulge));
               x = DLIN (10); bulge = 0;
               break;
            case 20: y = DLIN (20); break;
            case 42: bulge = D (42); break; 
         }
      }
      mVertex.Add ((new (x, y), 0, bulge));
      AddPolyline (N (70).IsOdd ());
   }
   List<(Point2 Pt, int Flags, double Bulge)> mVertex = [];

   // Load a SPLINE
   void LoadSpline () {
      var (x, flags) = (0.0, 0);
      mPts.Clear (); mKnots.Clear (); mWeights.Clear ();
      for (; ; ) {
         int before = mR.Pos;
         if (!Next () || G == 0) { mR.Pos = before; break; }
         switch (G) {
            case 10: x = DLIN (10); break;
            case 20: mPts.Add (new (x, DLIN (20))); break;
            case 40: mKnots.Add (D (40)); break;
            case 41: mWeights.Add (D (41)); break;
            case 70: flags = N (70); break;
         }
      }
      E2Flags eFlags = 0;
      if ((flags & 1) > 0) eFlags |= E2Flags.Closed;
      if ((flags & 2) > 0) eFlags |= E2Flags.Periodic;
      var spline = new Spline2 ([.. mPts], [.. mKnots], [.. mWeights]);
      Add (new E2Spline (LYR (), spline, eFlags));
   }
   List<Point2> mPts = [];
   List<double> mKnots = [], mWeights = [];

   // Handles SECTION codes, and does special processing for the HEADER section alone
   void LoadSection () {
      // We need special processing only for the HEADER section, so we do that here. 
      Next (); Check (G == 2);
      if (E (2) != HEADER) return;
      for (; ; ) {
         if (!Next () || G == 0) break;
         if (G != 9) continue;
         // Found a 9 group - header variable name
         var name = E (9);
         Next ();
         switch (name) {
            case _ACADVER: mACADVer = S (G).ToUpper (); break;
            case _CLAYER: mCurrentLayer = S (G); break;
            case _MEASUREMENT: if (!mUnitsSet) Scale = N (G) == 0 ? 25.4 : 1; break;
            case _INSUNITS:
               int n = N (G);
               if (n is 1 or 4) { Scale = n == 1 ? 25.4 : 1; mUnitsSet = true; }
               break;
            case _DWGCODEPAGE:
               if (string.CompareOrdinal (mACADVer, "AC1021") < 0) 
                  mEncoding = GetEncoding (S (G));
               break;
         }
      }
   }

   // This loads objects where key-value pairs are not repeated at all. So we can race
   // ahead and read all the values till we see the next 0 group
   void LoadSimple (EDXF type) {
      // Load all the key-value pairs until we hit the next 0 group
      NextAll ();
      switch (type) {
         case ARC: Add (Poly.Arc (PT (10), DLIN (40), DANG (50), DANG (51), true)); break;
         case BLOCK: mBlock = new Block2 (S (2), PT (10), []); break;
         case CIRCLE: Add (Poly.Circle (PT (10), DLIN (40))); break;
         case POINT: Add (new E2Point (LYR (), PT (10))); break;
         case TRACE or SOLID: Add (new E2Solid (LYR (), [PT (10), PT (11), PT (12), PT (13)])); break;

         case ENDBLK:
            // Safe to add this to mDwg since blocks cannot contain nested blocks
            if (mBlock is { } && !SkipBlocks.Contains (mBlock.Name)) mDwg.Add (mBlock);
            mBlock = null;
            break;
         case INSERT:
            string name = S (2);
            if (!SkipBlocks.Contains (name)) 
               Add (new E2Insert (mDwg, LYR (), name, PT (10), DANG (50), D (41, 1.0), D (42, 1.0)));
            break;
         case LAYER:
            bool visible = N (70).IsEven ();
            var layer = new Layer2 (S (2), CLR (), GetLType (S (6))) { IsVisible = visible };
            if (mLayerMap.TryAdd (layer.Name, layer)) mDwg.Add (layer);
            break;
         case MTEXT:
            var (nAlign, dx, dy) = (N (71), D (11), D (21));
            ETextAlign align = (ETextAlign)(nAlign >= 7 ? nAlign + 3 : nAlign);
            double angle = (dx.IsZero () && dy.IsZero ()) ? DANG (50) : Math.Atan2 (dy, dx);
            Add (MakeMText (LYR (), STYL (), S (1), PT (10), DLIN (40), angle, align, mSB));
            break;
         case STYLE:
            if (N (70).IsEven ()) {    // Otherwise, this represents a SHAPE entry
               var style = new Style2 (S (1), S (3), D (40), D (41, 1.0), DANG (50));
               if (mStyleMap.TryAdd (style.Name, style)) mDwg.Add (style);
            }
            break;
         case TEXT:
            int hAlign = N (72) switch { 1 => 1, 2 => 2, _ => 0, }, vAlign = 3 - N (73).Clamp (0, 3);
            align = (ETextAlign)(vAlign * 3 + hAlign + 1);
            Point2 pos = align == ETextAlign.BaseLeft ? PT (10) : PT (11);
            Add (new E2Text (LYR (), STYL (), CleanText (S (2), mSB), pos, DLIN (40), DANG (50), DANG (51), D (41, 1.0), align));
            break;
         default:
            throw new Exception ($"Unhandled {type} in LoadSimple");
      }
   }
   StringBuilder mSB = new ();

   // Reads one key-value pair - returns false if we are at end-of-file
   // This skips all keys above 255, except when readAll=true, in which case, it returns
   // even after reading a key above 255 (like 1000)
   bool Next (bool readAll = false) {
      for (; ; ) {
         if (mR.AtEndOfFile) return false;
         mR.Read (out G).SkipToLineEnd ();
         mR.ReadLineRange (out mStart, out mLength);
         if (G < 256) { mSt[G] = mStart; mLen[G] = mLength; return true; }
         if (readAll) return true; 
      }
   }
   int mStart, mLength;

   // Reads (and stores) all key-value pairs until we find a 0 group. This does not
   // consume the 0 group
   bool NextAll () {
      for (; ; ) {
         int before = mR.Pos;
         if (mR.AtEndOfFile) return false;
         mR.Read (out G).SkipToLineEnd ();
         if (G == 0) { mR.Pos = before; return true; }
         mR.ReadLineRange (out int st, out int len);
         if (G < 256) { mSt[G] = st; mLen[G] = len; }
      }
   }

   // Keeps reading past key-value pairs until we find a 0 group, then returns the corresponding
   // value as an EDXF enumeration. If we hit the end of the file, returns EOF. Also, as a 
   // side effect, this sets mBase to be the start index of the object
   EDXF NextObject () {
      for (; ; ) {
         if (mR.AtEndOfFile) return EOF;
         mR.Read (out G).SkipToNextLine ();
         mR.ReadLineRange (out int st, out int len);
         if (G == 0) {
            mBase = mSt[0] = st; mLen[0] = len;
            return E (0);
         }
      }
   }

   // Routines to fetch group values -------------------------------------------
   // The mSt[] and mLen[] arrays store the last read values of each of the group codes from 
   // 0..255. Suppose we ask for the value of group 41 code (which is used for Vertex.Bulge),
   // we want to return a value ONLY if we have read in a group 41 code AFTER we started reading
   // the most recent entity (VERTEX). If we read in a group-41 code for the previous vertex, but
   // the current vertex does not have a group 41 code, the previous value we latched into mSt/mLen
   // for group 41 should not be returned. To make this happen, we use the mBase index (which is
   // set to the start of each entity position when we start reading it), and discard any group
   // values whose start points are before this

   // Returns the color stored in group code 62
   Color4 CLR () => GetColor (SB (62));

   // Group value as an EDXF enum
   EDXF E (int g) => DXFCore.Dict.GetValueOrDefault (SB (g));

   // Group value G as a double
   double D (int g) => SB (g).ToDouble ();
   double D (int g, double fallback) => SB (g).ToDouble (fallback);

   // Group value G as a radian angle
   double DANG (int g) => D (g).D2R ();

   // Group value G as a linear value (scaled by current unit)
   double DLIN (int g) => D (g) * Scale;

   // Gets the layer whose name is stored in the group 8
   Layer2 LYR () => mLayerMap.GetValueOrDefault (S (8), mDwg.CurrentLayer);

   // Gets the style whose name is stored in the group 7
   Style2 STYL () => mStyleMap.GetValueOrDefault (S (7), mDwg.GetStyle ("STANDARD")!);

   // Group value G as an integer
   int N (int g) => SB (g).ToInt ();

   // Group value G,G+10 as a SCALED point
   Point2 PT (int g) => new (D (g) * Scale, D (g + 10) * Scale);

   // Group value G as a string
   string S (int g) => mEncoding.GetString (SB (g));

   // Get the group value N as a span of bytes (if this group value was not present for this entity,
   // this returns an empty (zero-length) span)
   ReadOnlySpan<byte> SB (int g) {
      int start = mSt[g]; if (start < mBase) return mD.AsSpan (0, 0);
      return mD.AsSpan (start, mLen[g]);
   }

   // Error handling routines --------------------------------------------------
   void Check (bool condition) {
      if (!condition) throw new InvalidOperationException ();
   }

   // Nested types -------------------------------------------------------------
   // Various modes for the 'Next' command
   enum EMode {
      Std,     // Standard - read one group / value code and return
      Skip,    // Read until and including the next G0 and return (0 group consumed)
      Gather   // Read until the next G0 and return - G0 not consumed, will fetch on next call
   };

   // DXF state ----------------------------------------------------------------
   string mACADVer = "AC1021";   // AutoCAD version
   string mCurrentLayer = "";    // Current layer from DXF file
   bool mUnitsSet = false;       // Have we figured out the DXF units yet?
   double Scale = 1;             // Scale factor to mm (METRIC=>1, IMPERIAL=>25.4 etc)
   Block2? mBlock;               // If non-null, this is the block we're currently reading

   // Private data -------------------------------------------------------------
   readonly byte[] mD;           // Raw data of the file
   readonly UTFReader mR;        // UTFReader used to read the file
   readonly Dwg2 mDwg = new ();  // The drawing we're constructing

   int G;                        // Group code 
   int mBase;                    // The current 'entity' (Group 0 set) data starts here
   int[] mSt = new int[256];     // For each group code, the start of the value
   int[] mLen = new int[256];    // .. and the length of that value (both in D)

   Encoding mEncoding = Encoding.UTF8;    // Encoding we're using for this file
   readonly Dictionary<string, Layer2> mLayerMap = new (StringComparer.OrdinalIgnoreCase);
   readonly Dictionary<string, Style2> mStyleMap = new (StringComparer.OrdinalIgnoreCase);
   readonly Dictionary<E2Dimension, string> mDimMap = [];
}
