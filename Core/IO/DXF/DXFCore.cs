// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DXFCore.cs
// ║║║║╬║╔╣║ Core routines used by DXF reader / writer
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
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
   DIMSTYLE,
   _LASTENT,

   // These are the other objects (not entities) that we are going to not skip over
   _FIRSTAUX,
   SECTION, ENDSEC, TABLE, ENDTAB, 
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
}

// class DXFReader (partial) ---------------------------------------------------
// Partial methods of DXFReader implemented only in DEBUG mode. 
// These are useful when testing out the DXF reader (reporting on unhandled entities,
// unknown header variables etc)
public partial class DXFReader {
#if DEBUG
   partial void Skipping (EDXF e, string name) {
      if (!sIgnore.Contains (e)) Fatal ($"Unhandled {name}: {V}");
   }
   static readonly HashSet<EDXF> sIgnore = 
      [CLASSES, VPORT, UCS, APPID, VIEW, BLOCK_RECORD, OBJECTS, ACDSDATA, THUMBNAILIMAGE, DWGMGR];

   partial void UnhandledGroup (int g) {
      sGroupIgnore ??= [.. Lib.ReadLines ("nori:DXF/group-ignore.txt").Select (a => a.ToInt ())];
      if (sGroupIgnore.Contains (g)) return;
      Fatal ($"Unhandled Group {g}, {mType} entity");
   }
   static HashSet<int>? sGroupIgnore;

   partial void UnknownHeaderVar (string s) {
      sHeaderIgnore ??= [.. Lib.ReadLines ("nori:DXF/header-ignore.txt")];
      if (sHeaderIgnore.Contains (s)) return;
      Fatal ($"Unknown header var: {s}");
   }
   HashSet<string>? sHeaderIgnore;

   partial void Warn (string s) { if ((sWarnings ??= []).Add (s)) Lib.Trace (s); }
   HashSet<string>? sWarnings;
#endif
}
