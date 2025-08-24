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
   /// <summary>The Bound of this Model</summary>
   public Bound3 Bound => Bound3.Update (ref mBound, () => new (mEnts.Select (e => e.Bound)));
   Bound3 mBound = new ();

   /// <summary>The set of entities in this Model</summary>
   public AList<Ent3> Ents => mEnts;
   readonly AList<Ent3> mEnts = [];

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
            var bound = mEnts[ch.Index].Bound.InflatedF (1);
            if (!mBound.Contains (bound)) mBound = new ();
            break;
         default: mBound = new (); break;
      }
   }
}
#endregion
