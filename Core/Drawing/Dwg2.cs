// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dwg2.cs
// ║║║║╬║╔╣║ Implements the Dwg class, representing a 2D drawing with different types of entities
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Dwg2 ---------------------------------------------------------------------------------
/// <summary>Class to represent a drawing in 2D</summary>
[EPropClass]
public partial class Dwg2 {
   public Dwg2 () => Ents.Subscribe (OnEntsChanged);

   // Properties ---------------------------------------------------------------
   /// <summary>The bounding rectangle of the drawing</summary>
   /// When the drawing is empty, a special case bounding rectangle of (0,0,100,50) is returned
   public Bound2 Bound {
      get {
         if (mBound.IsEmpty) {
            if (mEnts.Count == 0) mBound = new (0, 0, 100, 50);
            else mBound = new (mEnts.Select (a => a.Bound));
            Lib.Trace ($"Bound: {mBound.Width.Round (0)}x{mBound.Height.Round (0)}");
         }
         return mBound;
      }
   }
   Bound2 mBound = new ();

   /// <summary>The current layer of the drawing</summary>
   public Layer2 CurrentLayer {
      get {
         if (mLayers.Count == 0) Add (new Layer2 ("0", Color4.Black, ELineType.Continuous));
         return mCurrentLayer ??= mLayers[0];
      }
      set {
         if (!Lib.Check (mLayers.Contains (value), "Invalid layer passed to CurrentLayer")) return;
         if (value != mCurrentLayer) { Notify (EProp.CurrentLayer); mCurrentLayer = value; }
      }
   }
   Layer2? mCurrentLayer;

   /// <summary>The list of entities in the drawing (active list, implements Observable(ListChange)</summary>
   public AList <Ent2> Ents => mEnts;
   AList<Ent2> mEnts = [];

   /// <summary>Should the interior of the drawing be filled or not?</summary>
   public bool FillInterior {
      get => mFillInterior;
      set { if (Lib.Set (ref mFillInterior, value)) Notify (EProp.FillInterior); }
   }
   bool mFillInterior = true;

   /// <summary>Contains the 'snap grid' settings for this Dwg</summary>
   public Grid2 Grid { get => mGrid ?? Grid2.Default; set { mGrid = value; Notify (EProp.Grid); } }
   Grid2? mGrid;

   /// <summary>The list of layers in the drawing</summary>
   public IReadOnlyList <Layer2> Layers => mLayers;
   List<Layer2> mLayers = [];

   /// <summary>The list of blocks in the drawing</summary>
   /// New blocks are added by calling Add(Block2)
   public IReadOnlyList<Block2> Blocks => mBlocks ?? [];
   List<Block2>? mBlocks;

   /// <summary>List of styles in the drawing</summary>
   /// New blocks are added by calling Add(Style2)
   public IReadOnlyList<Style2> Styles => mStyles ?? [];
   List<Style2>? mStyles;

   /// <summary>Enumerates the underlying Poly from each of the E2Poly entities in the drawing</summary>
   public IEnumerable<Poly> Polys => mEnts.OfType<E2Poly> ().Select (a => a.Poly);
   /// <summary>Enumerates the locations of E2Point entities from the drawing</summary>
   public IEnumerable<Point2> Points => mEnts.OfType<E2Point> ().Select (a => a.Pt);

   // Methods ------------------------------------------------------------------
   /// <summary>Add an entity to the drawing</summary>
   public void Add (Ent2 ent) => mEnts.Add (ent);

   /// <summary>Add a Poly to the drawing, after wrapping it up in an E2Poly</summary>
   public void Add (Poly poly) => Add (new E2Poly (CurrentLayer, poly));

   /// <summary>Add a set of entities into the drawing</summary>
   public void Add (IEnumerable<Ent2> ents) => ents.ForEach (Add);

   /// <summary>Add e layer into the drawing</summary>
   /// If the drawing already has e layer with the same name, the old layer will
   /// be replaced with new one. And corresponding layers of entities are updated.
   public void Add (Layer2 layer) {
      if (mLayers.FirstOrDefault (a => a.Name == layer.Name) is var old && old != null) {
         foreach (var ent in Ents.Where (e => e.Layer == old)) ent.Layer = layer;
         mLayers.Remove (old);
      }
      mLayers.Add (layer);
   }

   /// <summary>Adds a Block2 to the list of blocks in the drawing</summary>
   public void Add (Block2 block) { (mBlocks ??= []).Add (block); _blockMap = null; }

   /// <summary>Adds a style to the list of styles</summary>
   public void Add (Style2 style) {
      if (style.Name.IsBlank ()) return;
      (mStyles ??= []).Add (style); _styleMap = null;
   }

   /// <summary>Gets a block given the name (could return null if the name does not exist)</summary>
   public Block2? GetBlock (string name)
      => (_blockMap ??= Blocks.ToDictionary (a => a.Name, StringComparer.OrdinalIgnoreCase)).GetValueOrDefault (name);
   Dictionary<string, Block2>? _blockMap;

   /// <summary>Gets a style, given the name</summary>
   public Style2? GetStyle (string name) {
      if (_styleMap == null) {
         _styleMap = new Dictionary<string, Style2> (StringComparer.OrdinalIgnoreCase);
         foreach (var s in Styles) _styleMap.TryAdd (s.Name, s);
      }
      var style = _styleMap.GetValueOrDefault (name);
      if (style == null && name == "STANDARD") {
         (mStyles ??= []).Add (style = Style2.Default);
         _styleMap = null;
      }
      return style;
   }
   Dictionary<string, Style2>? _styleMap;

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

   /// <summary>Purges layers, blocks, styles that are unused</summary>
   public Dwg2 Purge () {
      HashSet<Style2> styles = [];
      HashSet<Block2> blocks = [];
      HashSet<Layer2> layers = [];
      foreach (var ent in DeepEnumEnts ()) {
         layers.Add (ent.Layer);
         if (ent is E2Text text) styles.Add (text.Style);
         if (ent is E2Insert insert) blocks.Add (insert.Block);
      }
      mStyles?.RemoveAll (a => !styles.Contains (a));
      mBlocks?.RemoveAll (a => !blocks.Contains (a));
      mLayers.RemoveAll (a => !layers.Contains (a));
      return this;
   }

   /// <summary>Selects the given entity (and optionally deselects the others that are selected)</summary>
   public void Select (Ent2? ent, bool deselectOthers) {
      if (deselectOthers) 
         mEnts.Where (a => a.IsSelected).ForEach (a => a.IsSelected = false);
      if (ent != null) ent.IsSelected = true;
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
            var bound = mEnts[ch.Index].Bound.InflatedF (1);
            if (!mBound.Contains (bound)) mBound = new ();
            break;
         default: mBound = new (); break;
      }
   }

   /// <summary>Enumerate all entities in the drawing, as well as entities in all the blocks</summary>
   IEnumerable<Ent2> DeepEnumEnts ()
      => Blocks.SelectMany (a => a.Ents).Concat (mEnts);
}
#endregion
