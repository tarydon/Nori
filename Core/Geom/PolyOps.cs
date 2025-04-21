// ────── ╔╗
// ╔═╦╦═╦╦╬╣ PolyOps.cs
// ║║║║╬║╔╣║ Continuation of the Poly class, implements various operations
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

// This file contains a number of 'operations' on Poly. 
// All of them take a Poly and perform some operation on it, and return a modified
// Poly (or set of Poly). 
public partial class Poly {
   // Operations ---------------------------------------------------------------   
   /// <summary>Chamfers a Poly at a given node (returns null if not possible)</summary>
   /// If the node number passed in the start or end node of an open pline, this 
   /// return null. Otherwise, this is an 'interior' node, and there are two segments
   /// touching at that node (a lead-in segment, and a lead-out segment). If either
   /// of those segments are curved, or too short to take a chamfer, this returns 
   /// null. 
   /// dist1 is the distance from the corner along the lead-in segment, and dist2
   /// is the distance from the corner along the lead-out segment. 
   public Poly? Chamfer (int node, double dist1, double dist2) {
      // Handle the special case where we are chamfering at node 0 of a 
      // closed Poly (by rolling the poly and making it a chamfer at N-1)
      if (IsClosed && (node == 0 || node == Count))
         return Roll (1).Chamfer (Count - 1, dist1, dist2);

      // If this is not an interior node, or if one of the two segments attached
      // to the node is either an arc or too short, we return null
      if (node <= 0 || node >= Count) return null;
      Seg s1 = this[node - 1], s2 = this[node];
      if (s1.IsArc || s2.IsArc || s1.Length <= dist1 || s2.Length <= dist2) return null;

      // Use a PolyBuilder to build the chamfered poly. The target node where
      // the chamfer is to be added is 'node'
      PolyBuilder pb = new ();
      for (int i = 0; i < mPts.Length; i++) {
         Point2 pt = mPts[i];
         // If we are going to add the target node, shift it forward by dist2
         // along the lead-out segment slope (this is the end of the chamfer). See the code
         // below that would have already added the beginning of the chamfer (the
         // i == node - 1 check)
         if (i == node) pt = pt.Polar (dist2, s2.Slope);

         // This code adds all the other nodes (they could be the starts of line or arc
         // segments, and we handle both by looking through the mExtra array). Note that
         // we directly read the mExtra array rather than use Seg objects for better
         // performance
         if (HasArcs && i < mExtra.Length) {
            var extra = mExtra[i];
            if ((extra.Flags & EFlags.Arc) != 0) pb.Arc (pt, extra.Center, extra.Flags);
            else pb.Line (pt);
         } else 
            pb.Line (pt);

         // If we are heading towards the target node, add an new node for the beginning
         // of the chamfer, by moving backwards by dist1 along the lead-in segment slope
         if (i == node - 1) 
            pb.Line (s2.A.Polar (-dist1, s1.Slope));
      }
      // Done, close the poly if needed and return it
      if (IsClosed) pb.Close ();
      return pb.Build ();
   }

   /// <summary>'Rolls' a closed Poly so that node N becomes the starting node</summary>
   /// This returns a new Poly that looks identical, but whose start point is different. 
   /// It is mainly used to simplify some routines (like Chamfer) so they never have to 
   /// deal with the case where the Chamfer takes places at node 0 (which is both the start
   /// and end of the Poly). Instead of having to treat that as a special case, we just 
   /// Roll the Poly and now the node for chamfering becomes an interior node, which simplifies
   /// the logic. 
   public Poly Roll (int n) {
      if (!IsClosed) throw new InvalidOperationException ("Pline.Roll() works only with closed plines");
      if (!HasArcs) return new ([.. mPts.Roll (n)], [], mFlags);
      var knots = mExtra.ToList ();
      while (knots.Count < mPts.Length) knots.Add (new (Point2.Nil, 0));
      return new ([.. mPts.Roll (n)], [.. knots.Roll (n)], mFlags);
   }
}
