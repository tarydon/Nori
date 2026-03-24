namespace Nori.Internal;

enum EDXF {
   NIL, SECTION, ENDSEC, CLASS, TABLE, ENDTAB, VPORT, LTYPE, LAYER, STYLE, APPID, HEADER, 
   DIMSTYLE, BLOCK_RECORD, BLOCK, ENDBLK, LINE, SOLID, MTEXT, POINT, ARC, CIRCLE, LWPOLYLINE,
   TEXT, DIMENSION, INSERT, SPLINE, DICTIONARY, XRECORD, DIMASSOC, LAYOUT, MATERIAL, MLEADERSTYLE,
   MLINESTYLE, SCALE, TABLES, BLOCKS, ENTITIES, CLASSES,

   _ACADVER, _DWGCODEPAGE, _MEASUREMENT, _EXTMIN, _EXTMAX, _CLAYER, _LTSCALE, 
   
   ACDBDICTIONARYWDFLT, ACDBDETAILVIEWSTYLE, ACDBPLACEHOLDER, ACDBSECTIONVIEWSTYLE, TABLESTYLE,
   VISUALSTYLE, DICTIONARYVAR, CELLSTYLEMAP, ACDSSCHEMA, ACDSRECORD, UCS, BYLAYER, BYBLOCK,
   EOF
}

class DXFCore {
   public static SymTable<EDXF> Dict {
      get {
         if (sDict == null) {
            sDict = new ();
            for (var ed = EDXF.SECTION; ed <= EDXF.EOF; ed++) {
               var s = ed.ToString (); if (s[0] == '_') s = s.Replace ('_', '$');
               sDict.Add (s, ed);
            }
         }
         return sDict;
      }
   }
   static SymTable<EDXF>? sDict;
}