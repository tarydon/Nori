// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DXFReader.cs
// ║║║║╬║╔╣║ A DXFReader that reads directly from a ReadOnlySpan<byte>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static EDXF;
using static DXFCore;

#region class DXFReader ----------------------------------------------------------------------------
/// <summary>This can read a text DXF file and convert it to a Dwg2</summary>
/// We don't load the data into a string or array of strings, but rather uses the byte-array
/// directly. It uses the UTFReader class to parse integers, doubles etc directly from spans of
/// bytes, and is therefore rougly about twice as fast as the Flux DXF reader (which uses strings)
public class DXFReader {
   // Constructors -------------------------------------------------------------
   /// <summary>Initialize a DXFReader with a byte-array containing DXF data</summary>
   public DXFReader (byte[] data) => mR = new (mD = data);
   /// <summary>Initialize a DXFReader form a file</summary>
   public DXFReader (string file) : this (Lib.ReadBytes (file)) => mFilename = file;
   string? mFilename;

   // Properties ---------------------------------------------------------------
   /// <summary>Darken all layer colors (to have a luminance of no more than 160)</summary>
   public bool DarkenColors;

   /// <summary>If set, all dimension entities are moved to the "Dimension" layer</summary>
   public bool RelayerDimensions;

   /// <summary>If set above zero, then polylines that touch are stitched together</summary>
   /// Two open polylines whose endpoints are closer than this threshold are
   /// joined together (as long as they are on the same layer). If there are open
   /// polylines whose start and end are within this threshold of each other they
   /// are closed.
   public double StitchThreshold;

   /// <summary>Convert white entities to black on import</summary>
   public bool WhiteToBlack;

   // Methods ------------------------------------------------------------------
   /// <summary>Parse the file, build the DXF and return it</summary>
   public Dwg2 Load () {
      EDXF type;
      while ((type = NextObject ()) != EOF) {
         switch (type) {
            // Entities we know, but choose to ignore
            case > __FIRSTIGNORE and < __LASTTODO: break;
            // Entities that can be loaded by LoadSimple (read-all-groups-ahead strategy)
            case > __FIRSTSIMPLE and < __LASTSIMPLE: LoadSimple (type); break;

            // These entities require more complex handling, and we need a separate routine for each
            case DIMENSION: LoadDimension (); break;
            case ELLIPSE: LoadEllipse (); break;
            case LINE: LoadLine (); break;
            case LWPOLYLINE: LoadLWPolyline (); break;
            case POLYLINE: LoadPolyline (); break;
            case SECTION: LoadSection (); break;
            case SPLINE: LoadSpline (); break;

             // Catch other cases
            case NIL: UnknownEnt (S (0)); break;
            default: if (sReported.Add (type)) Lib.Trace ($"Unsupported {type} at line {LineNo ()}"); break;
         }
      }

      LinkDimensions ();
      ProcessBendText (mDwg);
      StitchDrawing ();
      SetCurrent ();
      return mDwg;
   }
   HashSet<EDXF> sReported = [];

   /// <summary>Basic interface function to load a DXF with default values</summary>
   public static Dwg2 Load (string file) => new DXFReader (file).Load ();

   // Implementation -----------------------------------------------------------
   // Adds an entity into the drawing (or into the current block being constructed,
   // if there is one)
   void Add (Ent2 ent) {
      if (N (60).IsOdd ()) return;     // Entity is invisible
      if (mBlock != null) { ent.InBlock = true; mBlock.Add (ent); }
      else mDwg.Add (ent);
   }

   // Adds multiple entities into the drawing
   void Add (IEnumerable<Ent2> ents) => ents.ForEach (Add);

   // Adds a Poly into the drawing (wrapping it up in an E2Poly entity)
   void Add (Poly poly) => Add (new E2Poly (LYR (), poly));

   // Called at the end of a LWPOLYLINE or POLYLINE entity to build a polyline
   void AddPolyline (Layer2 layer, bool closed) {
      if (mVertex.Count <= 1) return;
      // If there are any curve fit vertices here, remove the others
      if (mVertex.Any (a => (a.Flags & 8) > 0))
         mVertex = [.. mVertex.Where (a => (a.Flags & 8) > 0)];
      foreach (var (pt, flags, bulge) in mVertex) {
         if (Math.Abs (bulge) > 1e6 || bulge.IsZero ()) mPB.Line (pt);
         else mPB.Arc (pt, bulge);
      }
      if (closed) mPB.Close ();
      Add (new E2Poly (layer, mPB.Build ()));
   }
   PolyBuilder mPB = new ();

   // The current line number
   int LineNo () => mD.Take (mR.Pos).Count (a => a == '\n');

