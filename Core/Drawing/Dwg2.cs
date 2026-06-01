// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dwg2.cs
// ║║║║╬║╔╣║ Implements the Dwg class, representing a 2D drawing with different types of entities
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Dwg2 ---------------------------------------------------------------------------------
/// <summary>Class to represent a drawing in 2D</summary>
[EPropClass]
public partial class Dwg2 {
   // Constructors -------------------------------------------------------------
   public Dwg2 () => Ents.Subscribe (OnEntsChanged);

   // Properties ---------------------------------------------------------------
   /// <summary>The bounding rectangle of the drawing</summary>
   public Bound2 Bound {
      get {
         if (mBound.IsEmpty) {
            if (mEnts.Count == 0) mBound = new (-60, -30, 360, 180); // Default (visible) _empty_ drawing extents
            else mBound = new (mEnts.Select (a => a.Bound));
         }
         return mBound;
      }
   }
   Bound2 mBound = new ();

   /// <summary>The list of blocks in the drawing</summary>
   /// New blocks are added by calling Add(Block2)
   public IReadOnlyList<Block2> Blocks => mBlocks ?? [];
   List<Block2>? mBlocks;

   /// <summary>The current dimension style of the drawing</summary>
   /// Reading this will always return a valid DimStyle2 object: if no dimension styles exist yet,
   /// one called "STANDARD" will be created with default values and returned. When writing to this
   /// property, ensure the DimStyle2 exists in the current DimStyles list (otherwise the write 
   /// is ignored)
   [DebuggerBrowsable (DebuggerBrowsableState.Never)]
   public DimStyle2 CurrentDimStyle {
      get {
         if (DimStyles.Count == 0) Add (new DimStyle2 ("STANDARD", CurrentStyle));
         return mCurrentDimStyle ??= DimStyles[0];
      }
      set {
         if (!DimStyles.Contains (value)) { Lib.Suspicious (); return; }
         if (Lib.SetNR (ref mCurrentDimStyle, value)) Notify (EProp.CurrentDimStyle);
      }
   }
   DimStyle2? mCurrentDimStyle;

   /// <summary>Returns the current text style</summary>
   /// Reading this will always returna a valid Style2 object. If no text styles exist yet,
   /// one called "STANDARD" will be created with default values and returned. When setting
   /// this property, ensure the Style2 exists in the Styles list (otherwise the write is ignored)
   [DebuggerBrowsable (DebuggerBrowsableState.Never)]
   public Style2 CurrentStyle {
      get {
         if (Styles.Count == 0) Add (new Style2 ("STANDARD", "SIMPLEX", 0, 1, 0));
         return mCurrentStyle ??= Styles[0];
      }
      set {
         if (!Styles.Contains (value)) { Lib.Suspicious (); return; }
         if (Lib.SetNR (ref mCurrentStyle, value)) Notify (EProp.CurrentStyle);            
      }
   }
   Style2? mCurrentStyle;

   /// <summary>The list of entities in the drawing (active list, implements Observable(ListChange)</summary>
   public AList <Ent2> Ents => mEnts;
   readonly AList<Ent2> mEnts = [];

   /// <summary>Should the interior of the drawing be filled or not?</summary>
   public bool FillInterior {
      get => mFillInterior;
      set { if (Lib.Set (ref mFillInterior, value)) Notify (EProp.FillInterior); }
   }
   bool mFillInterior = true;

   /// <summary>File from which this drawing was loaded</summary>
   public string? Filename { get; set; }

   /// <summary>Contains the 'snap grid' settings for this Dwg</summary>
   public Grid2 Grid { get => mGrid ?? Grid2.Default; set { mGrid = value; Notify (EProp.Grid); } }
   Grid2? mGrid;

