// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dwg2VN.cs
// ║║║║╬║╔╣║ Implements basic VNodes related to the Dwg2 class
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Dwg2VN -------------------------------------------------------------------------------
/// <summary>VNode that renders an entire drawing</summary>
public class Dwg2VN : VNode {
   // Constructor --------------------------------------------------------------
   public Dwg2VN (Dwg2 dwg) : base (dwg) => ChildSource = dwg.Ents;
}
#endregion

#region class DwgFillVN ----------------------------------------------------------------------------
/// <summary>DwgFillVN is used to fill the interior closed polylines of a drawing</summary>
public class DwgFillVN : VNode {
   // Constructors -------------------------------------------------------------
   public DwgFillVN (Dwg2 dwg, int _) : base (dwg) => mDwg = dwg;
   readonly Dwg2 mDwg;

   // Overrides ----------------------------------------------------------------
   // See the Lux.FillPath routine for more details on the input required for this shader.
   // Basically we rasterize all the closed polylines in the drawing and use that to fill
   // the 'interior' of the drawing.
   public override void Draw () {
      var bound = mDwg.Bound.InflatedF (1.01);
      mIdx.Clear (); mVec.Clear (); mVec.Add (bound.Midpoint);
      var polys = mDwg.Ents.Where (ent => ent.Layer.Name == "0")
                           .OfType<E2Poly> ().Select (polyEnt => polyEnt.Poly)
                           .Where (poly => poly.IsClosed);
      foreach (var poly in polys) {
         mPts.Clear (); poly.Discretize (mPts, 0.05, Lib.FineTessAngle);
         mIdx.Add (0); int idx0 = mVec.Count;
         mVec.AddRange (mPts.Select (a => (Vec2F)a));
         for (int i = 0; i < mPts.Count; i++) mIdx.Add (idx0 + i);
         mIdx.Add (idx0); mIdx.Add (-1);
      }
      Lux.FillPath (mVec.AsSpan (), mIdx.AsSpan (), bound);
   }
   readonly List<Vec2F> mVec = [];     // Vec2F input to FillPath
   readonly List<int> mIdx = [];       // Indices input to FillPath
   readonly List<Point2> mPts = [];    // Work buffer used for poly.Discretize

   public override void OnAttach ()
      => DisposeOnDetach (mDwg.Ents.Subscribe (OnEntsChanged));

   // Color used for filling, and ZLevel to place it below the drawing
   public override void SetAttributes ()
      => (Lux.ZLevel, Lux.Color) = (-10, new (240, 240, 248));

   // Implementation -----------------------------------------------------------
   // We watch the list of entities in the drawing - when a closed Polyline is added
   // or removed, we redraw the
   void OnEntsChanged (ListChange ch) {
      switch (ch.Action) {
         case ListChange.E.Added:
         case ListChange.E.Removing:
            if (mDwg.Ents[ch.Index] is E2Poly { Poly.IsClosed: true }) Redraw ();
            break;
         case ListChange.E.Clearing: Redraw (); break;
         default: throw new BadCaseException (ch.Action);
      }
   }
}
#endregion

#region class BlockVN ------------------------------------------------------------------------------
/// <summary>BlockVN is a VNode for a Block (referenced by an Insert)</summary>
/// The reason we have BlockVN at all is so that multiple inserts using the same Block can
/// all reuse the same BlockVN. This code simply draws all the entities in the block. When
/// it appears as the child VN of an E2InsertVN, that parent has already pushed a transform
/// on the stack so that this drawing all happens at the correct location, orientation and scale.
class Block2VN (Block2 block) : VNode (block) {
   /// <summary>Gets the BlockVN corresponding to a given block</summary>
   /// What we do is a bit of 'dependency injection' here. The Block class does not know
   /// about BlockVN (and nor should it). However, it has an 'object VNode' pointer that we
   /// use to store the BlockVN when we construct it. Subsequently, we simply reuse the same
   /// VN. Note that this just constructs the VN. When the BlockVN is referenced in an insert,
   /// it will get 'drawn' and that's when it will get an RBatch etc. As more inserts use the
   /// same Block, it will simply gain additional parents and will not actually use any
   /// additional GPU memory.
   /// When all the inserts referencing a block are removed from the drawing, the 'parent count'
   /// of this BlockVN will run down to zero, and it will release its resources.
   public static Block2VN Get (Block2 block) {
      if (block.VNode is not Block2VN bvn)
         block.VNode = bvn = new Block2VN (block);
      return bvn;
   }

   // The children of the BlockVN are EntityVN, each one wrapping around one Entity in the block
   public override VNode? GetChild (int n) {
      var ent = block.Ents.SafeGet (n);
      return ent == null ? null : MakeFor (ent);
   }

   // Overrides  --------------------------------------------------------------
   public override void OnDetach () {
      base.OnDetach ();
      block.VNode = null; // reset the cached visual node
   }
}
#endregion
