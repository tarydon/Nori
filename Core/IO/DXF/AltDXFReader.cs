namespace Nori.Alt;
using static EDXF;

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
            case SECTION: LoadSection (); break;
            default: Console.Write ($"{type} "); break;
         }
      }
      return mDwg;
   }

   // Implementation -----------------------------------------------------------
   void LoadSection () {
      // We need special processing only for the HEADER section, so we do that here. 
      Next (); Check (G == 2);
      if (E (2) != HEADER) return;
      for (; ; ) {
         if (!Next () || G == 0) break;
         if (G == 9) {
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
                     mEncoding = DXFCore.GetEncoding (S (G));
                  break;
            }
         }
      }
   }

   // Reads the next group code into mG and the corresponding value span into 
   // mSt[G],mLen[G]. If the variable mSkipForward is set, then this keeps reading key-value
   // pairs until a group 0 has been read.
   bool Next () {
      int st, len;
      for (; ;) {
         if (mR.AtEndOfFile) return false;
         mR.Read (out G).SkipToNextLine ();       // Read the group code
         mR.ReadLineRange (out st, out len);       // Read the value
         if (G == 0) {
            if (len == 3 && mD[st] == 'E' && mD[st + 1] == 'O' && mD[st + 2] == 'F') return false;
            mBase = st; mSkipForward = false;
         }
         if (!mSkipForward) break;
      }
      if (G < 256) { mSt[G] = st; mLen[G] = len; }
      return true; 
   }
   // If this is set, the next call to Next() will keep reading group-value codes until a
   // group 0 is read (and that is stored in mG along with its value)
   bool mSkipForward;

   // Reads forward until the next group 0 code, and returns the EDXF tag for the object
   // found there
   EDXF NextObject () {
      mSkipForward = true;
      if (!Next ()) return EOF;
      Check (G == 0);      
      return E (0);
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
}
