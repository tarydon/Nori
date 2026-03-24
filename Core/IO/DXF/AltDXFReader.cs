using System.Buffers;
using System.Buffers.Text;
using Nori.Internal;
namespace Nori.Alt;
using static EDXF;

public class DXFReader {
   // Constructors -------------------------------------------------------------
   public DXFReader (byte[] data) => R = new (D = data);
   public DXFReader (string file) : this (Lib.ReadBytes (file)) { }
   static DXFReader () => Encoding.RegisterProvider (CodePagesEncodingProvider.Instance);

   // Properties ---------------------------------------------------------------
   /// <summary>
   /// The Standard 256 ACAD colors
   /// </summary>
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
   /// <summary>
   /// Parse the file, build the DXF and return it
   /// </summary>
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
            case 9: HeaderVar (E); break;
            case 10: X0 = Vf; break; case 20: Y0 = Vf; break;
            case 70: I0 = Vn; break; case 71: I1 = Vn; break; 
            case 72: I2 = Vn; break; case 73: I3 = Vn; break;
            case 40: D0 = Vn; break; case 41: D1 = Vn; break; case 42: D2 = Vn; break;
            case 62: ColorNo = E switch { BYLAYER => 256, BYBLOCK => 257, _ => Vn }; break;

            case 2:
               switch (mType) {
                  case SECTION: HandleSection (E); break;
                  case TABLE: HandleTable (E); break;
                  case LTYPE or LAYER: Name = V; break;
                  default: Fatal ("Unexpected 2 code"); break;
               }
               break;

            default: UnhandledGroup (G); break;
         }
      }
      return mDwg;
   }

   // Implementation -----------------------------------------------------------
   // Called when we read a header variable
   void HeaderVar (EDXF key) {
      if (key == NIL) { UnknownHeaderVar (V); Next (); return; }
      Next (); 
      switch (key) {
         case _ACADVER: mACADVer = V; break;
         case _CLAYER: mCurrentLayer = V; break;
         case _LTSCALE: mLTScale = Vf; break;
         case _MEASUREMENT: mScale = Vn == 0 ? 25.4 : 1; break;

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
   double mLTScale = 1, mScale = 1; 

   // This is called at the start of each section. 
   // For the HEADER section, this will read in a few variables. For other sections that we 
   // don't care about, we will simply skip the section (by running forward to the next ENDSEC)
   void HandleSection (EDXF s) {
      if (!sHandle.Contains (s)) {
         if (!sIgnore.Contains (s)) Warn ($"Unhandled SECTION: {V}");
         while (Next ()) { if (G == 0 && E == ENDSEC) break; }
      } else
         mSection = s;
   }
   // These are the stuff we handle, and the stuff we knowingly ignore
   static readonly HashSet<EDXF> sHandle = [HEADER, TABLES, BLOCKS, ENTITIES, LTYPE, LAYER],
      sIgnore = [CLASSES, VPORT, UCS];
   EDXF mSection, mTable;

   // This is called at the start of each table
   void HandleTable (EDXF t) {
      if (!sHandle.Contains (t)) {
         if (!sIgnore.Contains (t)) Warn ($"Unhandled TABLE: {V}");
         while (Next ()) { if (G == 0 && E == ENDTAB) break; }
      } else
         mTable = t;
   }

   // Reads the next group into mGroup and the value into mValue
   bool Next () {
      R.Read (out G).SkipToNextLine ();      // Read the group code
      R.ReadLineRange (out mSt, out mLen);   // Read the value
      if (mLen == 3 && D[mSt] == 'E' && D[mSt + 1] == 'O' && D[mSt + 2] == 'F') return false;
      return true; 
   }

   // Write-to properties ------------------------------------------------------
   // This is written to each time we see a 0 group, and effectively this ends up 'making' a
   // new object of that type. However, since a type descriptor (0 group) is _followed_ by the 
   // paramters requried for that object, we cannot actually make an object when we see a 0 group.
   // Instead, we make the _previous_ object that we saw, and that is why this is a big switch
   // statement on *mType* which is the _previous_ type we read. All the parameters we would need
   // to build that previous object have been read already and are now latched into state
   // variables like D, Color, N, LTypeName etc. 
   EDXF Type {
      set {
         switch (mType) {
         }

         // Now that we've constructed the previous object, we can store the incoming Type value
         // to start preparing for the next
         if ((mType = value) == NIL) Fatal ($"Unknown: {V}");

         // The reset we do after completing each entity
         mX0.Clear (); mY0.Clear ();
         I0 = I1 = I2 = I3 = 0;
      }
   }
   EDXF mType;

   // Computed properties ------------------------------------------------------
   EDXF E => DXFCore.Dict.GetValueOrDefault (D.AsSpan (mSt, mLen));  // Current value, as an EDXF enumeration
   
   ReadOnlySpan<byte> SP => D.AsSpan (mSt, mLen);
   string V => mEncoding.GetString (D.AsSpan (mSt, mLen));           // Current value, as a string
   double Vf => SP.ToDouble ();
   int Vn => SP.ToInt ();

   // Storage properties -------------------------------------------------------
   int I0, I1, I2, I3;
   int ColorNo;
   double X0 { get => field; set => mX0.Add (field = value); } 
   double Y0 { get => field; set => mY0.Add (field = value); }
   double D0 { get => field; set => mD0.Add (field = value); }
   double D1 { get => field; set => mD1.Add (field = value); }
   List<double> mX0 = [], mY0 = [], mD0 = [], mD1 = [], mD2 = [];
   string Name = "", LTName = "", StyleName = "";

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
      Fatal ($"Unhandled Group {g}");
   }
   static HashSet<int>? sGroupIgnore;

   void UnknownHeaderVar (string s) {
      sHeaderIgnore ??= [.. Lib.ReadLines ("nori:DXF/header-ignore.txt")];
      if (sHeaderIgnore.Contains (s)) return; 
      Warn ($"Unknown header var: {s}");
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
   int mSt, mLen;             // Start and length of the current line
   static SearchValues<byte> sCRLF = SearchValues.Create (13, 10);
   Encoding mEncoding = Encoding.UTF8;    // The encoding we're using
}
