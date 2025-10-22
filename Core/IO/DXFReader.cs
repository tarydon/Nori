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
   public DXFReader (string file)
      => mReader = new StreamReader (new FileStream (mFile = file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

   // Properties ---------------------------------------------------------------
   /// <summary>The Standard 256 ACAD Colors</summary>
   public static Color4[] ACADColors {
      get {
         if (sACADColors == null) {
            sACADColors = [..Lib.ReadLines ("nori:DXF/color.txt")
                            .Select (a => new Color4 (uint.Parse (a, NumberStyles.HexNumber) | 0xff000000))];
            Debug.Assert (sACADColors.Length == 256);
         }
         return sACADColors;
      }
   }
   static Color4[]? sACADColors;

   /// <summary>Darken all layer colors (to have a luminance of no more than 160)</summary>
   public bool DarkenColors;

   /// <summary>Convert white entities to black on import</summary>
   public bool WhiteToBlack;

   /// <summary>If set above zero, then polylines that touch are stitched together</summary>
   /// Two open polylines whose endpoints are closer than this threshold are
   /// joined together (as long as they are on the same layer). If there are open
   /// polylines whose start and end are within this threshold of each other they
   /// are closed.
   public double StitchThreshold;

   // Methods ------------------------------------------------------------------
   /// <summary>Parse the file, Build the DXF and return it</summary>
   public Dwg2 Load () {
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
            case 9: var key = V; Next (); HeaderVar (key); break;
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
      LinkDimensions ();
      ProcessBendText ();
      StitchDrawing ();
      return mDwg;
   }

   /// <summary>Convert an AutoCAD color to a Color4</summary>
   public Color4 GetColor (int index) {
      if (index == 256) return Color4.Nil;
      var color = ACADColors[index.Clamp (0, 255)];
      if (WhiteToBlack && color.EQ (Color4.White)) color = Color4.Black;
      if (DarkenColors) color = color.Darkened ();
      return color;
   }

   /// <summary>Helper to load a DXF file, given the filename</summary>
   public static Dwg2 Load (string name) => new DXFReader (name).Load ();

   // Building up the drawing --------------------------------------------------
   // Adds a Poly to the current layer
   void Add (Poly poly) => Add (new E2Poly (Layer, poly) { Color = GetColor () });

   void Add (Ent2 ent) {
      if (Invisible) return;
      if (mBlockEnts != null) { ent.InBlock = true; mBlockEnts.Add (ent); } else mDwg.Add (ent);
   }

   // Add a set of ent2 to the drawing
   void Add (IEnumerable<Ent2> ents) => ents.ForEach (Add);

   // Make an ellipse and adds it
   void AddEllipse (Point2 cen, Vector2 major, double ratio, (double, double) aRange) {
      Point2 east = cen + major;
      var (aStart, aEnd) = aRange;
      while (aEnd < aStart) aEnd += Lib.TwoPI;
      double R = major.Length, r = R * ratio, aSpan = aEnd - aStart;
      // Figure out the number of steps for discretization (5 degrees per step)
      int c = (int)Math.Max (4.0, (aSpan / (Math.PI / 36)).Round (0));
      double aStep = aSpan / c, rot = cen.AngleTo (east);
      bool closed = aSpan.EQ (2 * Math.PI);
      if (closed) c--;

      for (int i = 0; i <= c; i++) {
         var (sin, cos) = Math.SinCos (aStart + i * aStep);
         var node = new Point2 (R * cos, r * sin * ZDir).Rotated (rot);
         mPolyBuilder.Line (node.Moved (cen.X, cen.Y));
      }
      if (closed) mPolyBuilder.Close ();
      Add (mPolyBuilder.Build ());
   }

   // Extracts text from the encoded MTEXT string and returns the corresponding E2Text entities.
   IEnumerable<E2Text> AddMText (Layer2 layer, Style2 style, string text, Point2 pos, double height, double angle, ETextAlign align) {
      var matches = sRxMText.Matches (text);
      if (matches.Count > 0) {
         mSB.Clear ();
         int last = 0; var tspan = text.AsSpan ();
         foreach (Match M in matches) {
            // Extract the raw-text from the given string.
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

         void Append (string text) => mSB.Append (text);
         void Append2 (ReadOnlySpan<char> text) => mSB.Append (text);
      }
      // Now cleanup the raw-string by removing code-blocks ({...}) and split
      // them into multiple lines by the line-break (\P).
      string[] lines = text.Replace ("{", "").Replace ("}", "").Split ("\\P");
      // Output a text entity for each line.
      double dyLine = 0;
      var mat = Matrix2.Rotation (pos, angle);
      for (int i = 0; i < lines.Length; i++) {
         var pt = pos;
         if (!dyLine.IsZero ()) {
            pt = new (pt.X, pt.Y - dyLine);
            if (!angle.IsZero ()) pt *= mat;
         }
         var ent = new E2Text (layer, style, Clean (lines[i], mSB), pt, height, angle, style.Oblique, style.XScale, align) { Color = GetColor () };
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
   static Regex sRxMText = new (
     @"(\\[Ff][^|;]+((\|([bicp])\d+)+)?;)|" +   // Font name & style (e.g., \fTimes New Roman|b1|i0;)
     @"(\\[AHWCT](\d*?(\.\d+)?x?));|" +         // Height, Width, Alignment, Color codes like: \H3x; \H12.500; \W0.8x;
     @"(\\[LlOoKk])|" +                         // Underline, Overstrike, Strikethrough: \L \l \O \K
     @"\\U\+(?<hex4>[0-9A-Fa-f]{4})|" +         // Match 4 hex digits prefixed with \U+
     @"(\\S(?<fract>[^;]+[#/\^][^;]+);)",       // Stacking fractions like: \S+0.8^+0.1; \S+0.8#+0.1;
     RegexOptions.Compiled);

   // Make a Polyline (called after a POLYLINE or LWPOLYLINE entity)
   void AddPolyline () {
      if (mVertex.Count > 0) {
         // If there are curve-fit vertices, remove the others
         if (mVertex.Any (a => (a.Flags & 8) != 0))
            mVertex = mVertex.Where (a => (a.Flags & 8) != 0).ToList ();
         for (int i = 0; i < mVertex.Count; i++) {
            if (mVertex[i].Bulge > 1e6 || mVertex[i].Bulge.IsZero ()) mPolyBuilder.Line (mVertex[i].Pt);
            else mPolyBuilder.Arc (mVertex[i].Pt, mVertex[i].Bulge);
         }
         if (mClosedPoly == true) mPolyBuilder.Close ();
         Add (mPolyBuilder.Build ());
         mVertex.Clear (); mClosedPoly = null;
      }
   }

   // Implementation -----------------------------------------------------------
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
            ;
         }
         sb.Append (ch);
      }
      return sb.ToString ();
   }

   // Convert the currently read-in color number into a Color4
   Color4 GetColor () => GetColor (ColorNo);

   /// <summary>Converts a DXF linetype string to the corresponding ELineType enum value </summary>
   ELineType GetLType (string lt) => lt.ToUpper () switch {
      "DOT" or "DOTTED" => ELineType.Dot,
      "DASH" or "DASHED" => ELineType.Dash,
      "DASHDOT" => ELineType.DashDot,
      "DIVIDE" or "DASHDOTDOT" or "DASH2DOT" => ELineType.DashDotDot,
      "CENTER" => ELineType.Center,
      "BORDER" => ELineType.Border,
      "HIDDEN" => ELineType.Hidden,
      "DASH2" => ELineType.Dash2,
      "PHANTOM" => ELineType.Phantom,
      _ => ELineType.Continuous,
   };

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
   static HashSet<string> sSections = ["TABLES", "HEADER", "BLOCKS", "ENTITIES"];

   /// <summary>Used to process a header variable</summary>
   void HeaderVar (string key) {
      switch (key) {
         case "$MEASUREMENT": Scale = Vn == 0 ? 25.4 : 1; break;
      }
   }

   // Reads the next group into mGroup and the value into mValue
   bool Next () {
      mnLine += 2;
      var line1 = mReader.ReadLine (); if (line1 == null) return false;
      var line2 = mReader.ReadLine (); if (line2 == null) return false;
      G = int.Parse (line1); V = line2;
      return V != "EOF";
   }
   int mnLine = -1;

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
            case "CIRCLE": Add (Poly.Circle (Center.X * ZDir, Center.Y, Radius)); break;
            case "ELLIPSE": AddEllipse (Pt0, MajorAxis, AxisRatio, TRange); break;
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

            case "DIMENSION":
               var dim = new E2Dimension (Layer);
               mDimBlocks.Add (dim, Name);
               Add (dim);
               break;

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

            case "LINE":
               var line = Poly.Line (Pt0, Pt1);
               // Try to find bend info among mXData entries
               var (ba, radius, kfactor) = (double.NaN, 0.0, 0.42);
               foreach (var s in mXData) {
                  if (s.StartsWith ("BEND_ANGLE:")) ba = s[11..].ToDouble ().D2R ();
                  else if (s.StartsWith ("BEND_RADIUS:")) radius = s[12..].ToDouble () * Scale;
                  else if (s.StartsWith ("K_FACTOR:")) kfactor = s[9..].ToDouble ();
               }
               if (!ba.IsNaN ()) {
                  Add (new E2Bendline (mDwg, line.Pts, ba, radius, kfactor));
                  mXData.Clear ();
               } else Add (line);
               break;

            case "LWPOLYLINE":
               mClosedPoly = (Flags & 1) > 0;
               for (int i = 0; i < X0Set.Count; i++)
                  mVertex.Add (new (new (X0Set[i] * Scale, Y0Set[i] * Scale), 0, D2Set.SafeGet (i)));
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
                  var pts = X0Set.Zip (Y0Set).Select (a => new Point2 (a.First * Scale, a.Second * Scale)).ToImmutableArray ();
                  var knots = D0Set.ToImmutableArray ();
                  var weights = D1Set.Count > 0 ? D1Set.ToImmutableArray () : [];
                  var spline = new Spline2 (pts, knots, weights);
                  E2Flags flags = 0;
                  if ((Flags & 1) > 0) flags |= E2Flags.Closed;
                  if ((Flags & 2) > 0) flags |= E2Flags.Periodic;
                  Add (new E2Spline (Layer, spline, flags));
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
   Dictionary<E2Dimension, string> mDimBlocks = [];
   static HashSet<string> mUnsupported = [];
   bool miMultiVertex;

   // Load the dimensions
   void LinkDimensions () {
      List<Block2> blocks = [];
      foreach (var (dim, name) in mDimBlocks)
         if (dim.LoadEnts (mDwg, name) is { } block) blocks.Add (block);
      mDwg.RemoveBlocks (blocks);
   }

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

   void StitchDrawing () {
      if (StitchThreshold <= 0) return;
      // if (StitchThreshold > 0.009) new DwgStitcher (mDwg, 0.0001).Process (); REMOVETHIS
      new DwgStitcher (mDwg, StitchThreshold).Process ();
   }

   // Nested types -------------------------------------------------------------
   // A structure read in from a 'VERTEX' entity
   readonly struct Vertex {
      public Vertex (Point2 pt, int flags, double bulge) => (Pt, Flags, Bulge) = (pt, flags, bulge);
      public readonly Point2 Pt;
      public readonly int Flags;
      public readonly double Bulge;
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

   double X1 => D[11]; double Y1 => D[21]; Point2 Pt1 => new (X1 * Scale, Y1 * Scale);
   double X2 => D[12]; double Y2 => D[22]; Point2 Pt2 => new (X2 * Scale, Y2 * Scale);
   double X3 => D[13]; double Y3 => D[23]; Point2 Pt3 => new (X3 * Scale, Y3 * Scale);
   double X4 => D[14]; double Y4 => D[24];
   Point2 Pt0 => new (X0Set.SafeGet (0) * Scale, Y0Set.SafeGet (0) * Scale); // Group values 10,20 converted to a Point2
   Point2 Center => Pt0;               // ARC, CIRCLE
   double Radius => D[40] * Scale;      // ARC, CIRCLE
   double StartAng => D[50].D2R ();    // ARC
   double EndAng => D[51].D2R ();      // ARC
   double Oblique => D[51].D2R ();     // TEXT
   Vector2 MajorAxis => new (X1 * Scale, Y1 * Scale);  // ELLIPSE
   double AxisRatio => D[40];          // ELLIPSE
   (double, double) TRange => (D[41], D[42]);   // ELLIPSE
   double Height => D[40] * Scale;             // MTEXT
   double Angle => D[50].D2R ();       // MTEXT
   double XScale => D[41] == 0 ? 1 : D[41];  // INSERT
   double YScale => D[42] == 0 ? 1 : D[42];  // INSERT
   int HAlign => I2;                   // TEXT
   int VAlign => I3;                   // TEXT
   double Scale = 1;

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

   [SuppressMessage ("Performance", "SYSLIB1045")]
   static readonly Regex sBend = new (@"A([-+]?[0-9]*\.?[0-9]+)\s*R([0-9]*\.?[0-9]+)\s*K([0-9]*\.?[0-9]+)",
                                      RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

   int G; string V = "";         // Current Group-Code and Group-Value (updated by Next())
   readonly Dwg2 mDwg = new ();   // The drawing we are building;
   readonly string mFile;        // The file we're reading from
   StreamReader mReader;         // Reader we're loading from
   static readonly HashSet<string> sSkipBlocks = [
      "*Model_Space", "*Paper_Space", "*Paper_Space0", "*MODEL_SPACE", "*PAPER_SPACE", "*PAPER_SPACE0"
   ];
}
