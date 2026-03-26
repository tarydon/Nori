// ────── ╔╗
// ╔═╦╦═╦╦╬╣ AltDXFReader.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Buffers;
using Nori.Internal;
namespace Nori.Alt;
using static EDXF;

public class DXFReader {
   // Constructors -------------------------------------------------------------
   /// <summary>Initialize a DXFReader with a byte-array containing DXF data</summary>
   public DXFReader (byte[] data) => R = new (D = data);
   /// <summary>Initialize a DXFReader form a file</summary>
   public DXFReader (string file) : this (Lib.ReadBytes (file)) { }

   // Properties ---------------------------------------------------------------
   /// <summary>The Standard 256 ACAD colors</summary>
   public static Color4[] ACADColors
      => sACADColors ??= [..Lib.ReadLines("nori:DXF/color.txt")
                          .Select(a => new Color4(uint.Parse(a, NumberStyles.HexNumber) | 0xff000000))];
   static Color4[]? sACADColors;

   /// <summary>Darken all layer colors (to have a luminance of no more than 160)</summary>
   public bool DarkenColors;

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
      // In general, we have a 'backing variable' for each group code, like
      // 1=>Text, 62=>ColorNo, 2=>Name, 10=>X0 etc
      // However, there are a few entities (like LWPOLYLINE, HATCH, LEADER) where some group codes
      // like 10,20,42 are repeated multiple times in the entity. We have to recognize and process
      // these separately. For now, these are added to a list when we are processing the LWPOLYLINE
      // entity. Later, when we support HATCH, LEADER etc, we will extend this mechanism suitably.
      while (Next ()) {
         switch (G) {
            case 0: Type = E; break;
            case 1: Text = V; break;
            case 3: FontName = V; break;
            case 6: LTName = V; break;
            case 7: StyleName = V; break;
            case 8: if (mClosedPoly != null) LayerName = V; break;
            case 9: HeaderVar (E); break;
            case 10: X0 = Vf; break; case 20: Y0 = Vf; break;
            case 11: X1 = Vf; break; case 21: Y1 = Vf; break;
            case 12: X2 = Vf; break; case 22: Y2 = Vf; break;
            case 13: X3 = Vf; break; case 23: Y3 = Vf; break;
            case 14: X4 = Vf; break; case 24: Y4 = Vf; break;
            case 15: X5 = Vf; break; case 25: Y5 = Vf; break;
            case 16: X6 = Vf; break; 
            case 26: Y6 = Vf; break;
            case 40: D0 = Vf; break; case 41: D1 = Vf; break; case 42: D2 = Vf; break;
            case 43: D3 = Vf; break; case 44: D4 = Vf; break; case 45: D5 = Vf; break;
            case 46: D6 = Vf; break; case 47: D7 = Vf; break; case 48: D8 = Vf; break; 
            case 50: Ang0 = Vf; break; case 51: Ang1 = Vf; break;
            case 60: Invisible = Vn == 1; break;
            case 70: I0 = Vn; break; case 71: I1 = Vn; break; case 72: I2 = Vn; break; 
            case 73: I3 = Vn; break; case 74: I4 = Vn; break; case 77: I7 = Vn; break;
            case 62: ColorNo = E switch { BYLAYER => 256, BYBLOCK => 257, _ => Vn }; break;
            case 67: PaperSpace = Vn > 0; break;
            case 141: DimCen = Vf; break;
            case 147: DimGap = Vf; break;
            case 230: ZFlip = Vf < -0.999; break;
            case 1000: if (mType == LINE) mXData.Add (V); break;

            case 2:
               switch (mType) {
                  case SECTION: HandleSection (E); break;
                  case TABLE: HandleTable (E); break;
                  case LTYPE or LAYER or BLOCK or STYLE or DIMSTYLE or DIMENSION or INSERT: Name = V; break;
                  case ATTDEF or ATTRIB: Name = V; break;
                  default: Fatal ($"Unexpected 2 code (mType = {mType})"); break;
               }
               break;

            default: UnhandledGroup (G); break;
         }
      }
      return mDwg;
   }

   // Implementation -----------------------------------------------------------
   static DXFReader () {
      Encoding.RegisterProvider (CodePagesEncodingProvider.Instance);
      sTypeIgnore = [.. Lib.ReadLines ("nori:DXF/entity-ignore.txt").Select (Enum.Parse<EDXF>)];
   }

   // Adds a Poly to the current layer
   void Add (Poly poly) => Add (new E2Poly (Layer, poly));

   // Add an entity into the drawing
   void Add (Ent2 ent) {
      ent.Color = GetColor (); 
      if (Invisible) return;
      if (mBlockEnts != null) { ent.InBlock = true; mBlockEnts.Add (ent); } 
      else mDwg.Add (ent);
   }

   // Adds multiple entities into the drawing
   void Add (IEnumerable<Ent2> ents) => ents.ForEach (Add);

   // Builds an entity
   void BuildEnt (EDXF type) {
      if (type > _LASTENT) return;
      switch (type) {
         case BLOCK: (mBlockEnts, mBlockName, mBlockPt) = ([], Name, Pt0); break;
         case STYLE: mDwg.Add (new Style2 (Name, FontName, Height, XScale, Angle)); break;
         case SOLID or TRACE: Add (new E2Solid (Layer, [Pt0, Pt1, Pt2, Pt3])); break;

         case LAYER:
            bool visible = (Flags & 1) == 0; 
            var layer = new Layer2 (Name, GetColor (), GetLType ()) { IsVisible = visible };
            if (mLayers.TryAdd (Name, layer)) mDwg.Add (layer);
            break;

         case LINE:
            var line = Poly.Line (Pt0, Pt1); 
            // Try to find bend info among mXData entries. If we do, we create an E2Bendline
            // object. Otherwise, we create a single-segment line poly
            var (ba, radius, kfactor) = (double.NaN, 0.0, 0.42);
            foreach (var s in mXData) {
               if (s.StartsWith ("BEND_ANGLE:")) ba = s[11..].ToDouble ().D2R ();
               else if (s.StartsWith ("BEND_RADIUS:")) radius = s[12..].ToDouble () * Scale;
               else if (s.StartsWith ("K_FACTOR:")) kfactor = s[9..].ToDouble ();
            }
            if (ba.IsNan) Add (line);
            else {
               Add (new E2Bendline (mDwg, line.Pts, ba, radius, kfactor));
               mXData.Clear ();
            }
            break;

         case MTEXT:
            ETextAlign align = (ETextAlign)(I1 >= 7 ? I1 + 3 : I1);
            double angle = (X1 == 0 && Y1 == 0) ? Ang0 : Math.Atan2 (Y1, X1);
            Add (MakeMText (Layer, Style, Text, Pt0, Height, angle, align));
            break;

         //case ARC or CIRCLE: break;
         //case POINT or LWPOLYLINE or TEXT or DIMENSION or INSERT or SPLINE or POLYLINE: break;
         //case VERTEX or SEQEND or ATTDEF or ATTRIB or LEADER or TRACE or ELLIPSE or XLINE: break;
         default: Fatal ($"Unhandled entity {type}"); break;
      } // TODO: Handle HATCH
   }

   // Parses the encoded characters in the text to the corresponding special characters
   static string Clean (string text, StringBuilder? sb = null) {
      if (!text.Contains ("%%")) return text;
      (sb ??= new ()).Clear ();
      int len = text.Length, i = 0;
      while (i < len) {
         char ch = text[i++];
         if (ch == '%' && len > i + 1 && text[i] == '%') {
            switch (text[i + 1]) {
               case 'd' or 'D': ch = (char)0xB0; i += 2; break;
               case 'p' or 'P': ch = (char)0xB1; i += 2; break;
               case 'c' or 'C': ch = (char)0x2205; i += 2; break;
            }
         }
         sb.Append (ch);
      }
      return sb.ToString ();
   }

   // Converts the current AutoCAD color to a Color4
   Color4 GetColor () {
      if (ColorNo == 256) return Color4.Nil;
      var color = ACADColors[ColorNo.Clamp (0, 255)];
      if (WhiteToBlack && color.EQ (Color4.White)) color = Color4.Black;
      if (DarkenColors) color = color.Darkened ();
      return color;
   }

   // Converts the current linetype name to an ELType enumeration
   ELineType GetLType () => sLineTypes.GetValueOrDefault (LTName, ELineType.Continuous);
   static Dictionary<string, ELineType> sLineTypes = new (StringComparer.OrdinalIgnoreCase) {
      ["DOT"] = ELineType.Dot, ["DOTTED"] = ELineType.Dot, ["DASH"] = ELineType.Dash, 
      ["DASHED"] = ELineType.Dash, ["DASHDOT"] = ELineType.DashDot, ["DIVIDE"] = ELineType.DashDotDot,
      ["DASHDOTDOT"] = ELineType.DashDotDot, ["DASH2DOT"] = ELineType.DashDotDot, 
      ["CENTER"] = ELineType.Center, ["BORDER"] = ELineType.Border, ["HIDDEN"] = ELineType.Hidden,
      ["DASH2"] = ELineType.Dash2, ["PHANTOM"] = ELineType.Phantom
   };

   // Called when we read a header variable
   void HeaderVar (EDXF key) {
      if (key == NIL) { UnknownHeaderVar (V); Next (); return; }
      Next (); 
      switch (key) {
         case _ACADVER: mACADVer = V; break;
         case _CLAYER: mCurrentLayer = V; break;
         case _LTSCALE: mLTScale = Vf; break;
         case _MEASUREMENT: Scale = Vn == 0 ? 25.4 : 1; break;

         case _DWGCODEPAGE:
            mCodePage = V;
            if (string.CompareOrdinal (mACADVer, "AC1021") < 0) {
               if (!mEncodingsRegistered) {
                  mEncodingsRegistered = true; 
                  Encoding.RegisterProvider (CodePagesEncodingProvider.Instance); 
               }
               mEncoding = Encoding.GetEncoding (GetCodePage (mCodePage)) ?? Encoding.UTF8;
            }
            break;
         case _EXTMIN: case _EXTMAX: 
            double x = Vf; Next (); double y = Vf; Next (); mExtent += new Point2 (x, y); 
            break;

         default: throw new BadCaseException (key);
      }

      // helper ............................................
      static int GetCodePage (string name) {
         if (name == "dos932") return 932;
         if (int.TryParse (name.Split ('_').Last (), out int n)) return n;
         return 1252;
      }
   }
   static bool mEncodingsRegistered;
   string mACADVer = "", mCodePage = "", mCurrentLayer = "";
   Bound2 mExtent = new ();
   double mLTScale = 1, Scale = 1; 

   // This is called at the start of each section. 
   // For the HEADER section, this will read in a few variables. For other sections that we 
   // don't care about, we will simply skip the section (by running forward to the next ENDSEC)
   void HandleSection (EDXF s) {
      if (!sHandle.Contains (s)) {
         if (!sIgnore.Contains (s)) Fatal ($"Unhandled SECTION: {V}");
         while (Next ()) { if (G == 0 && E == ENDSEC) break; }
      } 
   }
   // These are the stuff we handle, and the stuff we knowingly ignore
   static readonly HashSet<EDXF> 
      sHandle = [HEADER, TABLES, BLOCKS, ENTITIES, LTYPE, LAYER, STYLE, DIMSTYLE],
      sIgnore = [CLASSES, VPORT, UCS, APPID, VIEW, BLOCK_RECORD, OBJECTS, ACDSDATA, THUMBNAILIMAGE, 
                 DWGMGR, LAYERS];

   // This is called at the start of each table
   void HandleTable (EDXF t) {
      if (!sHandle.Contains (t)) {
         if (!sIgnore.Contains (t)) Fatal ($"Unhandled TABLE: {V}");
         while (Next ()) { if (G == 0 && E == ENDTAB) break; }
      } 
   }

   // Extracts text from the encoded MTEXT string and returns the corresponding E2Text entities.
   IEnumerable<E2Text> MakeMText (Layer2 layer, Style2 style, string text, Point2 pos, double height, double angle, ETextAlign align) {
      var matches = sRxMText.Matches (text);
      if (matches.Count > 0) {
         // First, use the RegEx below to parse and replace various escape sequences in the string
         mSB.Clear ();
         int last = 0; var tspan = text.AsSpan ();
         foreach (Match M in matches) {
            // We found a match. First, copy all the characters preceding the match
            // into the target
            if (M.Index > last) Append2 (tspan[last..M.Index]);
            if (M.Groups.TryGetValue ("fract", out var fract) && fract.ValueSpan.Length > 0) {
               // No special fraction rendering is supported. Just concatenate using the division '/' symbol.
               Append (fract.Value.Replace ("^", "/").Replace ("#", "/"));
            }
            if (M.Groups.TryGetValue ("hex4", out var hex4) && hex4.ValueSpan.Length > 0) {
               // Replace the 4 hex digits with the corresponding unicode character
               int uni = int.Parse (hex4.Value, NumberStyles.HexNumber);
               Append (((char)uni).ToString ());
            }            
            last = M.Index + M.Length;
         }
         if (last < text.Length) Append2 (tspan[last..]);
         text = mSB.ToString ();

         // Helpers ........................................
         void Append (string text1) => mSB.Append (text1);
         void Append2 (ReadOnlySpan<char> text2) => mSB.Append (text2);
      }

      // Now cleanup the raw-string by removing code-blocks ({...}) and split them into multiple
      // lines by the line-break (\P). Then, output a text entity for each line
      double dyLine = 0;
      string[] lines = text.Replace ("{", "").Replace ("}", "").Split ("\\P");
      var mat = Matrix2.Rotation (pos, angle);
      foreach (var line in lines) {
         var pt = pos;
         if (!dyLine.IsZero ()) {
            pt = new (pt.X, pt.Y - dyLine);
            if (!angle.IsZero ()) pt *= mat;
         }
         var ent = new E2Text (layer, style, Clean (line, mSB), pt, height, angle, style.Oblique, style.XScale, align);
         dyLine += ent.DYLine;
         yield return ent;
      }
   }
   // The RegEx used to parse various escape-sequences in MText (See Doc\MText-Codes.pdf for a reference).
   // A MTEXT text entity can specify inline text styles and formatting. The Regex below identifies
   // the format strings and extracts the raw-text out of them. Multiple patterns are supported, and
   // each is on a separate line. These patterns are combined using the OR operator. The paragraph
   // markers (\P) and the style code-blocks ({...}) are left unidentified and are
   // processed after the text extraction.
   [SuppressMessage ("Performance", "SYSLIB1045")]
   static readonly Regex sRxMText = new (
     @"(\\[Ff][^|;]+((\|([bicp])\d+)+)?;)|" +   // Font name & style (e.g., \fTimes New Roman|b1|i0;)
     @"(\\[AHWCT](\d*?(\.\d+)?x?));|" +         // Height, Width, Alignment, Color codes like: \H3x; \H12.500; \W0.8x;
     @"(\\[LlOoKk])|" +                         // Underline, Overstrike, Strikethrough: \L \l \O \K
     @"\\U\+(?<hex4>[0-9A-Fa-f]{4})|" +         // Match 4 hex digits prefixed with \U+
     @"(\\S(?<fract>[^;]+[#/\^][^;]+);)",       // Stacking fractions like: \S+0.8^+0.1; \S+0.8#+0.1;
     RegexOptions.Compiled);

   // Reads the next group into mGroup and the value into mValue
   bool Next () {
      for (; ; ) {
         if (R.AtEndOfFile) return false;
         R.Read (out G).SkipToNextLine ();      // Read the group code
         R.ReadLineRange (out mSt, out mLen);   // Read the value
         if (mLen == 3 && D[mSt] == 'E' && D[mSt + 1] == 'O' && D[mSt + 2] == 'F') return false;
         if (G == 0 || !mSkipForward) { mSkipForward = false; return true; }
      }
   }
   bool mSkipForward;

   // This is written to each time we see a 0 group, and effectively this ends up 'making' a
   // new object of that type. However, since a type descriptor (0 group) is _followed_ by the 
   // paramters requried for that object, we cannot actually make an object when we see a 0 group.
   // Instead, we make the _previous_ object that we saw, and that is why this is a big switch
   // statement on *mType* which is the _previous_ type we read. All the parameters we would need
   // to build that previous object have been read already and are now latched into state
   // variables like D, Color, N, LTypeName etc. 
   EDXF Type {
      set {
         // First, construct the previous entity, if it is not a skipped entity         
         if (mType != SKIPPEDENT) BuildEnt (mType);

         // Now that we've constructed the previous object, we can store the incoming Type
         // value to start preparing for the next
         if ((mType = value) == NIL) Fatal ($"Unknown: {V}");

         // If the entity we are just starting is one that we are not going to actually process,
         // then we set the mType to SKIPPEDENT and return. Otherwise, we do a cleanup
         if (value is > _FIRSTENT and < _LASTAUX) {
            // Reset all buffers in preparation for reading this entity
            I0 = I1 = I2 = I3 = I4 = I7 = 0; D2 = 0; Ang0 = 0; 
            mX0.ClearFast (); mY0.ClearFast ();
            mD0.ClearFast (); mD1.ClearFast (); mD2.ClearFast ();
            PaperSpace = ZFlip = Invisible = false; ColorNo = 256;
            // TODO: Reset ColorNo if not VERTEX / SEQEND
         } else {
            if (value is > _FIRSTIGNORE and < _LASTIGNORE) mType = SKIPPEDENT;
            else Fatal ($"Unclassified Type {value}");
            mSkipForward = true;
         }
      }
   }
   static HashSet<EDXF> sTypeIgnore;
   EDXF mType = SKIPPEDENT;

   // Computed properties ------------------------------------------------------
   EDXF E => DXFCore.Dict.GetValueOrDefault (D.AsSpan (mSt, mLen));  // Current value, as an EDXF enumeration

   double Angle => Ang0.D2R ();        // Group 50
   int Flags => I0;                    // Flags value (group 70)   
   double Height => D0 * Scale;        // Group 10 code
   Point2 Pt0 => new (X0 * Scale, Y1 * Scale);  // 10,20 group codes
   Point2 Pt1 => new (X1 * Scale, Y1 * Scale);  // 11,21 group codes
   Point2 Pt2 => new (X2 * Scale, Y2 * Scale);  // 12,22 group codes
   Point2 Pt3 => new (X3 * Scale, Y3 * Scale);  // 13,23 group codes
   Style2 Style => mDwg.GetStyle (StyleName) ?? mDwg.GetStyle ("STANDARD")!;
   double XScale => D1 == 0 ? 1 : D1;  // Group 41 code (defaults to 1)
   double YScale => D2 == 0 ? 1 : D2;  // Group 42 code (defaults to 1)

   ReadOnlySpan<byte> SP => D.AsSpan (mSt, mLen);
   string V => mEncoding.GetString (D.AsSpan (mSt, mLen));           // Current value, as a string
   double Vf => SP.ToDouble ();
   int Vn => SP.ToInt ();

   // Storage properties -------------------------------------------------------
   int ColorNo;
   double Ang0, Ang1;
   int I0, I1, I2, I3, I4, I7;
   double D3, D4, D5, D6, D7, D8;
   double X1, Y1, X2, X4, X5, X6, Y2, X3, Y3, Y4, Y5, Y6;
   bool PaperSpace, Invisible;
   double DimCen, DimGap;
   double D0 { get => field; set => mD0.Add (field = value); }
   double D1 { get => field; set => mD1.Add (field = value); }
   double X0 { get => field; set => mX0.Add (field = value); } 
   double Y0 { get => field; set => mY0.Add (field = value); }
   List<double> mX0 = [], mY0 = [], mD0 = [], mD1 = [], mD2 = [];
   string Name = "", LTName = "", StyleName = "", FontName = "", Text = "";
   bool ZFlip;

   // Whenever we read in a new layername, we set this 
   string LayerName { set { if (field != value) mLayer = mLayers[field = value]; } } = "";
   Layer2 Layer => mLayer ?? mDwg.CurrentLayer;
   Layer2? mLayer;

   // Group 42 is also used for the current bulge factor (in VERTEX entities)
   // This can be elided for some VERTEX objects (and only X0 and Y0 may be supplied).
   // So when adding a new bulge, we make sure the mD2 list has the same number of 
   // elements as the mX0 list (filling the missing elements with bulge=0 as needed)
   double D2 {
      get => field;
      set {
         while (mD2.Count < mX0.Count - 1) mD2.Add (0);
         mD2.Add (field = value);
      }
   }

   // Debug code ---------------------------------------------------------------
   [DoesNotReturn] void Fatal (string s) => throw new ($"At line {R.LineNo - 2}: {s}");
   [DoesNotReturn] void Unexpected () => Fatal ("Unexpected");

   void UnhandledGroup (int g) {
      sGroupIgnore ??= [.. Lib.ReadLines ("nori:DXF/group-ignore.txt").Select (a => a.ToInt ())];
      if (sGroupIgnore.Contains (g)) return;
      Fatal ($"Unhandled Group {g}, {mType} entity");
   }
   static HashSet<int>? sGroupIgnore;

   void UnknownHeaderVar (string s) {
      sHeaderIgnore ??= [.. Lib.ReadLines ("nori:DXF/header-ignore.txt")];
      if (sHeaderIgnore.Contains (s)) return; 
      Fatal ($"Unknown header var: {s}");
   }
   HashSet<string>? sHeaderIgnore;

   void Warn (string s) { if ((sWarnings ??= []).Add (s)) Lib.Trace (s); }
   HashSet<string>? sWarnings;

   // Private data -------------------------------------------------------------
   int G;                        // Group code
   int mSt, mLen;                // Start and length of the current line
   readonly Dwg2 mDwg = new ();   // The drawing we're building
   readonly byte[] D;            // The raw data of the line
   readonly UTFReader R;         // The UTFReader used to read the file
   static SearchValues<byte> sCRLF = SearchValues.Create (13, 10);
   Encoding mEncoding = Encoding.UTF8;       // The encoding we're using
   Dictionary<string, Layer2> mLayers = [];  // Dictionary mapping names to layers
   string mBlockName = "*";      // Name of the block we're building
   List<Ent2>? mBlockEnts;       // List of entities in that block
   Point2 mBlockPt;              // Insert point of that block
   bool? mClosedPoly;            // NULL=not-in-poly, true=closed-poly, false=open-poly
   readonly List<string> mXData = [];     // Group 1000 codes (like BEND_ANGLE:90)
   readonly StringBuilder mSB = new ();   // Stringbuilder used in various operations
}