   /// <summary>The list of layers in the drawing</summary>
   [AuInclude]
   public Ledger<Layer2> Layers {
      get {
         return field ??= new Ledger<Layer2> (Changed) {
            Validator = IsValid, Keyer = a => a.Name,
            Maker = () => new ("0", Color4.Black, ELineType.Continuous)
         };

         // Helpers ........................................
         // Update entities when Layer is replaced
         void Changed (LChange<Layer2> c) {
            if (c.Kind == ELChange.Replace)
               new RelayerEnts (DeepEnumEnts ().Where (a => a.Layer == c.OldValue), c.NewValue!).Push ();
         }

         // Check if a Layer can be removed/replaced
         object? IsValid (LChange<Layer2> c) {
            if (c.Kind is ELChange.Remove) {
               Layer2 layer = c.OldValue!;
               if (DeepEnumEnts ().Any (a => a.Layer == layer)) return EError.InUse;
            }
            return null;
         }
      }
   }

   /// <summary>The list of dimension styles in the drawing</summary>
   /// New dimension styles are added by calling Add(DimStyle2)
   public IReadOnlyList<DimStyle2> DimStyles => mDimStyles ?? [];
   List<DimStyle2>? mDimStyles;

   /// <summary>List of styles in the drawing</summary>
   /// New blocks are added by calling Add(Style2)
   public IReadOnlyList<Style2> Styles => mStyles ?? [];
   List<Style2>? mStyles;

   /// <summary>Enumerates the underlying Poly from each of the E2Poly entities in the drawing</summary>
   public IEnumerable<Poly> Polys => mEnts.OfType<E2Poly> ().Select (a => a.Poly);
   /// <summary>Enumerates the locations of E2Point entities from the drawing</summary>
   public IEnumerable<Point2> Points => mEnts.OfType<E2Point> ().Select (a => a.Pt);
   /// <summary>The list of dimensions in this drawing</summary>
   public IEnumerable<E2Dim> Dimensions => mEnts.OfType<E2Dim> ();

   // Methods ------------------------------------------------------------------
   /// <summary>Add an entity to the drawing</summary>
   public void Add (Ent2 ent) => mEnts.Add (ent);
   /// <summary>Add a Poly to the drawing, after wrapping it up in an E2Poly</summary>
   public void Add (Poly poly) => Add (new E2Poly (Layers.Current, poly));
   /// <summary>Adds a point to the drawing (after wrapping it in an E2Point)</summary>
   public void Add (Point2 pt) => Add (new E2Point (Layers.Current, pt));
   /// <summary>Add a set of entities into the drawing</summary>
   public void Add (IEnumerable<Ent2> ents) => ents.ForEach (Add);
   /// <summary>Adds a Block2 to the list of blocks in the drawing</summary>
   public void Add (Block2 block) { (mBlocks ??= []).Add (block); _blockMap = null; }
   /// <summary>Adds a dimension style to the list of styles in the drawing</summary>
   public void Add (DimStyle2 style) => (mDimStyles ??= []).Add (style); 
   /// <summary>Adds a style to the list of styles</summary>
   public void Add (Style2 style) => (mStyles ??= []).Add (style);

   [Obsolete ("Use Layers.Add instead (REMOVE 2026.07.01)")]
   public void Add (Layer2 layer) => Layers.Add (layer);
   [Obsolete ("Use Layers.Current instead (REMOVE 2026.07.01)")]
   public Layer2 CurrentLayer { get => Layers.Current; set => Layers.Current = value; }
   [Obsolete ("Use Layers[N]=newLayer instead (REMOVE 2026.07.01)")]
   public void UpdateLayer (Layer2 oldLayer, Layer2 newLayer) {
      int n = Layers.IndexOf (oldLayer);
      if (n != -1) Layers[n] = newLayer; else Layers.Add (newLayer);
   }

   /// <summary>Marks the inner/outer polylines of the drawing</summary>
   public bool MarkInOut () {
      if (Ents.OfType<E2Poly> ().MaxBy (a => a.Bound.Area) is not { } outer) return false;
      if (!outer.Poly.IsClosed) return false;
      outer.IsOuter = true; var bound = outer.Bound;
      foreach (var inner in Ents.OfType<E2Poly> ()) {
         if (inner == outer) continue;
         if (!bound.Contains (inner.Bound)) return false;
         inner.IsOuter = false;
      }
      return true; 
   }

   /// <summary>Removes an "existing" entity from the drawing</summary>
   /// If the entity does not exist in the drawing, this throws an exception
   public void Remove (Ent2 ent) => Lib.Check (mEnts.Remove (ent), "Coding Error");

