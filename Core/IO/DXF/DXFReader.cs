// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DXFReader.cs
// ║║║║╬║╔╣║ Implements a DXF reader that reads directly from Span<byte>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Buffers;
namespace Nori;
using static EDXF;

/// <summary>DXFReader is used to read in a DXF file into a Dwg2</summary>
public partial class DXFReader {
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
            case 1: Text = V; break;
            case 3: FontName = V; break;
            case 8: if (mClosedPoly == null) LayerName = V; break;
            case 6: LTName = V; break;
            case 7: StyleName = V; break;
            case 9: HeaderVar (E); break;
            case 10: X0 = Vf; break; case 20: Y0 = Vf; break;
            case 11: X1 = Vf; break; case 21: Y1 = Vf; break;
            case 12: X2 = Vf; break; case 22: Y2 = Vf; break;
            case 13: X3 = Vf; break; case 23: Y3 = Vf; break;
            case 40: D40 = Vf; break; case 41: D41 = Vf; break; case 42: D42 = Vf; break;
            case 50: D50 = Vf; break; case 51: D51 = Vf; break;
            case 60: Invisible = Vn == 1; break;
            case 70: I0 = Vn; break; case 71: I1 = Vn; break; 
            case 72: I2 = Vn; break; case 73: I3 = Vn; break; 
            case 62: ColorNo = E switch { BYLAYER => 256, BYBLOCK => 257, _ => Vn }; break;
            case 230: ZDir = Vf < -0.999 ? -1 : 1; break;
            case 1000: if (mType == LINE) mXData.Add (V); break;

            case 2:
               switch (mType) {
                  case SECTION: HandleSection (E); break;
                  case TABLE: HandleTable (E); break;
                  default: Name = V; break;
               }
               break;

