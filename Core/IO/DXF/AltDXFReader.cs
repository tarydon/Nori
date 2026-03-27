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
            case 1: Text = ""; break;
            case 3: FontName = V; break;
            case 8: LayerName = V; break;
            case 6: LTName = V; break;
            case 7: StyleName = V; break;
            case 9: HeaderVar (E); break;
            case 10: X0 = Vf; break; case 20: Y0 = Vf; break;
            case 11: X1 = Vf; break; case 21: Y1 = Vf; break;
            case 12: X2 = Vf; break; case 22: Y2 = Vf; break;
            case 13: X3 = Vf; break; case 23: Y3 = Vf; break;
            case 14: X4 = Vf; break; case 24: Y4 = Vf; break;
            case 15: X5 = Vf; break; case 25: Y5 = Vf; break;
            case 16: X6 = Vf; break; 
            case 26: Y6 = Vf; break;
            case 40: D40 = Vf; break; case 41: D41 = Vf; break; case 42: D42 = Vf; break;
            case 43: D43 = Vf; break; case 44: D44 = Vf; break; case 45: D45 = Vf; break;
            case 46: D46 = Vf; break; case 47: D47 = Vf; break; case 48: D48 = Vf; break; 
            case 50: D50 = Vf; break; case 51: D51 = Vf; break;
            case 60: Invisible = Vn == 1; break;
            case 70: I0 = Vn; break; case 71: I1 = Vn; break; case 72: I2 = Vn; break; 
            case 73: I3 = Vn; break; case 74: I4 = Vn; break; case 77: I7 = Vn; break;
            case 62: ColorNo = E switch { BYLAYER => 256, BYBLOCK => 257, _ => Vn }; break;
            case 67: PaperSpace = Vn > 0; break;
            case 141: DimCen = Vf; break;
            case 147: DimGap = Vf; break;
            case 230: ZDir = Vf < -0.999 ? -1 : 1; break;
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

   void Add (Poly poly) => Add (new E2Poly (Layer, poly));

   void Add (Ent2 ent) {
      if (Invisible) return;
      ent.Color = GetColor (ColorNo); mDwg.Add (ent);
   }

   // Builds an entity
   void BuildEnt (EDXF type) {
      if (type > _LASTENT) return;
      switch (type) {
         case CIRCLE: Add (Poly.Circle (Pt0, Radius)); break;
         case STYLE: mDwg.Add (new Style2 (Name, FontName, D40 * Scale, D41 == 0 ? 1 : D41, D50.D2R ())); break;
         case TRACE or SOLID: Add (new E2Solid (Layer, [Pt0, Pt1, Pt2, Pt3])); break;

         case LAYER:
            bool visible = (I0 & 1) != 1;
            var layer = new Layer2 (Name, GetColor (ColorNo), GetLType (LTName)) { IsVisible = visible };
            if (mLayers.TryAdd (Name, layer)) mDwg.Add (layer);
            break;

         case LINE:
            var line = Poly.Line (Pt0, Pt1);
            // Try to find bend info among mXData entries
            var (ba, radius, kfactor) = (double.NaN, 0.0, 0.42);
            foreach (var s in mXData) {
               if (s.StartsWith ("BEND_ANGLE:")) ba = s[11..].ToDouble ().D2R ();
               else if (s.StartsWith ("BEND_RADIUS:")) radius = s[12..].ToDouble () * Scale;
               else if (s.StartsWith ("K_FACTOR:")) kfactor = s[9..].ToDouble ();
            }
            if (!ba.IsNan) {
               Add (new E2Bendline (mDwg, line.Pts, ba, radius, kfactor));
               mXData.Clear ();
            } else Add (line);
            break;

         case TEXT:
            int hAlign = I2 > 2 ? 0 : I2.Clamp (0, 2), vAlign = 3 - I3.Clamp (0, 3);
            ETextAlign align = (ETextAlign)(vAlign * 3 + hAlign + 1);
            Point2 pos = align == ETextAlign.BaseLeft ? Pt0 : Pt1;
            Add (new E2Text (Layer, Style, Clean (Text, mSB), pos, Height, Angle, Oblique, XScale, align));
            break;

         case BLOCK or SOLID or MTEXT or LINE or ARC : break;
         case POINT or LWPOLYLINE or DIMENSION or INSERT or SPLINE or POLYLINE: break;
         case VERTEX or SEQEND or ATTDEF or ATTRIB or LEADER or TRACE or ELLIPSE or XLINE: break;
         default: Fatal ($"Unhandled entity {type}"); break;
      } // TODO: Handle HATCH
   }

   // Parses the encoded characters in the text to the corresponding special characters
   internal static string Clean (string text, StringBuilder? sb = null) {
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

   /// <summary>Convert an AutoCAD color to a Color4</summary>
   Color4 GetColor (int index) {
      if (index == 256) return Color4.Nil;
      var color = ACADColors[index.Clamp (0, 255)];
      if (WhiteToBlack && color.EQ (Color4.White)) color = Color4.Black;
      if (DarkenColors) color = color.Darkened ();
      return color;
   }

   /// <summary>Converts a DXF linetype string to the corresponding ELineType enum value </summary>
   static ELineType GetLType (string lt) => lt.ToUpper () switch {
      "DOT" or "DOTTED" => ELineType.Dot,
      "DASH" or "DASHED" => ELineType.Dash,
      "DASHDOT" => ELineType.DashDot,
      "DIVIDE" or "DASHDOTDOT" or "DASH2DOT" => ELineType.DashDotDot,
      "CENTER" => ELineType.Center,
      "BORDER" => ELineType.Border,
      "HIDDEN" => ELineType.Hidden,
      "DASH2" => ELineType.Dash2,
      "PHANTOM" => ELineType.Phantom,
      _ => ELineType.Continuous
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
      sHandle = [HEADER, TABLES, BLOCKS, ENTITIES, LTYPE, LAYER, STYLE, DIMSTYLE, LAYERS],
      sIgnore = [CLASSES, VPORT, UCS, APPID, VIEW, BLOCK_RECORD, OBJECTS, ACDSDATA, THUMBNAILIMAGE, 
                 DWGMGR];

   // This is called at the start of each table
   void HandleTable (EDXF t) {
      if (!sHandle.Contains (t)) {
         if (!sIgnore.Contains (t)) Fatal ($"Unhandled TABLE: {V}");
         while (Next ()) { if (G == 0 && E == ENDTAB) break; }
      } 
   }

   // Reads the next group into mGroup and the value into mValue
   bool Next () {
      for (; ; ) {
         // if (R.AtEndOfFile) return false;
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
            I0 = I1 = I2 = I3 = I4 = I7 = 0; D42 = 0; D50 = 0; 
            mX0.ClearFast (); mY0.ClearFast (); mXData.Clear ();
            mD0.ClearFast (); mD1.ClearFast (); mD2.ClearFast ();
            PaperSpace = Invisible = false; ZDir = 1; ColorNo = 256;
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

   int Flags => I0;
   double Angle => D50.D2R ();
   double Height => D40 * Scale;
   double Oblique => D51.D2R ();
   Point2 Pt0 => new (X0 * Scale, Y0 * Scale);
   Point2 Pt1 => new (X1 * Scale, Y1 * Scale);
   Point2 Pt2 => new (X2 * Scale, Y2 * Scale);
   Point2 Pt3 => new (X3 * Scale, Y3 * Scale);
   double Radius => D40 * Scale;
   double XScale => D41 == 0 ? 1 : D41;
   double YScale => D42 == 0 ? 1 : D42;

   ReadOnlySpan<byte> SP => D.AsSpan (mSt, mLen);
   string V => mEncoding.GetString (D.AsSpan (mSt, mLen));           // Current value, as a string
   double Vf => SP.ToDouble ();
   int Vn => SP.ToInt ();

   // Storage properties -------------------------------------------------------
   int ColorNo;
   double D50, D51;
   bool PaperSpace, Invisible;
   int I0, I1, I2, I3, I4, I7;
   double D43, D44, D45, D46, D47, D48;
   double X1, Y1, X2, X4, X5, X6, Y2, X3, Y3, Y4, Y5, Y6;
   double DimCen, DimGap;
   double D40 { get => field; set => mD0.Add (field = value); }
   double D41 { get => field; set => mD1.Add (field = value); }
   double X0 { get => field; set => mX0.Add (field = value); } 
   double Y0 { get => field; set => mY0.Add (field = value); }
   List<double> mX0 = [], mY0 = [], mD0 = [], mD1 = [], mD2 = [];
   string Name = "", LTName = "", StyleName = "", FontName = "", Text = "";
   double ZDir = 1;

   double D42 {
      get => field;
      set {
         while (mD2.Count < mX0.Count - 1) mD2.Add (0);
         mD2.Add (field = value);
      }
   }

   Style2 Style => mDwg.GetStyle (StyleName) ?? mDwg.GetStyle ("STANDARD")!;
   string LayerName { set { if (field != value) { mLayer = mLayers.GetValueOrDefault (field = value); } } }
   Dictionary<string, Layer2> mLayers = new (StringComparer.OrdinalIgnoreCase);
   Layer2 Layer => mLayer ?? mDwg.CurrentLayer;
   Layer2? mLayer;

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
   readonly Dwg2 mDwg = new ();  // The drawing we're building
   readonly byte[] D;            // The raw data of the line
   readonly UTFReader R;         // The UTFReader used to read the file
   int G;                        // Group code
   int mLineNo;                  // The line number
   int mSt, mLen;                // Start and length of the current line
   static SearchValues<byte> sCRLF = SearchValues.Create (13, 10);
   Encoding mEncoding = Encoding.UTF8;       // The encoding we're using
   List<string> mXData = [];     // Strings loaded from 1000 group (used for bend-data)
   readonly StringBuilder mSB = new ();      
}
