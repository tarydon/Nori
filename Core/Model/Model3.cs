// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Model3.cs
// ║║║║╬║╔╣║ Implements Model3, a 3D model with different types of entities (derived from Ent3)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Model3 -------------------------------------------------------------------------------
/// <summary>Represents a Model in 3D space (can contain surfaces, sheet-metal ents, wireframes etc)</summary>
public class Model3 {
   // Constructors -------------------------------------------------------------
   public Model3 () => mEnts.Subscribe (OnEntsChanged);

   // Properties ---------------------------------------------------------------
   /// <summary>The list of blocks in the drawing</summary>
   /// New blocks are added by calling Add(Block2)
   public IReadOnlyList<Block3> Blocks => mBlocks ?? [];
   List<Block3>? mBlocks;

   /// <summary>The Bound of this Model</summary>
   public Bound3 Bound => Bound3.Cached (ref mBound, () => new (mEnts.Select (e => e.Bound)));
   Bound3 mBound = new ();

   /// <summary>The set of entities in this Model</summary>
   public AList<Ent3> Ents => mEnts;
   readonly AList<Ent3> mEnts = [];

   // Methods ------------------------------------------------------------------
   /// <summary>Adds a Block2 to the list of blocks in the drawing</summary>
   public void Add (Block3 block) { (mBlocks ??= []).Add (block); }

   public bool CleanupBlocks () {
      if (mBlocks == null) return false;

      // First, remove blocks with no entities
      bool result = false;
      for (; ; ) {
         bool changed = false;
         for (int i = mEnts.Count - 1; i >= 0; i--) {
            if (mEnts[i] is not E3Insert ei) continue;
            switch (ei.Simplify ()) {
               case E3Insert.ESimplify.Remove: mEnts.RemoveAt (i); changed = true; break;
               case E3Insert.ESimplify.Update: changed = true; break;
            }
         }
         foreach (var block in mBlocks) {
            for (int i = block.Ents.Count - 1; i >= 0; i--) {
               if (block.Ents[i] is not E3Insert ei) continue;
               switch (ei.Simplify ()) {
                  case E3Insert.ESimplify.Remove: block.RemoveAt (i); changed = true; break;
                  case E3Insert.ESimplify.Update: changed = true; break;
               }
            }
         }
         if (!changed) break;
         result = true;
      }

      // If we are left with just one insert which has an identity (or even only a translation 
      // transform), flatten that
      if (mEnts.Count == 1 && mEnts[0] is E3Insert e2i) {
         if (!e2i.Xfm.HasMirroring) {
            mEnts.RemoveAt (0);
            foreach (var ent in e2i.Block.Ents) mEnts.Add (ent);
            result = true;
         }
      }

      HashSet<Block3> used = [];
      foreach (var ei in DeepEnumEnts ().OfType<E3Insert> ())
         used.Add (ei.Block);
      for (int i = mBlocks.Count - 1; i >= 0; i--)
         if (!used.Contains (mBlocks[i])) { mBlocks.RemoveAt (i); result = true; }
      return result;
   }

   /// <summary>
   /// Enumerate every Ent3 (including the ones in blocks)
   /// </summary>
   /// Each entity is enumerated only once, so if multiple E3Insert reference a Block3,
   /// that block is walked only once
   public IEnumerable<Ent3> DeepEnumEnts () {
      HashSet<Block3> done = [];
      foreach (var ent in mEnts) {
         yield return ent;
         if (ent is E3Insert ei) {
            Block3 block = ei.Block;
            if (done.Add (block))
               foreach (var ent1 in block.Ents) yield return ent1;
         }
      }
   }

   /// <summary>Returns the 'neighbors' of the given entity (all entities that share a common edge)</summary>
   public IReadOnlyList<E3Surface> GetNeighbors (E3Surface ent) {
      if (_neighbors == null) {
         _neighbors = [];
         HashSet<E3Surface> nearby = [];
         foreach (var ent1 in Ents.OfType<E3Surface> ()) {
            nearby.Clear (); 
            foreach (var edge in ent1.Contours.SelectMany (a => a.Curves)) 
               if (GetCoedge (edge, out var ent2, out _)) nearby.Add (ent2);
            _neighbors.Add (ent1, [.. nearby]);
         }
      }
      return _neighbors.TryGetValue (ent, out var list) ? list : [];
   }
   Dictionary<E3Surface, List<E3Surface>>? _neighbors;