            default: UnhandledGroup (G); break;
         }
      }

      LinkDimensions ();
      ProcessBendText ();
      StitchDrawing ();
      if (mDwg.Layers.FirstOrDefault (a => a.Name == mCurrentLayer) is { } layer)
         mDwg.CurrentLayer = layer;
      return mDwg;
   }

   /// <summary>Helper to load a DXF file, given the filename</summary>
   public static Dwg2 Load (string file) => new DXFReader (file).Load ();

   // Implementation -----------------------------------------------------------
   static DXFReader () {
      Encoding.RegisterProvider (CodePagesEncodingProvider.Instance);
      sTypeIgnore = [.. Lib.ReadLines ("nori:DXF/entity-ignore.txt").Select (Enum.Parse<EDXF>)];
   }

   // Add a Poly (wrapping it in an E2Poly)
   void Add (Poly poly) => Add (new E2Poly (Layer, poly));

   // Adds an Ent2 to the drawing (after setting Color)
   void Add (Ent2 ent, bool setColor = true) {
      if (Invisible) return;
      if (setColor) ent.Color = GetColor (ColorNo);
      if (mBlockEnts != null) { ent.InBlock = true; mBlockEnts.Add (ent); }
      else mDwg.Add (ent);
   }

   // Add a set of ent2 to the drawing
   void Add (IEnumerable<Ent2> ents) => ents.ForEach (a => Add (a));

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
         mPB.Line (node.Moved (cen.X, cen.Y));
      }
      if (closed) mPB.Close ();
      Add (mPB.Build ());
   }

   void AddPolyline () {
      if (Vertex.Count > 0) {
         // If there are curve-fit vertices, remove the others
         if (Vertex.Any (a => (a.Flags & 8) != 0))
            Vertex = [.. Vertex.Where (a => (a.Flags & 8) != 0)];
         foreach (var (Pt, Flags, Bulge) in Vertex) {
            if (Bulge > 1e6 || Bulge.IsZero ()) mPB.Line (Pt);
            else mPB.Arc (Pt, Bulge);
         }
         if (mClosedPoly == true) mPB.Close ();
         Add (mPB.Build ());
         Vertex.Clear (); 
      }
      mClosedPoly = null;
   }

   // Builds an entity
   void BuildEnt (EDXF type) {
      if (type > _LASTENT) return;
      switch (type) {
         case ARC: Add (Poly.Arc (Pt0, Radius, D50.D2R (), D51.D2R (), true)); break;
         case BLOCK: (mBlockEnts, mBlockName, mBlockPt) = ([], Name, Pt0); break;
         case CIRCLE: Add (Poly.Circle (new (X0 * Scale * ZDir, Y0 * Scale), Radius)); break;
         case ELLIPSE: AddEllipse (Pt0, (Vector2)Pt1, D40, new (D41, D42)); break;
         case POINT: Add (new E2Point (Layer, Pt0)); break;
         case POLYLINE: mClosedPoly = (Flags & 1) != 0; break;
         case SEQEND: AddPolyline (); break;
         case STYLE: mDwg.Add (new Style2 (Name, FontName, D40 * Scale, D41 == 0 ? 1 : D41, D50.D2R ())); break;
         case TRACE or SOLID: Add (new E2Solid (Layer, [Pt0, Pt1, Pt2, Pt3])); break;
         case VERTEX: Vertex.Add ((Pt0, Flags, Bulge)); break;

         case ATTRIB:
            if ((Flags & 1) != 0) break;
            goto case TEXT;

         case DIMENSION:
            var dim = new E2Dimension (Layer);
            dim.SetDimSettings (mDwg.DimSettings);
            mDimBlocks.Add (dim, Name); Add (dim, false);
            break;

         case ENDBLK:
            // Safe to add this to mDwg since blocks cannot contain nested blocks
            if (!sSkipBlocks.Contains (mBlockName))
               mDwg.Add (new Block2 (mBlockName, mBlockPt, mBlockEnts ?? []));
            mBlockEnts = null;
            break;

         case INSERT:
            if (!sSkipBlocks.Contains (Name))
               Add (new E2Insert (mDwg, Layer, Name, Pt0, Angle, XScale, YScale), false);
            break;

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
               Add (new E2Bendline (mDwg, line.Pts, ba, radius, kfactor), false);
               mXData.Clear ();
            } else Add (line);
            break;

         case LWPOLYLINE:
            // mClosedPoly is used by AddPolyline, and writing to D42 below
            // will ensure the mD42 array has at least as many elements as mD40/mD41
            mClosedPoly = (Flags & 1) > 0; D42 = 0;  
            for (int i = 0; i < mX0.Count; i++)
               Vertex.Add (new (new (mX0[i] * Scale, mY0[i] * Scale), 0, mD42[i]));
            AddPolyline ();
            break;

         case MTEXT:
            ETextAlign align = (ETextAlign)(I1 >= 7 ? I1 + 3 : I1);
            double angle = (X1.IsZero () && Y1.IsZero ()) ? Angle : Math.Atan2 (Y1, X1);
            Add (MakeMText (Layer, Style, Text, Pt0, Height, angle, align));
            break;

         case SPLINE:
            if (mX0.Count > 0 && mD40.Count > 0) {
               E2Flags flags = 0;
               var pts = mX0.Zip (mY0).Select (a => new Point2 (a.First * Scale, a.Second * Scale)).ToImmutableArray ();
               var knots = mD40.ToImmutableArray ();
               var weights = mD41.Count > 0 ? mD41.ToImmutableArray () : [];
               var spline = new Spline2 (pts, knots, weights);
               if ((Flags & 1) > 0) flags |= E2Flags.Closed;
               if ((Flags & 2) > 0) flags |= E2Flags.Periodic;
               Add (new E2Spline (Layer, spline, flags));
            }
            break;

         case TEXT:
            int hAlign = I2 > 2 ? 0 : I2.Clamp (0, 2), vAlign = 3 - I3.Clamp (0, 3);
            align = (ETextAlign)(vAlign * 3 + hAlign + 1);
            Point2 pos = align == ETextAlign.BaseLeft ? Pt0 : Pt1;
            Add (new E2Text (Layer, Style, Clean (Text, mSB), pos, Height, Angle, Oblique, XScale, align));
            break;

         case LEADER or XLINE: break;

         default: Fatal ($"Unhandled entity {type}"); break;
      }
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
   public Color4 GetColor (int index) {
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
   double Scale = 1; 

   // This is called at the start of each section. 
   // For the HEADER section, this will read in a few variables. For other sections that we 
   // don't care about, we will simply skip the section (by running forward to the next ENDSEC)
   void HandleSection (EDXF s) {
      if (!sHandle.Contains (s)) {
         Skipping (s, "SECTION");
         while (Next ()) { if (G == 0 && E == ENDSEC) break; }
      } 
   }

   // This is called at the start of each table
   void HandleTable (EDXF t) {
      if (!sHandle.Contains (t)) {
         Skipping (t, "TABLE");
         while (Next ()) { if (G == 0 && E == ENDTAB) break; }
      } 
   }
   // These are the stuff we handle, and the stuff we knowingly ignore
   static readonly HashSet<EDXF>
      sHandle = [HEADER, TABLES, BLOCKS, ENTITIES, LTYPE, LAYER, STYLE, DIMSTYLE, LAYERS];

   // Load the dimensions
   void LinkDimensions () {
      List<Block2> blocks = [];
      foreach (var (dim, name) in mDimBlocks)
         if (dim.LoadEnts (mDwg, name) is { } block) blocks.Add (block);
      mDwg.RemoveBlocks (blocks);
   }

   // Extracts text from the encoded MTEXT string and returns the corresponding E2Text entities.
   IEnumerable<E2Text> MakeMText (Layer2 layer, Style2 style, string text, Point2 pos, double height, double angle, ETextAlign align) {
      var matches = sRxMText.Matches (text);
      if (matches.Count > 0) {
         mSB.Clear ();
         int last = 0; var tspan = text.AsSpan ();
         foreach (Match M in matches) {
            // Extract the raw-text1 from the given string.
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

         // Helpers ........................................
         void Append (string text1) => mSB.Append (text1);
         void Append2 (ReadOnlySpan<char> text2) => mSB.Append (text2);
      }

      // Now cleanup the raw-string by removing code-blocks ({...}) and split
      // them into multiple lines by the line-break (\P).
      string[] lines = text.Replace ("{", "").Replace ("}", "").Split ("\\P");
      // Output a text entity for each line.
      double dyLine = 0;
      var mat = Matrix2.Rotation (pos, angle);
      foreach (var line in lines) {
         var pt = pos;
         if (!dyLine.IsZero ()) {
            pt = new (pt.X, pt.Y - dyLine);
            if (!angle.IsZero ()) pt *= mat;
         }
         var ent = new E2Text (layer, style, Clean (line, mSB), pt, height, angle, style.Oblique, style.XScale, align);
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
   static readonly Regex sRxMText = new (
     @"(\\[Ff][^|;]+((\|([bicp])\d+)+)?;)|" +   // Font name & style (e.g., \fTimes New Roman|b1|i0;)
     @"(\\[AHWCT](\d*?(\.\d+)?x?));|" +         // Height, Width, Alignment, Color codes like: \H3x; \H12.500; \W0.8x;
     @"(\\[LlOoKk])|" +                         // Underline, Overstrike, Strikethrough: \L \l \O \K
     @"\\U\+(?<hex4>[0-9A-Fa-f]{4})|" +         // Match 4 hex digits prefixed with \U+
     @"(\\S(?<fract>[^;]+[#/\^][^;]+);)",       // Stacking fractions like: \S+0.8^+0.1; \S+0.8#+0.1;
     RegexOptions.Compiled);

   // Reads the next group into mGroup and the value into mValue
   bool Next () {
      for (; ; ) {
         if (R.AtEndOfFile) return false;
         R.Read (out G).SkipToNextLine ();      // Read the group code
         R.ReadLineRange (out mSt, out mLen);   // Read the value
         if (mLen == 3 && D[mSt] == 'E' && D[mSt + 1] == 'O' && D[mSt + 2] == 'F') return false;
         if (G == 0 || !mSkipForward) { mSkipForward = false; return true; }
      }
   }
   bool mSkipForward;

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
   static readonly Regex sBend = new (@"A([-+]?[0-9]*\.?[0-9]+)\s*R([0-9]*\.?[0-9]+)\s*K([0-9]*\.?[0-9]+)",
      RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

   void StitchDrawing () {
      if (StitchThreshold <= 0) return;
      if (StitchThreshold > 0.009) new DwgStitcher (mDwg, 0.0001).Process ();
      new DwgStitcher (mDwg, StitchThreshold).Process ();
   }

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
            I0 = I1 = I2 = I3 = 0; D41 = D42 = D50 = D51 = X1 = Y1 = 0; 
            mX0.ClearFast (); mY0.ClearFast (); mXData.Clear ();
            mD40.ClearFast (); mD41.ClearFast (); mD42.ClearFast ();
            Invisible = false; ZDir = 1; StyleName = "";
            if (mClosedPoly == null) ColorNo = 256;
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
   double Bulge => D42;
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
   bool Invisible;
   int I0, I1, I2, I3;
   double X1, Y1, X2, Y2, X3, Y3;
   double D40 { get => field; set => mD40.Add (field = value); }
   double D41 { get => field; set => mD41.Add (field = value); }
   double X0 { get => field; set => mX0.Add (field = value); } 
   double Y0 { get => field; set => mY0.Add (field = value); }
   List<double> mX0 = [], mY0 = [], mD40 = [], mD41 = [], mD42 = [];
   string Name = "", LTName = "", StyleName = "", FontName = "", Text = "";
   List<(Point2 Pt, int Flags, double Bulge)> Vertex = [];
   bool? mClosedPoly;   // NULL=not making POLYLINE, true/false=making polyline
   double ZDir = 1;

   double D42 {
      get => field;
      set {
         while (mD42.Count < mX0.Count - 1) mD42.Add (0);
         mD42.Add (field = value);
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

   partial void Skipping (EDXF e, string name);
   partial void UnhandledGroup (int g);
   partial void UnknownHeaderVar (string s);
   partial void Warn (string s);

   // Private data -------------------------------------------------------------
   readonly Dwg2 mDwg = new ();  // The drawing we're building
   readonly byte[] D;            // The raw data of the line
   readonly UTFReader R;         // The UTFReader used to read the file
   int G;                        // Group code
   int mSt, mLen;                // Start and length of the current line
   static SearchValues<byte> sCRLF = SearchValues.Create (13, 10);
   Encoding mEncoding = Encoding.UTF8;    // The encoding we're using
   List<string> mXData = [];     // Strings loaded from 1000 group (used for bend-data)
   readonly Dictionary<E2Dimension, string> mDimBlocks = [];   // Dimension blocks
   readonly StringBuilder mSB = new ();   // StringBuilder used in multiple contexts
   readonly PolyBuilder mPB = new ();     // PolyBuilder used in multiple contexts
   List<Ent2>? mBlockEnts;       // Entities in block
   string mBlockName = "";       // Name of block we're reading
   Point2 mBlockPt;              // Block reference point

   // Blocks that can be ignored
   static readonly HashSet<string> sSkipBlocks = new (StringComparer.OrdinalIgnoreCase) {
      "*Model_Space", "*Paper_Space", "*Paper_Space0", "*MODEL_SPACE", "*PAPER_SPACE", "*PAPER_SPACE0"
   };
}
