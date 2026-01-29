namespace Nori;

public class ModifyDwgEnts : UndoStep {
   public ModifyDwgEnts (Dwg2 dwg, string desc, IEnumerable<Ent2> add, IEnumerable<Ent2> rmv) {
      mDescription = desc;
      mAdd = [.. add]; mRmv = [.. rmv];
      mDwg = dwg;
      QuickStitch ();
   }
   readonly Dwg2 mDwg;
   readonly string mDescription;
   readonly List<Ent2> mAdd, mRmv;

   public override string Description => mDescription;

   public override void Step (EUndo dir) {
      var (add, rmv) = dir == EUndo.Redo ? (mAdd, mRmv) : (mRmv, mAdd);
      foreach (var ent in rmv) mDwg.Ents.Remove (ent);
      foreach (var ent in add) mDwg.Ents.Add (ent);
   }

   // Implementation -----------------------------------------------------------
   // Quick-stitch the incoming entities with existing open poly in the drawing
   void QuickStitch () {
      for (int i = mAdd.Count - 1; i >= 0; i--) {
         // Take each open poly in the new set
         if (mAdd[i] is not E2Poly { Poly.IsOpen: true } e2p) continue;
         foreach (var ent in mDwg.Ents) {
            // Check it against all existing open poly in the same layer
            if (ent is not E2Poly { Poly.IsOpen: true } e2p0) continue;
            if (e2p0.Layer != e2p.Layer) continue; 
            if (e2p0.Poly.TryAppend (e2p.Poly, out var tmp)) {
               // We are able to append this with the an existing poly e2p0, so
               // remove that and add in the composite poly instead
               mRmv.Add (e2p0);
               mAdd[i] = e2p0.With (tmp);
            }
         }
      }
   }
}