   /// <summary>Given a particular Curve3, this returns the co-edge (and the surface it belongs to)</summary>
   /// If no co-edge is found, this returns false. If a coedge is found is sets s2 and c2 and
   /// returns true. 
   public bool GetCoedge (Curve3 c, [NotNullWhen (true)]out E3Surface? s2, [NotNullWhen (true)]out Curve3? c2) {
      if (_coedges == null) {
         // First time we cal lthis, build up a dictionary that maps each pair-id
         // with both the curves carrying that edge
         _coedges = [];
         Dictionary<int, (E3Surface S, Curve3 C)> unpaired = [];
         foreach (var s1 in Ents.OfType<E3Surface> ()) {
            foreach (var c1 in s1.Contours.SelectMany (con => con.Curves)) {
               if (c1.PairId == 0) continue;
               if (unpaired.TryGetValue (c1.PairId, out var tuple)) {
                  _coedges.Add (c1.PairId, (tuple.S, tuple.C, s1, c1));
                  unpaired.Remove (c1.PairId);
               } else
                  unpaired.Add (c1.PairId, (s1, c1));
            }
         }
      }
      // Once the dictionary has been built, we can find the two curves corresponding 
      // to this edge
      if (_coedges.TryGetValue (c.PairId, out var pair)) {
         if (pair.C1 == c) { s2 = pair.S2; c2 = pair.C2; return true; }
         if (pair.C2 == c) { s2 = pair.S1; c2 = pair.C1; return true; }
         throw new InvalidOperationException ();
      }
      s2 = null; c2 = null;
      return false;
   }
   Dictionary<int, (E3Surface S1, Curve3 C1, E3Surface S2, Curve3 C2)>? _coedges;

   /// <summary>Adds the list of shared edges between two surfaces to the output list</summary>
   /// The output contains an even number of edges. Even-numbered edges are edges from surface s1, 
   /// and the odd-numbered edges are the corresponding co-edges from surface s2. This clears the
   /// output array before it starts, and returns true if any edges were found
   public bool GetSharedEdges (E3Surface s1, E3Surface s2, List<Curve3> output) {
      output.Clear (); 
      foreach (var edge in s1.Contours.SelectMany (a => a.Curves))
         if (GetCoedge (edge, out var sOther, out var eOther) && sOther == s2)
            output.AddM (edge, eOther);
      return output.Count > 0;
   }

   /// <summary>Returns the list of all entities that are connected</summary>
   public List<E3Surface> GetConnected (E3Surface seed) {
      HashSet<E3Surface> seen = [seed];
      Queue<E3Surface> todo = []; todo.Enqueue (seed);
      while (todo.Count > 0) {
         var ent = todo.Dequeue ();
         foreach (var neighbor in GetNeighbors (ent))
            if (seen.Add (neighbor)) todo.Enqueue (neighbor);
      }
      return [.. seen];
   }

   /// <summary>Partitions the model by connectivity</summary>
   public List<List<E3Surface>> PartitionByConnectivity () {
      HashSet<E3Surface> done = [];
      List<List<E3Surface>> sets = [];
      foreach (var seed in Ents.OfType<E3Surface> ()) {
         if (done.Add (seed)) {
            var set = GetConnected (seed);
            foreach (var a in set) done.Add (a);
            sets.Add (set);
         }
      }
      return sets;
   }

   // Operators ----------------------------------------------------------------
   /// <summary>Returns a copy of the entire model transformed by the given matrix</summary>
   public static Model3 operator * (Model3 model, Matrix3 xfm) => new (model, xfm);

   // Implementation -----------------------------------------------------------
   // Handles changes in the Ents list, and keeps the Bound up-to-date
   void OnEntsChanged (ListChange ch) {
      switch (ch.Action) {
         case ListChange.E.Added:
            if (mEnts.Count == 1) mBound = new ();
            if (!mBound.IsEmpty) mBound += mEnts[ch.Index].Bound;
            break;
         case ListChange.E.Removing:
            // When removing an entity, if that entity lies on the 'edge' of the
            // drawing, reset the bound for recompute
            var bound = mEnts[ch.Index].Bound.InflatedF (1.01);
            if (!mBound.Contains (bound)) mBound = new ();
            break;
         default: mBound = new (); break;
      }
   }

   // Constructor used by transform operator
   Model3 (Model3 src, Matrix3 xfm) {
      foreach (var ent in src.Ents) mEnts.Add (ent * xfm);
      mEnts.Subscribe (OnEntsChanged);
   }
}
#endregion