   // Load the dimensions
   void LinkDimensions () {
      Layer2? layer = null;
      List<Block2> blocks = [];
      if (RelayerDimensions)
         mDwg.Add (layer = new Layer2 ("Dimension", Color4.Blue, ELineType.Continuous));
      foreach (var (dim, name) in mDimMap) {
         if (dim.LoadEnts (mDwg, name) is { } block) {
            if (layer != null) dim.Layer = layer;
            blocks.Add (block);
         }
      }
      mDwg.RemoveBlocks (blocks);
   }

   // Loads a DIMENSION entity
   void LoadDimension () {
      NextAll ();
      EDim kind = (EDim)(N (70) & 7);
      mPts.Clear (); 
      var (layer, style, text) = (LYR (), mDwg.GetDimStyle (S (3)), S (1));

      E2Dim? dim = kind switch {
         EDim.Angular3P => new E2Dim3PAngular (layer, style, Add5 (15, 13, 14, 10, 11), text),
         _ => new E2DimGeneric (layer, style, mPts, text),
      };
      mDimMap.Add (dim, S (2)); Add (dim);

      // Helpers ...........................................
      List<Point2> Add1 (int a) { mPts.Add (PT (a)); return mPts; }
      List<Point2> Add2 (int a, int b) { Add1 (a); return Add1 (b); }
      List<Point2> Add5 (int a, int b, int c, int d, int e) { Add2 (a, b); Add2 (c, d); return Add1 (e); }
   }

   // Loads an ELLIPSE entity
   void LoadEllipse () {
      NextAll (); 
      var (cen, major, ratio, aStart, aEnd) = (PT (10), (Vector2)PT (11), D (40), D (41), D (42));
      var (east, zDir) = (cen + major, D (230) < -0.999 ? -1.0 : 1.0);
      while (aEnd < aStart) aEnd += Lib.TwoPI;
      double R = major.Length, r = R * ratio, aSpan = aEnd - aStart;

      // Figure out the number of steps for discretization (5 degrees per step)
      int c = (int)Math.Max (4.0, (aSpan / (Math.PI / 36)).Round (0));
      double aStep = aSpan / c, rot = cen.AngleTo (east);
      bool closed = aSpan.EQ (2 * Math.PI);
      if (closed) c--;
      for (int i = 0; i <= c; i++) {
         var (sin, cos) = Math.SinCos (aStart + i * aStep);
         var node = new Point2 (R * cos, r * sin * zDir).Rotated (rot);
         mPB.Line (node.Moved (cen.X, cen.Y));
      }
      if (closed) mPB.Close ();
      Add (mPB.Build ());
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
      AddPolyline (LYR (), N (70).IsOdd ());
   }
   List<(Point2 Pt, int Flags, double Bulge)> mVertex = [];

