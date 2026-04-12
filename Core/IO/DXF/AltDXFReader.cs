namespace Nori.Alt;
using static EDXF;
using static DXFCore;

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

            case SECTION: LoadSection (); break;
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
      return mDwg;
   }
   HashSet<EDXF> sReported = [];

   // Implementation -----------------------------------------------------------
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
         case LAYER:
            bool visible = N (70).IsEven ();
            var layer = new Layer2 (S (2), GetColor (SB (62)), GetLType (S (6))) { IsVisible = visible };
            if (mLayerMap.TryAdd (layer.Name, layer)) mDwg.Add (layer);
            break;
         default:
            throw new NotImplementedException ();
      }
   }

   // Reads one key-value pair - returns false if we are at end-of-file
   // This skips all keys above 255
   bool Next () {
      for (; ; ) {
         if (mR.AtEndOfFile) return false;
         mR.Read (out G).SkipToLineEnd ();
         mR.ReadLineRange (out int st, out int len);
         if (G < 256) { mSt[G] = st; mLen[G] = len; return true; }
      }
   }

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

   // Group value as an EDXF enum
   EDXF E (int g) => DXFCore.Dict.GetValueOrDefault (SB (g));

   // Get the group value N as a span of bytes (if this group value was not present for this entity,
   // this returns an empty (zero-length) span)
   ReadOnlySpan<byte> SB (int g) {
      int start = mSt[g]; if (start < mBase) return mD.AsSpan (0, 0);
      return mD.AsSpan (start, mLen[g]);
   }

   // Group value G as an integer
   int N (int g) => SB (g).ToInt ();

   // Group value G as a string
   string S (int g) => mEncoding.GetString (SB (g));

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
   string mACADVer = "AC1021";
   string mCurrentLayer = "";
   bool mUnitsSet = false;
   double Scale = 1;

   // Private data -------------------------------------------------------------
   readonly byte[] mD;           // Raw data of the file
   readonly UTFReader mR;        // UTFReader used to read the file
   readonly Dwg2 mDwg = new ();  // The drawing we're constructing

   int G;                        // Group code 
   int mBase;                    // The current 'entity' (Group 0 set) data starts here
   int[] mSt = new int[256];     // For each group code, the start of the value
   int[] mLen = new int[256];    // .. and the length of that value (both in D)

   Encoding mEncoding = Encoding.UTF8;    // Encoding we're using for this file
   Dictionary<string, Layer2> mLayerMap = new (StringComparer.OrdinalIgnoreCase);
}
