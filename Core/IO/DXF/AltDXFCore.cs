namespace Nori.Alt;
using static EDXF;
using static ELineType;

// enum EDXF -------------------------------------------------------------------
enum EDXF {
   NIL, SKIPPEDENT,

   _FIRSTENT,

   // All objects in this range are ignored
   _FIRSTIGNORE,
   CLASS, ENDSEC, TABLE, ENDTAB, VPORT, APPID, BLOCK_RECORD, HELIX, MESH, SUN, UCS, 
   UNDERLAY, VIEW, OBJECTS, ACDSDATA, THUMBNAILIMAGE, DWGMGR, LTYPE, VIEWPORT, IMAGE, 
   HATCH, TOLERANCE, _3DFACE, _3DSOLID, OLE2FRAME, OLEFRAME, ACAD_PROXY_ENTITY, REGION, 
   ASSURFACE, WIPEOUT, BODY, SURFACE, LIGHT, ATTDEF, DICTIONARY, XRECORD, DIMASSOC, 
   LAYOUT, MATERIAL, MLEADERSTYLE, MLINESTYLE, SCALE, ACDBDICTIONARYWDFLT, ACDBDETAILVIEWSTYLE, 
   ACDBPLACEHOLDER, ACDBSECTIONVIEWSTYLE, TABLESTYLE, VISUALSTYLE, DICTIONARYVAR, 
   CELLSTYLEMAP, ACDSSCHEMA, ACDSRECORD,
   _LASTIGNORE,

   // These objects are all loaded using a 'simple load' - this means we can read in all
   // the key value pairs (since none repeat) before building the object
   _FIRSTSIMPLE,
   LAYER,
   _LASTSIMPLE,

   // These are the entities we are going to try and read (this also includes things like
   // LAYER, STYLE etc that don't reside in the ENTITIES section, but in other sections such
   // as the TABLES section)
   STYLE, BLOCK, LINE, SOLID, MTEXT, POINT, ARC, CIRCLE, LWPOLYLINE, TEXT, DIMENSION,
   INSERT, SPLINE, POLYLINE, VERTEX, SEQEND, ATTRIB, LEADER, TRACE, ELLIPSE, XLINE, ENDBLK,
   _LASTENT,

   // These are the other objects (not entities) that we are going to not skip over
   _FIRSTAUX,
   SECTION, DIMSTYLE,
   _LASTAUX,

   // Header values we are going to read
   _ACADVER, _DWGCODEPAGE, _MEASUREMENT, _CLAYER, _INSUNITS,

   // Miscellaneous
   HEADER, 
   TABLES, BLOCKS, ENTITIES, CLASSES, LAYERS, BYBLOCK, BYLAYER,

   EOF
}

// class DXFCore ---------------------------------------------------------------
public class DXFCore {
   internal static Color4[] ACADColors
      => sACADColors ??= [..Lib.ReadLines("nori:DXF/color.txt")
                          .Select(a => new Color4(uint.Parse(a, NumberStyles.HexNumber) | 0xff000000))];
   static Color4[]? sACADColors;

   internal static SymTable<EDXF> Dict {
      get {
         if (sDict == null) {
            sDict = new ();
            for (var ed = _FIRSTENT; ed <= EOF; ed++) {
               var s = ed.ToString ();
               if (s.StartsWith ("_3")) s = s[1..];
               else if (s[0] == '_') s = s.Replace ('_', '$');
               sDict.Add (s, ed);
            }
         }
         return sDict;
      }
   }
   static SymTable<EDXF>? sDict;

   internal static Color4 GetColor (ReadOnlySpan<byte> txt) {
      if (Dict.GetValueOrDefault (txt) is BYLAYER or BYBLOCK) return Color4.Nil;
      return ACADColors[txt.ToInt () & 255];
   }

   internal static ELineType GetLType (string s) 
      => sLinteypes.GetValueOrDefault (s, Continuous);
   static Dictionary<string, ELineType> sLinteypes = new (StringComparer.OrdinalIgnoreCase) {
      ["DOT"] = Dot, ["DOTTED"] = Dot, ["DASH"] = Dash, ["DASHED"] = Dash, ["DASHDOT"] = DashDot, 
      ["DIVIDE"] = DashDotDot, ["DASHDOTDOT"] = DashDotDot, ["DASH2DOT"] = DashDotDot, 
      ["CENTER"] = Center, ["BORDER"] = Border, ["DASH2"] = Dash2, ["HIDDEN"] = Hidden,
      ["PHANTOM"] = Phantom, ["CONTINUOUS"] = Continuous,
   };

   internal static Encoding GetEncoding (string codepage) {
      if (!sEncodingsRegistered) {
         sEncodingsRegistered = true;
         Encoding.RegisterProvider (CodePagesEncodingProvider.Instance);
      }
      return Encoding.GetEncoding (GetCodePage (codepage)) ?? Encoding.UTF8;

      // Helper ............................................
      static int GetCodePage (string name) {
         if (name == "dos932") return 932;
         if (int.TryParse (name.Split ('_').Last (), out int n)) return n;
         return 1252;
      }
   }
   static bool sEncodingsRegistered;
}