   /// <summary>Removes a block from the drawing</summary>
   /// If any Insert reference this block, this will raise an exception
   public void Remove (Block2 block) {
      if (DeepEnumEnts ().OfType<E2Insert> ().Any (a => a.Block == block))
         throw new ArgumentException ("Cannot remove a Block2 that is in use");
      if (mBlocks?.Remove (block) != true) Lib.Suspicious ();
      _blockMap = null;
   }

   /// <summary>Removes a text-style from the drawing</summary>
   /// If this Style2 is in use (in any E2Text or E2Dim entities), this will raise an exception
   public void Remove (Style2 style) {
      bool used = false;
      foreach (var ent in DeepEnumEnts ()) {
         if (ent is E2Text text && text.Style == style) used = true;
         if (ent is E2Dim dim && dim.Style.Style == style) used = true;
         if (used) throw new ArgumentException ("Cannot remove a Style2 that is in use");
      }
      if (mStyles?.Remove (style) != true) Lib.Suspicious ();
      if (mCurrentStyle == style) { mCurrentStyle = Styles.SafeGet (0); Notify (EProp.CurrentStyle); }
   }

   /// <summary>Removes a DimStyle2 from the drawing</summary>
   /// If this DimStyle2 is in use (in any E2Dim entities), this will raise an exception
   public void Remove (DimStyle2 style) {
      if (DeepEnumEnts ().OfType<E2Dim> ().Any (a => a.Style == style))
         throw new ArgumentException ("Cannot remove a DimStyle2 that is in use");
      if (!mDimStyles?.Remove (style) == true) Lib.Suspicious ();
      if (mCurrentDimStyle == style) { mCurrentDimStyle = DimStyles.SafeGet (0); Notify (EProp.CurrentDimStyle); }
   }

   /// <summary>Removes set of "existing" entities from the drawing</summary>
   /// The entities are supposed to be 'ordered' in the same ordering as in the mEnts array.
   /// This makes it possible for the removal of all entities to happen in O(n) time, rather
   /// than the O(n^2) that a set of repeated searches would require. If any of the entities
   /// in the input set do not belong in the drawing, or the input set is not in the same
   /// ordering as the mEnts array, this throws an exception in debug mode.
   public void RemoveOrdered (IList<Ent2> set) {
      int idx = set.Count - 1; if (idx < 0) return;
      Ent2 next = set[idx];
      for (int i = mEnts.Count - 1; i >= 0; i--) {
         if (ReferenceEquals (mEnts[i], next)) {
            mEnts.RemoveAt (i); if (--idx < 0) return;
            next = set[idx];
         }
      }
      Lib.Check (false, "Coding error");
   }

   /// <summary>Gets a block given the name (could return null if the name does not exist)</summary>
   public Block2? GetBlock (string name)
      => (_blockMap ??= Blocks.ToDictionary (a => a.Name, StringComparer.OrdinalIgnoreCase)).GetValueOrDefault (name);
   Dictionary<string, Block2>? _blockMap;

   /// <summary>Gets a style, given the name</summary>
   /// If the particular named style does not exist, this returns null.
   /// A good fallback for the caller is to use Dwg.CurrentStyle (which is never null)
   public Style2? GetStyle (string name) 
      => Styles.FirstOrDefault (a => a.Name.EqIC (name));

   /// <summary>Gets a dimension style, given the name</summary>
   /// If the given named style does not exist, this returns null. 
   /// A good fallback for the caller is to use Dwg.CurrentDimStyle (which is never null)
   public DimStyle2? GetDimStyle (string name) 
      => DimStyles.FirstOrDefault (a => a.Name.EqIC (name));

