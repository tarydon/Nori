// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DwgStep.cs
// ║║║║╬║╔╣║ Implements undo steps related to Dwg2
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class ModifyDwgEnts ------------------------------------------------------------------------
/// <summary>UndoStep used to add/remove entities into a drawing</summary>
public class ModifyDwgEnts : UndoStep {
   // Constructor --------------------------------------------------------------
   /// <summary>Construct a ModifyDwgEnts step with a set of entities to add and a set to remove</summary>
   /// If stitch if set to true, then this will try to 'stitch' any E2Poly we are adding in
   /// with existing open poly already existing in the drawing. The stitch happens if the Poly
   /// endpoints touch to within Epsilon, and are in the same layer
   public ModifyDwgEnts (Dwg2 dwg, string desc, IEnumerable<Ent2> add, IEnumerable<Ent2> rmv, bool stitch = true) : base (dwg, desc) {
      mDwg = dwg; mAdd = [.. add]; mRmv = [.. rmv];
      if (stitch) QuickStitch ();
      Lib.Trace ($"{desc}: add {mAdd.Count}, remove {mRmv.Count}");
   }

   // Overrides ----------------------------------------------------------------
   // Actually do the add/remove of entities. Note that any 'stitching' required has already
   // been done by the constructor. 
   public override void Step (EUndoDir dir) {
      var (add, rmv) = dir == EUndoDir.Redo ? (mAdd, mRmv) : (mRmv, mAdd);
      foreach (var ent in rmv) mDwg.Ents.Remove (ent);
      foreach (var ent in add) mDwg.Ents.Add (ent);
   }

   // Implementation -----------------------------------------------------------
   // Quick-stitch the incoming entities with existing open poly in the drawing.
   // This does not modify the drawing, but adjusts the mAdd and mRmv sets that we
   // are building. So for example, if we are adding a new open Poly A into the drawing
   // that is stitchable to an existing Poly B already in the drawing, then:
   // - the mAdd set will be adjusted to contain the stitched result A+B instead
   // - the mRmv set will contain the Poly B (which must be removed from the drawing)
   void QuickStitch () {
      HashSet<Ent2> seen = [.. mRmv];
      for (int i = mAdd.Count - 1; i >= 0; i--) {
         // Take each open poly in the new set
         if (mAdd[i] is not E2Poly { Poly.IsOpen: true } e2p) continue;
         foreach (var ent in mDwg.Ents) {
            // Check it against all existing open poly in the same layer
            if (ent is not E2Poly { Poly.IsOpen: true } e2p0) continue;
            if (e2p0.Layer != e2p.Layer || seen.Contains (e2p0)) continue; 
            if (e2p0.Poly.TryAppend (e2p.Poly, out var tmp)) {
               // We are able to append this with the an existing poly e2p0, so
               // remove that and add in the composite poly instead. At this point, we
               // increment i and break out (causing this same E2Poly to be considered one more
               // time, so that the 'other' end can also join with any existing open Poly
               // if possible). 
               mRmv.Add (e2p0); seen.Add (e2p0);
               mAdd[i++] = e2p0.With (tmp);
               break;
            }
         }
      }
   }

   // Private data -------------------------------------------------------------
   readonly Dwg2 mDwg;              // Drawing we're working with
   readonly List<Ent2> mAdd, mRmv;  // Set of entities to add and remove
}
#endregion

#region class ModifyDwgLayers ----------------------------------------------------------------------
/// <summary>UndoStep used to add/remove layers from the drawing</summary>
public class ModifyDwgLayers : UndoStep {
   /// <summary>Constructor that takes a set of layers to add, and a set to remove</summary>
   /// Note that a Layer2 can be removed only if it has no entities. Otherwise, the Dwg2 will
   /// throw an exception. 
   public ModifyDwgLayers (Dwg2 dwg, string desc, IEnumerable<Layer2> add, IEnumerable<Layer2> rmv) : base (dwg, desc)
      => (mDwg, mAdd, mRmv) = (dwg, [.. add], [.. rmv]);

   // Overrides ----------------------------------------------------------------
   // Add/remove the necessary layers
   public override void Step (EUndoDir dir) {
      var (add, rmv) = dir == EUndoDir.Redo ? (mAdd, mRmv) : (mRmv, mAdd);
      foreach (var layer in rmv) mDwg.Remove (layer);
      foreach (var layer in add) mDwg.Add (layer);
   }

   // Private data -------------------------------------------------------------
   readonly Dwg2 mDwg;                 // Drawing we're working with
   readonly List<Layer2> mAdd, mRmv;   // Set of layers to add/remove
}
#endregion
