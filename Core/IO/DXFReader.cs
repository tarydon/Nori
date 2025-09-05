// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DXFReader.cs
// ║║║║╬║╔╣║ Implements DXFReader: reads in a Dwg2 from a DXF file
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;

namespace Nori;

/// <summary>DXFReader is used to read a DXF file into a Dwg2</summary>
public partial class DXFReader {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a DXFReader, given a filename</summary>
   public DXFReader (string file) => mReader = new StreamReader (new FileStream (mFile = file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

   // Methods ------------------------------------------------------------------
   /// <summary>Parse the file, Build the DXF and return it</summary>
   public Dwg2 Build () {
      // In general, we have a 'backing variable' for each group code, like
      // 1=>Text, 62=>ColorNo, 2=>Name, 10=>X0 etc.
      // However, there are a few entities (like LWPOLYLINE, HATCH, LEADER)
      // where some group codes like 10,20,42 are repeated multiple times in the
      // entity. We have to recognize and process these separately. For now,
      // the 10,20 and 42 group codes are added to a list when processing the LWPOLYLINE
      // entity. Later, when we support HATCH, LEADER etc, we will extend this mechanism
      // suitably.
      while (Next ()) {
         switch (G) {
            case 0: Type = V; break;
            case 2:
               if (mType == "SECTION") HandleSection (V);
               else S[G] = V;
               break;
            case 8: if (mClosedPoly == null) S[G] = V; break;     // Ignore layers when we are inside a POLYLINE read
            case > 0 and < 10: S[G] = V; break;
            case 10: X0Set.Add (Vf); break;
            case 20: Y0Set.Add (Vf); break;
            case 40: D0Set.Add (D[40] = Vf); break;
            case 41: D1Set.Add (D[41] = Vf); break;
            case 42:
               while (D2Set.Count < X0Set.Count - 1) D2Set.Add (0);
               D2Set.Add (D[42] = Vf);
               break;
            case > 10 and < 53: D[G] = Vf; break;
            case 60: Invisible = Vn == 1; break;
            case 62: ColorNo = V == "BYLAYER" ? 256 : Vn; break;
            case 70: I0 = Vn; break; case 71: I1 = Vn; break;
            case 72: I2 = Vn; break; case 73: I3 = Vn; break;
            case 230: ZDir = Vf.EQ (-1) ? -1 : 1; break;
            case 1000: mXData.Add (V); break;
         }
      }
      mReader.Dispose ();
      ProcessBendText ();
      return mDwg;
   }

   /// <summary>Helper to load a DXF file, given the filename</summary>
   public static Dwg2 FromFile (string name) => new DXFReader (name).Build ();

   // Implementation -----------------------------------------------------------
   // Called at the start of each section.
   // For the HEADER section, this reads in all the important header variables.
   // For some of the sections like OBJECTS etc this just skips past the section.
   // For other sections like TABLES, BLOCKS, ENTITIES, this just returns without
   // 'eating' the section so the section continues to be processed as normal
   void HandleSection (string name) {
      if (!sSections.Contains (name)) {
         while (Next ()) { if (G == 0 && V == "ENDSEC") break; }
      } else
         S[G] = name;
      return;
   }
   static HashSet<string> sSections = ["TABLES", "BLOCKS", "ENTITIES"];

   // This is written to each time we see a 0 group, and effectively this ends up
   // 'making' a new object of some type. In some sense, writes to the Type property are
   // like delimiters between the objects we are creating. Since the Type descriptor (0 group)
   // is _followed_ by the parameters required for that object, we cannot actually make an
   // object when we see a 0 group. Instead, we make the _previous_ object that we saw, and
   // that is why this is a big switch statement on mType (which is the _previous_ type we read).
   // All the parameters we would need to build that previous object are now latched into the
   // state variables like D, Color, N, LTypeName etc.
   string? Type {
      set {
         switch (mType) {
            case "ARC": Add (Poly.Arc (Center, Radius, StartAng, EndAng, true)); break;
            case "CIRCLE": Add (Poly.Circle (Center, Radius)); break;
            case "DIMENSION": Add (new E2Dimension (Layer, mDwg.GetBlock (Name)!.Ents)); break;
            case "ELLIPSE": AddEllipse (Pt0, MajorAxis, AxisRatio, TRange); break;
            case "LINE":
               var line = Poly.Line (Pt0, Pt1);
               // Try to find bend info among mXData entries
               var (ba, radius, kfactor) = (double.NaN, 0.0, 0.42);
               foreach (var s in mXData) {
                  if (s.StartsWith ("BEND_ANGLE:")) ba = s[11..].ToDouble ().D2R ();
                  else if (s.StartsWith ("BEND_RADIUS:")) radius = s[12..].ToDouble ();
                  else if (s.StartsWith ("K_FACTOR:")) kfactor = s[9..].ToDouble ();
               }
               if (!ba.IsNaN ()) {
                  Add (new E2Bendline (mDwg, line.Pts, ba, radius, kfactor));
                  mXData.Clear ();
               } else Add (line);
               break;

            case "POINT": Add (new E2Point (Layer, Pt0) { Color = GetColor () }); break;
            case "POLYLINE": mClosedPoly = (Flags & 1) > 0; break;
            case "SEQEND": AddPolyline (); break;
            case "VERTEX": mVertex.Add (new (Pt0, Flags, Bulge)); break;
            case "BLOCK": (mBlockEnts, mBlockName, mBlockPt) = ([], Name, Pt0); break;
            case "STYLE": mDwg.Add (new Style2 (Name, Font, Height, XScale, Angle)); break;
            case "TRACE" or "SOLID": Add (new E2Solid (Layer, [Pt0, Pt1, Pt2, Pt3]) { Color = GetColor () }); break;

            case "ATTRIB":
               if ((Flags & 1) != 0) break;
               goto case "TEXT";

            case "ENDBLK":
               // Safe to add this to mDwg since blocks cannot contain nested blocks
               if (!sSkipBlocks.Contains (mBlockName))
                  mDwg.Add (new Block2 (mBlockName, mBlockPt, mBlockEnts ?? []));
               mBlockEnts = null;
               break;

            case "INSERT":
               if (!sSkipBlocks.Contains (Name))
                  Add (new E2Insert (mDwg, Layer, Name, Pt0, Angle, XScale, YScale));
               break;

            case "LWPOLYLINE":
               mClosedPoly = (Flags & 1) > 0;
               for (int i = 0; i < X0Set.Count; i++)
                  mVertex.Add (new (new (X0Set[i], Y0Set[i]), 0, D2Set.SafeGet (i)));
               AddPolyline ();
               break;

            case "LAYER":
               var layer = new Layer2 (Name, GetColor (), GetLType (LTypeName)) { IsVisible = (Flags & 1) != 1 };
               if (mLayers.TryAdd (Name, layer)) mDwg.Add (layer);
               break;

            case "MTEXT":
               int n = I1; if (n >= 7) n += 3;
               ETextAlign align = (ETextAlign)n;
               double angle = Angle;
               if (!(D[11].IsZero () && D[21].IsZero ())) angle = Math.Atan2 (D[21], D[11]);
               Add (AddMText (Layer, Style, Text, Pt0, Height, angle, align));
               break;

            case "SPLINE":
               if (X0Set.Count > 0 && D0Set.Count > 0) {
                  var pts = X0Set.Zip (Y0Set).Select (a => new Point2 (a.First, a.Second)).ToImmutableArray ();
                  var knots = D0Set.ToImmutableArray ();
                  var weights = D1Set.Count > 0 ? D1Set.ToImmutableArray () : [];
                  var spline = new Spline2 (pts, knots, weights);
                  Add (new E2Spline (Layer, spline));
               }
               break;

            case "TEXT":
               int h = HAlign > 2 ? 0 : HAlign.Clamp (0, 2), v = 3 - VAlign.Clamp (0, 3);
               align = (ETextAlign)(v * 3 + h + 1);
               Point2 pos = align == ETextAlign.BaseLeft ? Pt0 : Pt1;
               Add (new E2Text (Layer, Style, Clean (Text, mSB), pos, Height, Angle, Oblique, XScale, align) { Color = GetColor () });
               break;

            case "ENDSEC" or "SECTION" or "TABLE" or "VPORT" or "ENDTAB" or "VIEW" or "APPID"
               or "BLOCK_RECORD" or "UCS" or "3DFACE" or "3DSOLID" or "VIEWPORT" or "IMAGE"
               or "ACAD_TABLE" or "OLE2FRAME" or "Point2" or "ACAD_PROXY_ENTITY" or "ACAD_LINE"
               or "ACAD_CIRCLE" or "TCPOINTENTITY" or "ASSURFACE" or "WIPEOUT" or "BODY"
               or "SURFACE"or "REGION":
               break;

            default:
               if (!Lib.Testing && mType != null && mUnsupported.Add (mType))
                  Lib.Trace ($"DXFReader: {mType} unsupported in {mFile}\n");
               break;
         }

         // Now that we've constructed the previous object, we can store the incoming Type value
         // to start preparing for the next.
         mType = value;
         miMultiVertex = mType == "LWPOLYLINE";
         // For each entity type, there are some 'optional' codes that we may not find in the DXF.
         // Prepare for that contingency by resetting those optional values to zero. It's possible
         // (depending on the entity type) that some of the codes below that we are resetting to zero
         // are not optional, but mandatory. No harm done, those will get overwritten shortly as
         // we read in the actual group codes and value from the DXF
         I0 = I1 = I2 = I3 = 0; ZDir = 1;
         if (mType is not ("VERTEX" or "SEQEND")) ColorNo = 256;
         D[11] = D[21] = D[50] = D[51] = D[41] = D[42] = 0; Invisible = false;
         S[2] = S[3] = S[7] = "";
         X0Set.Clear (); Y0Set.Clear ();
         D0Set.Clear (); D1Set.Clear (); D2Set.Clear ();
      }
   }
   static HashSet<string> mUnsupported = [];
   bool miMultiVertex;

   // Convert the special text in the DXF to a bend line, unless is match with the sBend format
   void ProcessBendText () {
      var ents = mDwg.Ents;
      List<Ent2> bend = [], rmv = [];
      foreach (var e2t in ents.OfType<E2Text> ()) {
         var match = sBend.Match (e2t.Text);
         if (!match.Success) continue;
         var e2p = ents.OfType<E2Poly> ().Where (a => a.Poly.IsLine).MinBy (a => a.Poly.GetDistance (e2t.Pt).Dist);
         if (e2p == null) continue;
         rmv.AddRange (e2t, e2p);
         double angle = match.Groups[1].Value.ToDouble ().D2R ().Clamp (-Lib.PI, Lib.PI);
         double radius = match.Groups[2].Value.ToDouble ();
         double kfactor = match.Groups[3].Value.ToDouble ();
         if (kfactor > 0.501) kfactor /= 2;
         bend.Add (new E2Bendline (mDwg, e2p.Poly.Pts, angle, radius, kfactor));
      }
      foreach (var a in rmv) ents.Remove (a);
      foreach (var b in bend) ents.Add (b);
   }

   // DXF group value storage --------------------------------------------------
   // These variables store the values read in from the DXF groups
   string[] S = new string[10];
   double[] D = new double[53];
   List<double> X0Set = [], Y0Set = [];
   List<double> D0Set = [], D1Set = [], D2Set = [];
   bool Invisible;
   int ColorNo;                        // Group 62 value

   // Aliases ------------------------------------------------------------------
   string Text => S[1];
   string Name => S[2];
   string Font => S[3];
   string LTypeName => S[6] ?? "CONTINUOUS";
   string LayerName => S[8];

   double X1 => D[11]; double Y1 => D[21]; Point2 Pt1 => new (X1, Y1);
   double X2 => D[12]; double Y2 => D[22]; Point2 Pt2 => new (X2, Y2);
   double X3 => D[13]; double Y3 => D[23]; Point2 Pt3 => new (X3, Y3);
   double X4 => D[14]; double Y4 => D[24];
   Point2 Pt0 => new (X0Set.SafeGet (0), Y0Set.SafeGet (0)); // Group values 10,20 converted to a Point2
   Point2 Center => Pt0;               // ARC, CIRCLE
   double Radius => D[40];             // ARC, CIRCLE
   double StartAng => D[50].D2R ();    // ARC
   double EndAng => D[51].D2R ();      // ARC
   double Oblique => D[51].D2R ();     // TEXT
   Vector2 MajorAxis => new (X1, Y1);  // ELLIPSE
   double AxisRatio => D[40];          // ELLIPSE
   (double, double) TRange => (D[41], D[42]);   // ELLIPSE
   double Height => D[40];             // MTEXT
   double Angle => D[50].D2R ();       // MTEXT
   double XScale => D[41] == 0 ? 1 : D[41];  // INSERT
   double YScale => D[42] == 0 ? 1 : D[42];  // INSERT
   int HAlign => I2;                   // TEXT
   int VAlign => I3;                   // TEXT

   // Helpers ------------------------------------------------------------------
   int Vn => V.ToInt ();            // Group value, converted to integer
   double Vf => double.Parse (V);   // Group value, converted to double
   Layer2 Layer => mLayers.GetValueOrDefault (LayerName) ?? mDwg.CurrentLayer;
   Style2 Style => mDwg.GetStyle (S[7]) ?? mDwg.GetStyle ("STANDARD")!;

   // Private data -------------------------------------------------------------
   // This is built up as we read VERTEX nodes (as part of a POLYLINE entity)
   // Once we have finished, we make a Poly object from this data and clear this list
   List<Vertex> mVertex = [];
   // When we're constructing a Block, this list gathers the entities that should go into
   // that block. If this is list is null, we're adding entities directly into the drawing
   List<Ent2>? mBlockEnts;
   string mBlockName = "*";         // Name of the block we're building
   Point2 mBlockPt;                 // Insertion point of the block
   bool? mClosedPoly;               // NULL=not reading polyline, true=reading closed poly, false=reading open poly

   string? mType;                   // The _previous_ Type (0 group entity) that we saw
   readonly Dictionary<string, Layer2> mLayers = [];  // Dictionary mapping layer names to layer objects
   readonly PolyBuilder mPolyBuilder = new ();

   // Storage area for group codes
   double ZDir = 1;                 // Set to +1 or -1 depending on group 230 code (extrusion direction)
   int I0, I1, I2, I3;              // Integer value read from group 70 .. 73
   // A StringBuilder member used in various text compositions.
   readonly StringBuilder mSB = new ();
   // Raw 1000 group code strings from DXF (e.g., "BEND_ANGLE:90")
   List<string> mXData = [];
   // Aliases for the group codes (for better readability)
   int Flags => I0;
   double Bulge => D2Set.SafeGet (0);
   double D2 {
      get => D2Set.SafeGet (0);
      set {
         if (D2Set.Count == 0) D2Set.Add (0);
         D2Set[0] = value;
      }
   }

   static readonly Regex sBend = new (@"A([-+]?[0-9]*\.?[0-9]+)\s*R([0-9]*\.?[0-9]+)\s*K([0-9]*\.?[0-9]+)",
                                      RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
