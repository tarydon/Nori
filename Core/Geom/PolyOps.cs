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
      if (IsClosed && (node == 0 || node == Count)) {
         var r = Roll (1);
         return r.IsCircle ? null : r.Chamfer (Count - 1, dist1, dist2); //No chamfer for circles
      }

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

   /// <summary>In-fillets a Poly at a given node (returns null if not possible)</summary>
   /// If the node number passed in the start or end node of an open pline, this 
   /// return null. Otherwise, this is an 'interior' node, and there are two segments
   /// touching at that node (a lead-in segment, and a lead-out segment). If either
   /// of those segments are curved, or too short to take a in-fillet, this returns null. 
   /// <param name="radius">In-fillet radius</param>
   /// <param name="left">Indicates how the in-fillet arc winds around target node</param>
   public Poly? InFillet (int node, double radius, bool left) {
      if (radius.IsZero ()) return null;
      // Handle the special case where we are in-filleting at node 0 of a 
      // closed Poly (by rolling the poly and making a in-fillet at N-1)
      if (IsClosed && (node == 0 || node == Count)) {
         var r = Roll (1);
         return r.IsCircle ? null : r.InFillet (Count - 1, radius, left); // No Infillet for circles
      }

      // If this is not an interior node, or if one of the two segments attached
      // to the node is either an arc or too short, we return null
      if (node <= 0 || node >= Count) return null;
      Seg s1 = this[node - 1], s2 = this[node];
      if (s1.IsArc || s2.IsArc || s1.Length <= radius || s2.Length <= radius) return null;

      // Use a PolyBuilder to build the in-filleted poly. The target node where
      // the in-fillet is to be added is 'node'
      PolyBuilder pb = new ();
      for (int i = 0; i < mPts.Length; i++) {
         Point2 pt = mPts[i];
         // If we are going to add the target node, shift it forward by dist2
         // along the lead-out segment slope (this is the end of the in-fillet). See the code
         // below that would have already added the beginning of the in-fillet (the
         // i == node - 1 check)
         if (i == node) pt = pt.Polar (radius, s2.Slope);

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
         // of the in-fillet, by moving backwards by dist1 along the lead-in segment slope
         if (i == node - 1)
            pb.Arc (s2.A.Polar (-radius, s1.Slope), s2.A, left ? EFlags.CW : EFlags.CCW);
      }
      // Done, close the poly if needed and return it
      if (IsClosed) pb.Close ();
      return pb.Build ();
   }

   /// <summary>Adds a step at a given node (returns null if not possible)</summary>
   /// If the node number passed in the start or end node of an open pline, this 
   /// return null. Otherwise, this is an 'interior' node, and there are two segments
   /// touching at that node (a lead-in segment, and a lead-out segment). If either
   /// of those segments are curved, or too short to make a step, this returns 
   /// null. 
   /// dist1 is the distance from the corner along the lead-in segment, and dist2
   /// is the distance from the corner along the lead-out segment.
   /// 'left' indicates how the step gets added w.r.t to the target node
   /// 'pos' indicates the reference position w.r.t the lead-in and lead-out segments
   public Poly? CornerStep (int node, double dist1, double dist2, ECornerOpFlags flags) {
      // Handle the special case where we are in-filleting at node 0 of a 
      // closed Poly (by rolling the poly and making a in-fillet at N-1)
      if (IsClosed && (node == 0 || node == Count)) {
         var r = Roll (1);
         return r.IsCircle ? null : r.CornerStep (Count - 1, dist1, dist2, flags); // No corner step for circles
      }

      // If this is not an interior node, or if one of the two segments attached
      // to the node is either an arc or too short, we return null
      if (node <= 0 || node >= Count) return null;
      var (onSameSide, nearLeadOut) = ((flags & ECornerOpFlags.SameSideOfBothSegments) != 0, (flags & ECornerOpFlags.NearLeadOut) != 0);
      Seg s1 = this[node - 1], s2 = this[node];
      if (s1.IsArc || s2.IsArc || (onSameSide && (s1.Length <= dist1 || s2.Length <= dist2))) return null;
      // When the step is outside the actual poly, it should be possible to add the step even
      // though the lengths are less than the desired step length
      if (!onSameSide) {
         if (nearLeadOut) {
            if (s2.Length <= dist2) return null;
         } else if (s1.Length <= dist1) return null;
      }

      // Use a PolyBuilder to build the in-filleted poly. The target node where
      // the in-fillet is to be added is 'node'
      PolyBuilder pb = new ();
      for (int i = 0; i < mPts.Length; i++) {
         Point2 pt = mPts[i];
         // If we are going to add the target node, shift it forward by dist2
         // along the lead-out segment slope (this is the end of the in-fillet). See the code
         // below that would have already added the beginning of the in-fillet (the
         // i == node - 1 check)
         if (i == node) pt = pt.Polar ((onSameSide || nearLeadOut) ? dist2 : -dist2, s2.Slope);

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
         // of the step, by moving backwards or forwards depending on the pick point by dist1 along the lead-in segment slope
         if (i == node - 1) {
            var st = s2.A.Polar ((onSameSide || !nearLeadOut) ? -dist1 : dist1, s1.Slope);
            pb.Line (st);
            pb.Line (st.Polar ((onSameSide || nearLeadOut) ? dist2 : -dist2, s2.Slope));
         }
      }
      // Done, close the poly if needed and return it
      if (IsClosed) pb.Close ();
      return pb.Build ();
   }

   /// <summary>Add a fillet at a given node (returns null if not possible)</summary>
   /// If the node number passed in the start or end node of an open pline, this 
   /// return null. Otherwise, this is an 'interior' node, and there are two segments
   /// touching at that node (a lead-in segment, and a lead-out segment). If either
   /// of those segments are curved, or too short to take a fillet, this returns null. 
   /// <param name="radius">Fillet radius</param>
   public Poly? Fillet (int node, double radius) {
      if (radius.IsZero ()) return null;
      // Handle the special case where we are filleting at node 0 of a 
      // closed Poly (by rolling the poly and making a fillet at N-1)
      if (IsClosed && (node == 0 | node == Count)) {
         var r = Roll (1);
         return r.IsCircle ? null : r.Fillet (Count - 1, radius); // No Fillet for circles
      }

      // If this is not an interior node, or if one of the two segments attached
      // to the node is either an arc or too short, we return null
      if (node <= 0 | node >= Count) return null;
      Seg s1 = this[node - 1], s2 = this[node];
      if (s1.IsArc | s2.IsArc | s1.Length <= radius | s2.Length <= radius) return null;

      // Use a PolyBuilder to build the fillet poly. The target node where
      // the fillet is to be added is 'node'
      PolyBuilder pb = new ();
      for (int i = 0; i < Count; i++) {
         Point2 pt = mPts[i];
         // If we are going to add the target node, shift it forward by radius
         // along the lead-out segment slope (this is the end of the fillet). See the code
         // below that would have already added the beginning of the fillet (the
         // i == node - 1 check)
         if (i == node) pt = pt.Polar (radius, s2.Slope);

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
         // of the fillet, by moving backwards by radius along the lead-in segment slope
         if (i == node - 1) {
            var start = s2.A.Polar (-radius, s1.Slope); var pt1 = start + (s2.A - start).Perpendicular ();
            var end = s2.A.Polar (radius, s2.Slope); var pt2 = end + (end - s2.A).Perpendicular ();
            pb.Arc (start, Geo.LineXLine (start, pt1, end, pt2), EFlags.CCW);
         }
      }
      // Done, close the poly if needed and return it
      if (IsClosed) pb.Close ();
      return pb.Build ();
   }

   /// <summary>Creates and returns a new reversed Poly of 'this'</summary>
   public Poly Reversed () {
      if (!HasArcs) return new ([.. mPts.Reverse ()], [], mFlags);
      PolyBuilder builder = new ();
      const EFlags Mask = EFlags.CW | EFlags.CCW;
      for (int i = Count - 1; i >= 0; i--) {
         Seg s = this[i];
         if (s.IsArc) builder.Arc (s.B, s.Center, s.Flags ^ Mask);
         else builder.Line (s.B);
      }
      if (!IsClosed) builder.Line (A); else builder.Close ();
      return builder.Build ();
   }

   /// <summary>'Rolls' a closed Poly so that node N becomes the starting node</summary>
   /// This returns a new Poly that looks identical, but whose start point is different. 
   /// It is mainly used to simplify some routines (like Chamfer) so they never have to 
   /// deal with the case where the Chamfer takes places at node 0 (which is both the start
   /// and end of the Poly). Instead of having to treat that as a special case, we just 
   /// Roll the Poly and now the node for chamfering becomes an interior node, which simplifies
   /// the logic. 
   public Poly Roll (int n) {
      if (IsCircle) return this;
      if (!IsClosed) throw new InvalidOperationException ("Poly.Roll() works only with closed plines");
      if (!HasArcs) return new ([.. mPts.Roll (n)], [], mFlags);
      var knots = mExtra.ToList ();
      while (knots.Count < mPts.Length) knots.Add (new (Point2.Nil, 0));
      return new ([.. mPts.Roll (n)], [.. knots.Roll (n)], mFlags);
   }

   /// <summary>Flags to perform some corner operations like fillet, corner-step etc</summary>
   [Flags]
   public enum ECornerOpFlags : ushort {
      None = 0,
      /// <summary>Reference point on the left of the nearest segment</summary>
      Left = 1,
      /// <summary>Reference point nearer to the lead-out segment. If this flag is not set, then the point is nearer to the lead-in segment</summary>
      NearLeadOut = 2,
      /// <summary>Reference point is on the same side of both the lead-in and lead-out segments</summary>
      SameSideOfBothSegments = 4,
      /// <summary>The closest seg closer to horizontal than vertical</summary>
      Horz = 8
   }
}
