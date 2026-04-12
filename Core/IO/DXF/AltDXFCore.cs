namespace Nori.Alt;
using static EDXF;

// enum EDXF -------------------------------------------------------------------
enum EDXF {
   NIL, SKIPPEDENT,

   // These are the entities we are going to try and read (this also includes things like
   // LAYER, STYLE etc that don't reside in the ENTITIES section, but in other sections such
   // as the TABLES section)
   _FIRSTENT,
   LAYER, STYLE, BLOCK, LINE, SOLID, MTEXT, POINT, ARC, CIRCLE, LWPOLYLINE, TEXT, DIMENSION,
   INSERT, SPLINE, POLYLINE, VERTEX, SEQEND, ATTRIB, LEADER, TRACE, ELLIPSE, XLINE, ENDBLK,
   _LASTENT,

   // These are the other objects (not entities) that we are going to not skip over
   _FIRSTAUX,
   SECTION, ENDSEC, TABLE, ENDTAB, DIMSTYLE,
   _LASTAUX,

   // These are all the entities / tables / sections that we are going to ignore   
   _FIRSTIGNORE,
   CLASS, LTYPE, VIEWPORT, IMAGE, HATCH, TOLERANCE, ACAD_CIRCLE, ACAD_LINE, _3DFACE, _3DSOLID,
   ACAD_TABLE, OLE2FRAME, OLEFRAME, ACAD_PROXY_ENTITY, REGION, TCPOINTENTITY, ASSURFACE, WIPEOUT,
   Point2, BODY, SURFACE, LIGHT, ATTDEF,
   _LASTIGNORE,

   // Header values we are going to read
   _ACADVER, _DWGCODEPAGE, _MEASUREMENT, _CLAYER, _INSUNITS,

   // Miscellaneous
   APPID, BLOCK_RECORD, HELIX, MESH, SUN, UCS, UNDERLAY, VIEW, VPORT, OBJECTS,
   ACDSDATA, THUMBNAILIMAGE, DWGMGR, HEADER, DICTIONARY, XRECORD, DIMASSOC, LAYOUT, MATERIAL,
   MLEADERSTYLE, MLINESTYLE, SCALE, TABLES, BLOCKS, ENTITIES, CLASSES, LAYERS, BYBLOCK,
   ACDBDICTIONARYWDFLT, ACDBDETAILVIEWSTYLE, ACDBPLACEHOLDER, ACDBSECTIONVIEWSTYLE, TABLESTYLE,
   VISUALSTYLE, DICTIONARYVAR, CELLSTYLEMAP, ACDSSCHEMA, ACDSRECORD, BYLAYER,

   EOF
}

// class DXFCore ---------------------------------------------------------------
public class DXFCore {
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

   internal static Encoding GetEncoding (string codepage) {

   }
}