   /// <summary>Picks the closest E2Poly and returns some rich information about the pick</summary>
   public bool PickPoly (Point2 pt, double aperture, out TPolyPick pick) {
      E2Poly? e2p = PickPoly (pt, aperture, out int nSeg, out double lie);
      if (e2p == null) { pick = new (); return false; }
      Seg seg = e2p.Poly[nSeg];
      int nNode = (lie > 0.5 ? 1 : 0) + nSeg;

      Poly.ECornerOpFlags flags = Poly.ECornerOpFlags.None;
      bool left = seg.IsPointOnLeft (pt);
      if (nNode == nSeg) flags |= Poly.ECornerOpFlags.NearLeadOut;
      if (left) flags |= Poly.ECornerOpFlags.Left;
      var vec = seg.B - seg.A;
      if (Math.Abs (vec.X) > Math.Abs (vec.Y)) flags |= Poly.ECornerOpFlags.Horz;
      int nC = e2p.Poly.Count;
      if (e2p.Poly.IsClosed || (nNode > 0 && nNode < nC)) {
         Seg other = e2p.Poly[(nSeg == nNode) ? (nSeg - 1 + nC) % nC : (nSeg + 1) % nC];
         if (left == other.IsPointOnLeft (pt)) flags |= Poly.ECornerOpFlags.SameSideOfBothSegments;
      }
      pick = new (e2p, nSeg, nNode, flags);
      return true;
   }

   /// <summary>Picks the closest E2Poly, and returns the poly and clicked seg's index</summary>
   public E2Poly? PickPoly (Point2 pt, double aperture, out int nSeg, out double lie) {
      E2Poly? e2p = null; nSeg = 0;
      foreach (var ent in mEnts.OfType<E2Poly> ()) {
         if (!ent.Bound.InflatedL (aperture).Contains (pt)) continue;
         var (dist, nseg) = ent.Poly.GetDistance (pt);
         if (dist < aperture) (e2p, nSeg, aperture) = (ent, nseg, dist);
      }
      lie = e2p != null ? e2p.Poly[nSeg].GetLie (pt) : 0;
      return e2p;
   }

   public void RemoveBlocks (IEnumerable<Block2> blocks) {
      blocks.ForEach (b => mBlocks?.Remove (b));
      if (mBlocks?.Count == 0) mBlocks = null;
   }

   /// <summary>Purges layers, blocks, styles that are unused</summary>
   public Dwg2 Purge () {
      HashSet<Style2> styles = [];
      HashSet<DimStyle2> dimStyles = [];
      HashSet<Block2> blocks = [];
      HashSet<Layer2> layers = [];
      foreach (var ent in DeepEnumEnts ()) {
         layers.Add (ent.Layer);
         if (ent is E2Text text) styles.Add (text.Style);
         if (ent is E2Dim dim) { styles.Add (dim.Style.Style); dimStyles.Add (dim.Style); }
         if (ent is E2Insert insert) blocks.Add (insert.Block);
      }
      if (layers.Count > 0) layers.Add (Layers.Current);
      mStyles?.RemoveAll (a => !styles.Contains (a));
      mBlocks?.RemoveAll (a => !blocks.Contains (a)); 
      // mDimStyles?.RemoveAll (a => !dimStyles.Contains (a));      
      Layers.RemoveAll (a => !layers.Contains (a));
      return this;
   }

   /// <summary>Selects the given entity (and optionally deselects the others that are selected)</summary>
   public void Select (Ent2? ent, bool deselectOthers) {
      if (deselectOthers)
         mEnts.Where (a => a.IsSelected).ForEach (a => a.IsSelected = false);
      ent?.IsSelected ^= true; // Toggle selection
   }

   // Implementation -----------------------------------------------------------
   // Handles changes in the Ents list, and keeps the Bound up-to-date
   void OnEntsChanged (ListChange ch) {
      switch (ch.Action) {
         case ListChange.E.Added:
            // When adding the first entity, reset the bound (which was set to a
            // dummy vaue). Then, incrementally update the bound if it is valid
            if (mEnts.Count == 1) mBound = new ();
            if (!mBound.IsEmpty) mBound += mEnts[ch.Index].Bound;
            break;
         case ListChange.E.Removing:
            // When removing an entity, if that entity lies on the 'edge' of the
            // drawing, reset the bound for recompute
            var bound = mEnts[ch.Index].Bound.InflatedF (1.001);
            if (!mBound.Contains (bound)) mBound = new ();
            break;
         default: mBound = new (); break;
      }
   }

   public IEnumerable<Ent2> DeepEnumEnts () {
      foreach (var b in mBlocks ?? [])
         foreach (var ent in b.Ents) yield return ent;
      foreach (var ent in mEnts) yield return ent;
   }
}
#endregion
