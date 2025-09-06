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
      if (IsCircle) return null; // No chamfer for circles
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
         if (HasArcs && i < Extra.Length) {
            var extra = Extra[i];
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
   /// of those segments are curved, or too short to take a in-fillet, this returns null.
   public Poly? InFillet (int node, double radius, bool left) {
      if (IsCircle || radius.IsZero ()) return null; // No Infillet for circles
      // Handle the special case where we are in-filleting at node 0 of a
      // closed Poly (by rolling the poly and making a in-fillet at N-1)
      if (IsClosed && (node == 0 || node == Count))
         return Roll (1).InFillet (Count - 1, radius, left);

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
         if (HasArcs && i < Extra.Length) {
            var extra = Extra[i];
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
      if (IsCircle) return null; // No Corner-step for circles.
      // Handle the special case where we are in-filleting at node 0 of a
      // closed Poly (by rolling the poly and making a in-fillet at N-1)
      if (IsClosed && (node == 0 || node == Count))
         return Roll (1).CornerStep (Count - 1, dist1, dist2, flags);

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
         if (HasArcs && i < Extra.Length) {
            var extra = Extra[i];
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
   /// <param name="node">The node to apply the fillet at</param>
   /// <param name="radius">Fillet radius</param>
   public Poly? Fillet (int node, double radius) {
      if (IsCircle || radius.IsZero ()) return null; // No Fillet for circles
      // Handle the special case where we are filleting at node 0 of a
      // closed Poly (by rolling the poly and making a fillet at N-1)
      if (IsClosed && (node == 0 || node == Count))
         return Roll (1).Fillet (Count - 1, radius);

      // If this is not an interior node, or if one of the two segments attached
      // to the node is either an arc or too short, we return null
      if (node <= 0 || node >= Count) return null;
      Seg s1 = this[node - 1], s2 = this[node];
      if (s1.IsArc || s2.IsArc) return null;
      var turnAngle = GetTurnAngle (node);

      // Find the tangent points for the fillet by shifting the common vertex along the slope of each line
      double len = Math.Abs (radius / Math.Tan ((Lib.PI - turnAngle) / 2));
      if (s1.Length <= len || s2.Length <= len) return null;

      // Use a PolyBuilder to build the fillet poly. The target node where
      // the fillet is to be added is 'node'
      PolyBuilder pb = new ();

      for (int i = 0; i < Count; i++) {
         Point2 pt = mPts[i];
         // If we are going to add the target node, shift it forward by len
         // along the lead-out segment slope (this is the end of the fillet). See the code
         // below that would have already added the beginning of the fillet (the
         // i == node - 1 check)
         if (i == node) pt = pt.Polar (len, s2.Slope);

         // This code adds all the other nodes (they could be the starts of line or arc
         // segments, and we handle both by looking through the mExtra array). Note that
         // we directly read the mExtra array rather than use Seg objects for better
         // performance
         if (HasArcs && i < Extra.Length) {
            var extra = Extra[i];
            if ((extra.Flags & EFlags.Arc) != 0) pb.Arc (pt, extra.Center, extra.Flags);
            else pb.Line (pt);
         } else
            pb.Line (pt);

         // If we are heading towards the target node, add a new node for the beginning
         // of the fillet, by moving backwards by len along the lead-in segment slope
         if (i == node - 1) {
            var start = s2.A.Polar (-len, s1.Slope); var pt1 = start + (s2.A - start).Perpendicular ();
            var end = s2.A.Polar (len, s2.Slope); var pt2 = end + (end - s2.A).Perpendicular ();
            var winding = turnAngle > 0 ? EFlags.CCW : EFlags.CW;
            pb.Arc (start, Geo.LineXLine (start, pt1, end, pt2), winding);
         }
      }
      // Done, close the poly if needed and return it
      if (IsClosed) pb.Close ();
      return pb.Build ();
   }

   /// <summary>Inserts edge recess (rect notch) on the specified seg (returns null if not possible)</summary>
   /// <param name="left">Side of the seg, where the notch unfurls</param>
   /// <param name="centerOffset">Offset of the notch-center, from start of the seg</param>
   /// <param name="width">Width of rect notch</param>
   /// <param name="depth">Depth of rect notch</param>
   public Poly? EdgeRecess (int seg, bool left, double centerOffset, double width, double depth) {
      Seg s = this[seg];
      if (!s.IsLine || centerOffset < width / 2 || s.Length < (centerOffset + width / 2)) return null; // Check: Notch fits the given seg length.

      PolyBuilder pb = new ();
      // Precompute values now, and keep the loop below clean.
      (double slope, double slope2) = (s.Slope, s.Slope + (left ? Lib.HalfPI : -Lib.HalfPI));
      double offset = centerOffset - (width / 2); // Portion of seg leading upto notch
      double offset2 = s.Length - (offset + width); // Portion of seg remaining after the notch

      for (int i = 0; i < Count; i++) {
         Point2 pt = mPts[i];
         // This code adds all the other nodes (they could be the starts of line or arc
         // segments, and we handle both by looking through the mExtra array). Note that
         // we directly read the mExtra array rather than use Seg objects for better
         // performance
         if (HasArcs && i < Extra.Length) {
            var extra = Extra[i];
            if ((extra.Flags & EFlags.Arc) != 0) {
               pb.Arc (pt, extra.Center, extra.Flags);
               continue;
            }
         }
         pb.Line (pt);
         if (i == seg) {
            if (!offset.IsZero ()) pb.Line (pt = pt.Polar (offset, slope));
            pb.Line (pt = pt.Polar (depth, slope2));
            pb.Line (pt = pt.Polar (width, slope));
            if (!offset2.IsZero ()) pb.Line (pt.Polar (-depth, slope2));
         }
      }
      // Done, close the poly if needed and return it
      if (IsClosed) return pb.Close ().Build ();
      return pb.End (mPts[^1]);
   }

   /// <summary>Extends the specified seg about given lie, and returns resultant polys, if any.</summary>
   /// <param name="segIdx">The segment to extend</param>
   /// <param name="lie">Indicates the direction to extend</param>
   /// <param name="dist">If not 0, specifies the extension limit</param>
   /// <param name="polySoup">Set of polys to extend against</param>
   public IEnumerable<Poly> ExtendedSeg (int segIdx, double lie, double dist, IEnumerable<Poly> polySoup) {
      // Find all the "extended" intersections along the given segment with the rest of the segments.
      // Note: When a seg is extended, it may also fragment the poly into multiple polys!
      // Open poly fragments unless the end segs grow outwards. Closed poly converts into an open poly.
      if (IsCircle) yield break; // Circle cannot extend
      Seg seg = this[segIdx];
      bool fwd = lie > 0.5; // Extend forward/backward
      bool limitDist = !dist.IsZero ();
      if (!CanExtend (seg, fwd, polySoup, out double extendLie)) {
         if (seg.IsArc) { // Nothing stops it from becoming a full circle
            yield return Circle (seg.Center, seg.Radius);
            // Once "arc" seg is detached, the poly fragments into two more fragments.
            if (segIdx > 0) {
               List<Point2> pts = [.. Pts[..(segIdx + 1)]]; // First split section
               int cExtra2 = Math.Min (Extra.Length, segIdx);
               yield return new Poly ([.. pts], Extra[..cExtra2], mFlags);
            }
            if (Count - segIdx + 1 > 1) {
               List<Point2> pts = [.. Pts[(segIdx + 1)..]]; // Second split section
               int cExtra2 = Math.Min (Extra.Length, segIdx + 1);
               yield return new Poly ([.. pts], Extra[cExtra2..], mFlags);
            }
            yield break;
         } else if (!limitDist) yield break;
      }
      if (limitDist && seg.IsLine) extendLie = 1 + (dist / seg.Length);
      Point2 ptExt = seg.GetPointAt (extendLie);

      if (IsClosed) {
         var poly = Roll (fwd ? segIdx + 1 : segIdx);
         List<Point2> pts = [.. poly.Pts];
         if (fwd) pts.Add (ptExt);
         else { pts.Add (pts[0]); pts[0] = ptExt; }
         yield return new Poly ([.. pts], poly.Extra, poly.mFlags & ~EFlags.Closed);
         yield break;
      }

      // Open poly - may split into two open polys, one of which is extended
      int nextIdx = fwd ? segIdx + 1 : segIdx; // Next poly begin!
      int cExtra = Math.Min (Extra.Length, nextIdx);
      if (nextIdx > 0) {
         List<Point2> pts = [.. Pts[..(nextIdx + 1)]]; // First split section
         if (fwd) pts[^1] = ptExt;
         yield return new Poly ([.. pts], Extra[..cExtra], mFlags);
      }
      if (Count - nextIdx + 1 > 1) {
         List<Point2> pts = [.. Pts[(nextIdx)..]]; // Second split section
         if (!fwd) pts[0] = ptExt;
         yield return new Poly ([.. pts], Extra[cExtra..], mFlags);
      }

      static bool CanExtend (Seg seg, bool fwd, IEnumerable<Poly> polySoup, out double extendLie) {
         Span<Point2> buffer = stackalloc Point2[2];
         (double fwdCutoff, double revCutoff) = (1 + Lib.Epsilon, 0 - Lib.Epsilon);
         (double fwdExt, double revExt) = (1000, -1000);
         foreach (var s in polySoup.SelectMany (p => p.Segs)) {
            var pts = seg.Intersect (s, buffer, finite: false);
            if (pts.Length == 0) continue;
            foreach (var pt in pts) {
               double lie = seg.GetLie (pt);
               if (fwd) {
                  if (lie > fwdCutoff) fwdExt = Math.Min (fwdExt, lie);
               } else {
                  if (lie < revCutoff) revExt = Math.Max (revExt, lie);
               }
            }
         }
         extendLie = fwd ? fwdExt : revExt;
         return fwd ? fwdExt != 1000 : revExt != -1000;
      }
   }

   /// <summary>Trims the specified seg about given lie, and returns any left-over poly/s</summary>
   /// <param name="segIdx">Segment to trim</param>
   /// <param name="lie">Indicates section of segment to trim</param>
   /// <param name="polySoup">Set of polys to trim against</param>
   /// <returns>Any left-over poly/s</returns>
   public IEnumerable<Poly> TrimmedSeg (int segIdx, double lie, IEnumerable<Poly> polySoup) {
      // Find all the "contained" intersections along the given segment with the rest of the segments.
      // The given segment gets removed - either fully, or some section on the segment.
      //    This is determined by fromLie, toLie delimiters. (Default: fromLie == 0, toLie == 1, along the given segment)
      // "Closed poly" become open poly, accommodating any left-over fragments of the trimmed segment
      // "Open poly" breaks into one or two open polys, accommodating any left-over fragments of the trimmed segment
      var seg = this[segIdx];
      ArcInfo segExtra = segIdx < Extra.Length ? Extra[segIdx] : ArcInfo.Nil;
      if (IsCircle) {
         (double lieA, double lieB) = CircleTrimExtents (seg, lie.Clamp (), polySoup);
         if (lieA == 0 && lieB == 1) yield break; // Fully trimmed out
         bool iCCW = (mFlags & EFlags.CCW) != 0;
         yield return Arc (segExtra.Center, this[0].Radius, lieA * Lib.TwoPI, lieB * Lib.TwoPI, iCCW);
         yield break;
      }

      (double fromLie, double toLie) = TrimExtents (seg, lie.Clamp (), polySoup);
      (bool hFrag, bool tFrag) = (fromLie != 0, toLie != 1); // Indicates any left-over head frag and/or tail frag
      if (Count == 1 && !hFrag && !tFrag) yield break; // Single seg poly (incl. circle) is fully consumed
      List<Point2> pts = []; List<ArcInfo> extra = [];
      if (IsClosed) {
         var poly = Roll (segIdx + 1);
         if (tFrag) {
            pts.Add (seg.GetPointAt (toLie));
            if ((segExtra.Flags & EFlags.Arc) != 0)
               extra.Add (segExtra);
            else if (HasArcs) extra.Add (ArcInfo.Nil);
         }
         pts.AddRange (poly.Pts); extra.AddRange (poly.Extra);
         if (extra.Count == pts.Count) extra.RemoveLast ();
         if (hFrag) {
            pts.Add (seg.GetPointAt (fromLie));
            if ((segExtra.Flags & EFlags.Arc) != 0) {
               // Pad "empty" extras
               int cPadExtra = pts.Count - extra.Count - 2; // Note: We are going to add an "extra".
               for (int i = 0; i < cPadExtra; i++)
                  extra.Add (ArcInfo.Nil);
               extra.Add (segExtra);
            }
         }
         yield return new Poly ([.. pts], [.. extra], poly.mFlags & ~EFlags.Closed);
         yield break;
      }

      // Open poly...
      // First fragmented poly
      pts.AddRange (Pts[0..(segIdx + 1)]);
      extra.AddRange (Extra[0..(Math.Min (segIdx, Extra.Length))]);
      if (hFrag) {
         pts.Add (seg.GetPointAt (fromLie));
         if ((segExtra.Flags & EFlags.Arc) != 0) {
            // Pad "empty" extras
            int cPadExtra = pts.Count - extra.Count - 2; // Note: We are going to add an "extra".
            for (int i = 0; i < cPadExtra; i++)
               extra.Add (ArcInfo.Nil);
            extra.Add (segExtra);
         }
      }
      if (pts.Count > 1)
         yield return new Poly ([.. pts], [.. extra], extra.Count == 0 ? mFlags & ~EFlags.HasArcs : mFlags);

      // Second fragmented poly
      pts.Clear (); extra.Clear ();
      if (tFrag) {
         pts.Add (seg.GetPointAt (toLie));
         if ((segExtra.Flags & EFlags.Arc) != 0)
            extra.Add (segExtra);
         else if (HasArcs) extra.Add (ArcInfo.Nil);
      }
      pts.AddRange (Pts[(segIdx + 1)..]);
      if (Extra.Length > segIdx + 1) extra.AddRange (Extra[(segIdx + 1)..]);
      if (pts.Count > 1)
         yield return new Poly ([.. pts], [.. extra], extra.Count == 0 ? mFlags & ~EFlags.HasArcs : mFlags);

      static (double fromLie, double toLie) TrimExtents (Seg seg, double refLie, IEnumerable<Poly> polySoup) {
         (double fromLie, double toLie) = (0, 1);
         Span<Point2> buffer = stackalloc Point2[2];
         foreach (var s in polySoup.SelectMany (p => p.Segs)) {
            var pts = seg.Intersect (s, buffer, finite: true);
            if (pts.Length == 0) continue;
            foreach (var pt in pts) {
               double lie = seg.GetLie (pt);
               if (lie > refLie) toLie = Math.Min (toLie, lie);
               else fromLie = Math.Max (fromLie, lie);
            }
         }
         return (fromLie.IsZero () ? 0 : fromLie, toLie.EQ (1) ? 1 : toLie);
      }

      static (double fromLie, double toLie) CircleTrimExtents (Seg seg, double refLie, IEnumerable<Poly> polySoup) {
         if (!seg.IsCircle) throw new Exception ("Expected Circle seg");
         // Issue: If the trim section passes through the circle's only node point, then standard TrimExtents cannot be used
         (double fromLie, double toLie) = (0, 1);
         (double minLie, double maxLie) = (1, 0);
         Span<Point2> buffer = stackalloc Point2[2];
         foreach (var s in polySoup.SelectMany (p => p.Segs)) {
            var pts = seg.Intersect (s, buffer, finite: true);
            if (pts.Length == 0) continue;
            foreach (var pt in pts) {
               double lie = seg.GetLie (pt);
               if (lie > refLie) toLie = Math.Min (toLie, lie);
               else fromLie = Math.Max (fromLie, lie);
               maxLie = Math.Max (maxLie, lie);
               minLie = Math.Min (minLie, lie);
            }
         }
         if (refLie > maxLie || refLie < minLie) return (maxLie, minLie);
         return (fromLie.IsZero () ? 0 : fromLie, toLie.EQ (1) ? 1 : toLie);
      }
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
      var knots = Extra.ToList ();
      while (knots.Count < mPts.Length) knots.Add (new (Point2.Nil, 0));
      return new ([.. mPts.Roll (n)], [.. knots.Roll (n)], mFlags);
   }

   /// <summary>This tries to append the other poly to the current, only if their endpoints are within the threshold</summary>
   public bool TryAppend (Poly other, [MaybeNullWhen (false)] out Poly result, double threshold = 1e-6) {
      result = null;
      if (IsClosed || other.IsClosed || ReferenceEquals (this, other)) return false;
      Poly a, b;
      if (B.EQ (other.A, threshold)) (a, b) = (this, other);
      else if (B.EQ (other.B, threshold)) (a, b) = (this, other.Reversed ());
      else if (A.EQ (other.B, threshold)) (a, b) = (other, this);
      else if (A.EQ (other.A, threshold)) (a, b) = (other.Reversed (), this);
      else return false;
      List<Point2> pts = [.. a.mPts.SkipLast (1), .. b.mPts];
      EFlags flags = 0;
      if (pts[0].EQ (pts[^1])) {
         flags |= EFlags.Closed; pts = [.. pts.SkipLast (1)];
      }
      if (!a.HasArcs && !b.HasArcs) result = new Poly ([.. pts], [], flags);
      else {
         var extra = new List<ArcInfo> (a.Count + b.Count);
         for (int i = 0; i < a.Count; i++)
            extra.Add (a.Extra.SafeGet (i));
         extra.AddRange (b.Extra);
         result = new Poly ([.. pts], [.. extra], flags | EFlags.HasArcs);
      }
      return true;
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