   // Handles a POLYLINE entity.
   // This is handled as a special case here, since a POLYLINE is created with multiple VERTEX
   // entities finally followed by a SEQEND
   void LoadPolyline () {
      NextAll ();
      var (layer, closed) = (LYR (), N (70).IsOdd ());
      mVertex.Clear ();
      for (; ; ) {
         var e = NextObject (); NextAll ();
         if (e == SEQEND) break;
         if (e == VERTEX) mVertex.Add ((PT (10), N (70), D (42)));
      }
      AddPolyline (layer, closed);
   }

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
      if (mPts.Count == 0) return;
      var spline = new Spline2 ([.. mPts], [.. mKnots], [.. mWeights]);
      Add (new E2Spline (LYR (), spline, eFlags));
   }
   List<Point2> mPts = [];
   List<double> mKnots = [], mWeights = [];

   // Handles SECTION codes, and does special processing for the HEADER section alone
   void LoadSection () {
      // We need special processing only for the HEADER section, so we do that here. 
      Next ();
      EDXF name = E (2);
      if (name is BLOCKS or ENTITIES) {
         // Starting entities section
         mDefPoints ??= mDwg.Layers.FirstOrDefault (a => a.Name.EqIC ("DEFPOINTS"));
      }
      if (name != HEADER) return;

      for (; ; ) {
         if (!Next () || G == 0) break;
         if (G != 9) continue;
         // Found a 9 group - header variable name
         name = E (9);
         Next ();
         switch (name) {
            case _ACADVER: mACADVer = S (G).ToUpper (); break;
            case _CLAYER: mCurrentLayer = S (G); break;
            case _DIMSTYLE: mCurrentDimStyle = S (G); break;
            case _DIMASZ: mDimASZ = F (G); break; case _DIMEXE: mDimEXE = F (G); break;
            case _DIMEXO: mDimEXO = F (G); break; case _DIMTXT: mDimTXT = F (G); break;
            case _DIMCEN: mDimCEN = F (G); break; case _DIMGAP: mDimGAP = F (G); break;
            case _DIMTIH: mDimTIH = N (G); break; case _DIMTOH: mDimTOH = N (G); break;
            case _DIMTAD: mDimTAD = N (G); break; case _DIMDEC: mDimDEC = N (G); break;
            case _DIMADEC: mDimADEC = N (G); break; case _DIMTOFL: mDimTOFL = N (G); break;
            case _DIMSCALE: mDimScale = F (G); break;
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
      // Here, scale all the header variables that need to be scaled
      float f = (float)Scale;
      mDimASZ *= f; mDimEXO *= f; mDimEXE *= f; mDimTXT *= f; mDimCEN *= f; mDimGAP *= f;
   }
   // Various defaults for DimStyle2
   int mDimTIH = 0, mDimTOH = 0, mDimTOFL = 1, mDimTAD = 1, mDimDEC = 2, mDimADEC = 1;
   float mDimASZ = 2.5f, mDimEXO = 0.625f, mDimEXE = 1.25f, mDimTXT = 2.5f, mDimCEN = 2.5f;
   float mDimScale = 1, mDimGAP = 0.625f;
   Layer2? mDefPoints;

   // This loads objects where key-value pairs are not repeated at all. So we can race
   // ahead and read all the values till we see the next 0 group
   void LoadSimple (EDXF type) {
      // Load all the key-value pairs until we hit the next 0 group
      NextAll ();
      switch (type) {
         case ARC: Add (Poly.Arc (PT (10), DLIN (40), DANG (50), DANG (51), true)); break;
         case BLOCK: mBlock = new Block2 (S (2), PT (10), []); break;
         case CIRCLE: Add (Poly.Circle (PTZ (10), DLIN (40))); break;
         case TRACE or SOLID: Add (new E2Solid (LYR (), [PT (10), PT (11), PT (12), PT (13)])); break;

         case ATTRIB:
            if (N (70).IsOdd ()) break;
            goto case TEXT;
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
         case POINT:
            layer = LYR (); if (layer == mDefPoints) return;
            Add (new E2Point (layer, PT (10)));
            break;
         case STYLE:
            if (N (70).IsEven ()) {    // Otherwise, this represents a SHAPE entry
               double xScale = D (41); if (xScale.IsZero ()) xScale = 1; 
               var style = new Style2 (S (2), S (3), DLIN (40), xScale, DANG (50));
               if (mStyleMap.TryAdd (style.Name, style)) mDwg.Add (style);
            }
            break;
         case TEXT:
            int hAlign = N (72) switch { 1 => 1, 2 => 2, _ => 0, }, vAlign = 3 - N (73).Clamp (0, 3);
            align = (ETextAlign)(vAlign * 3 + hAlign + 1);
            Point2 pos = align == ETextAlign.BaseLeft ? PT (10) : PT (11);
            Add (new E2Text (LYR (), STYL (), CleanText (S (1), mSB), pos, DLIN (40), DANG (50), DANG (51), D (41, 1.0), align));
            break;
         case DIMSTYLE:
            mDwg.Add (new DimStyle2 (S (2), F (40, mDimScale), F (41, mDimASZ), F (42, mDimEXO), 
               F (44, mDimEXE), F (140, mDimTXT), F (141, mDimCEN), F (147, mDimGAP), N (73, mDimTIH) > 0, 
               N (74, mDimTOH) > 0, N (172, mDimTOFL) > 0, N (77, mDimTAD), N (271, mDimDEC), N (179, mDimADEC),
               STYL ())); 
            break;
         default:
            throw new NoriCodeException ($"Unhandled {type}");
      }
   }
   StringBuilder mSB = new ();

   // Reads one key-value pair - returns false if we are at end-of-file
   // This skips all keys above 272, except when readAll=true, in which case, it returns
   // even after reading a key above 272 (like 1000)
   bool Next (bool readAll = false) {
      for (; ; ) {
         if (mR.AtEndOfFile) return false;
         mR.Read (out G).SkipToLineEnd ();
         mR.ReadLineRange (out mStart, out mLength);
         if (G < 277) { mSt[G] = mStart; mLen[G] = mLength; return true; }
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
         if (G < 272) { mSt[G] = st; mLen[G] = len; }
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

   // Sets up the current layer, dimension style etc
   void SetCurrent () {
      if (mDwg.Layers.FirstOrDefault (a => a.Name == mCurrentLayer) is { } layer)
         mDwg.CurrentLayer = layer;
      if (mDwg.DimStyles.FirstOrDefault (a => a.Name == mCurrentDimStyle) is { } dimstyle)
         mDwg.CurrentDimStyle = dimstyle;
      mDwg.Filename = mFilename;
   }

   // Stitches the drawing if we want
   void StitchDrawing () {
      if (StitchThreshold <= 0) return;
      if (StitchThreshold > 0.009) new DwgStitcher (mDwg, 0.0001).Process ();
      new DwgStitcher (mDwg, StitchThreshold).Process ();
   }

   // Routines to fetch group values -------------------------------------------
   // The mSt[] and mLen[] arrays store the last read values of each of the group codes from 
   // 0..272. Suppose we ask for the value of group 41 code (which is used for Vertex.Bulge),
   // we want to return a value ONLY if we have read in a group 41 code AFTER we started reading
   // the most recent entity (VERTEX). If we read in a group-41 code for the previous vertex, but
   // the current vertex does not have a group 41 code, the previous value we latched into mSt/mLen
   // for group 41 should not be returned. To make this happen, we use the mBase index (which is
   // set to the start of each entity position when we start reading it), and discard any group
   // values whose start points are before this

   // Returns the color stored in group code 62.
   // The 'DarkenColors' and 'WhiteToBlack' settings also affect the color
   Color4 CLR () {
      var color = GetColor (SB (62));
      if (WhiteToBlack && color.EQ (Color4.White)) color = Color4.Black;
      if (DarkenColors) color = color.Darkened ();
      return color;
   }

   // Group value as an EDXF enum
   EDXF E (int g) => Dict.GetValueOrDefault (SB (g));

   // Group value G as a double
   double D (int g) => SB (g).ToDouble ();
   double D (int g, double fallback) => SB (g).ToDouble (fallback);

   // Group value G as a radian angle
   double DANG (int g) => D (g).D2R ();

   // Group value G as a linear value (scaled by current unit)
   double DLIN (int g) => D (g) * Scale;

   DimStyle2 DSTYL () => mDwg.GetDimStyle (S (3));

   // Group value G as a float
   float F (int g) => SB (g).ToFloat ();
   float F (int g, float fallback) => SB (g).ToFloat (fallback);

   // Gets the layer whose name is stored in the group 8
   Layer2 LYR () => mLayerMap.GetValueOrDefault (S (8)) ?? mDwg.CurrentLayer;

   // Gets the style whose name is stored in the group 7
   Style2 STYL () => mStyleMap.GetValueOrDefault (S (7)) ?? mDwg.GetStyle ("STANDARD")!;

   // Group value G as an integer
   int N (int g) => SB (g).ToInt ();
   int N (int g, int fallback) => SB (g).ToInt (fallback);

   // Group value G,G+10 as a SCALED point
   Point2 PT (int g) => new (D (g) * Scale, D (g + 10) * Scale);
   // Group value G,G+10 as a SCALED, possibility reflected point (if ZDir == -1)
   Point2 PTZ (int g) => new (D (g) * Scale * (D (230) < -0.5 ? -1 : 1), D (g + 10) * Scale);

   // Group value G as a string
   string S (int g) => mEncoding.GetString (SB (g));

   // Get the group value N as a span of bytes (if this group value was not present for this entity,
   // this returns an empty (zero-length) span)
   ReadOnlySpan<byte> SB (int g) {
      int start = mSt[g]; if (start < mBase) return mD.AsSpan (0, 0);
      return mD.AsSpan (start, mLen[g]);
   }

   // DXF state ----------------------------------------------------------------
   string mACADVer = "AC1021";   // AutoCAD version
   string mCurrentLayer = "";    // Current layer from DXF file
   string mCurrentDimStyle = ""; // Current dimension style from DXF file
   bool mUnitsSet = false;       // Have we figured out the DXF units yet?
   double Scale = 1;             // Scale factor to mm (METRIC=>1, IMPERIAL=>25.4 etc)
   Block2? mBlock;               // If non-null, this is the block we're currently reading

   // Private data -------------------------------------------------------------
   readonly byte[] mD;           // Raw data of the file
   readonly UTFReader mR;        // UTFReader used to read the file
   readonly Dwg2 mDwg = new ();  // The drawing we're constructing

   int G;                        // Group code 
   int mBase;                    // The current 'entity' (Group 0 set) data starts here
   int[] mSt = new int[272];     // For each group code, the start of the value
   int[] mLen = new int[272];    // .. and the length of that value (both in D)

   Encoding mEncoding = Encoding.UTF8;    // Encoding we're using for this file
   readonly Dictionary<string, Layer2> mLayerMap = new (StringComparer.OrdinalIgnoreCase);
   readonly Dictionary<string, Style2> mStyleMap = new (StringComparer.OrdinalIgnoreCase);
   readonly Dictionary<E2Dim, string> mDimMap = [];
}
#endregion
