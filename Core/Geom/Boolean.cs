// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Boolean.cs
// ║║║║╬║╔╣║ Implements various set operations of Polys
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static Nori.Lib;

/// <summary>Implements 'scan-line' algorithm for line-line intersection computation.</summary>
public class Scanline {
   public Scanline (ReadOnlySpan<Poly> input) {
      for (int i = 0; i < input.Length; i++) {
         Poly poly = input[i];
         for (int j = 0; j < poly.Count; j++) {
            var seg = poly[j];
            AddEdge (seg.A, seg.B, i);
         }
      }
   }

   /// <summary>Sweeps though the events from the event pool and handles the events based on the event type.</summary>
   /// <param name="cb">Callback to invoke when an event occurs.</param>
   public void Scan (Action<Event> cb) {
      EdgeComparer comp = new ();
      for (; Events.Count > 0;) {
         var cur = Events.Min;
         var pt = cur.Pt; comp.SetY (pt.Y);
         Events.Remove (cur);

         cb?.Invoke (cur);
         switch (cur.Kind) {
            case Event.EKind.E:
               Edge e = Edges[cur.Edge];
               // Add the edge to the active list and check intersection with the adjacent edges.
               int n = AEL.BinarySearch (e, comp);
               if (n < 0) n = ~n;  // 2's complement

               //int n = 0;
               //for (int i = 0; i < AEL.Count; i++, n++) {
               //   n = i;
               //   if (comp.Compare (e, AEL[i]) > 0) break;
               //}

               if (n >= 0) {
                  AEL.Insert (n, e);
                  CheckIntersection (n - 1, n, pt.Y);
                  CheckIntersection (n, n + 1, pt.Y);
               }
               break;

            case Event.EKind.X:
               Edge e1 = Edges[cur.Edge], e2 = Edges[cur.OtherEdge];
               // Ignore if the intersection point is one of the edge end-points, as
               // the edges are going to leave the list anyway.
               if (pt.EQ (e1.B) || pt.EQ (e2.B)) continue;
               int n1 = AEL.IndexOf (e1), n2 = n1 + 1;
               if (n1 < 0) continue;
               if (!(n1 < AEL.Count - 1 && AEL[n2] == e2)) {
                  n2 = n1 - 1;
                  if (!(n1 > 0 && AEL[n2] == e2))
                     n2 = AEL.IndexOf (e2);
                  if (n2 < 0) continue;
               }
               if (n1 > n2) (n1, n2) = (n2, n1);
               (e1, e2) = (AEL[n1], AEL[n2]);
               // The intersection point coincides with one of the edge start-points.
               // Check if it is ordered correctly and reorder if necessary.
               if ((pt.EQ (e1.A) || pt.EQ (e2.A)) && comp.Compare (e1, e2) <= 0) continue;
               // Swap the edges
               (AEL[n2], AEL[n1]) = (e1, e2);
               CheckIntersection (n1 - 1, n1, pt.Y);
               CheckIntersection (n2, n2 + 1, pt.Y);
               break;

            case Event.EKind.L:
               // Remove the edge and check intersection between the new neighbours
               int at = AEL.IndexOf (Edges[cur.Edge]);
               if (at >= 0) {
                  AEL.RemoveAt (at);
                  CheckIntersection (at - 1, at, pt.Y);
               }
               break;
         }
      }
   }

   /// <summary>Checks two edges from the active-list for the intersection and updates the event pool if they intersect.</summary>
   void CheckIntersection (int n1, int n2, double y) {
      if (n1 < 0 || n1 >= AEL.Count || n2 < 0 || n2 >= AEL.Count) return;
      Edge e1 = AEL[n1], e2 = AEL[n2];
      if (e1.Contour == e2.Contour) return;
      var (k1, k2) = ((uint)e1.ID, (uint)e2.ID);
      if (k1 > k2) (k1, k2) = (k2, k1);
      // Compute a bijective mapping for k1 and k2: 
      long k = k1; k = (k << 32) | k2;
      if (!Done.TryAdd (k, k)) return;

      var pt = Geo.LineSegXLineSeg (e1.A, e1.B, e2.A, e2.B);
      if (!pt.IsNil) Events.Add (new (EventID, e1.ID, Event.EKind.X, pt, e2.ID));
   }

   /// <summary>Adds a new segment to the edge list.</summary>
   void AddEdge (Point2 a, Point2 b, int contour) {
      int edge = Edges.Count;
      if (Compare (a, b) > 0) (a, b) = (b, a);
      Edges.Add (new (edge, contour, a, b));

      Events.Add (new (EventID, edge, Event.EKind.E, a));
      Events.Add (new (EventID, edge, Event.EKind.L, b));
   }

   // Given two segment point categorizes them into 'enter' or 'leave' events, based on event sorting rules.
   static int Compare (Point2 a, Point2 b) {
      int n = b.Y.CompareTo (a.Y);
      if (n != 0) return n;
      return a.X.CompareTo (b.X);
   }

   //Done cache.
   readonly Dictionary<long, long> Done = [];
   // The event pool.
   readonly SortedSet<Event> Events = [];
   // Edge list.
   readonly List<Edge> Edges = [];
   // The active edge list.
   readonly List<Edge> AEL = [];
   // Creates a new evetn ID.
   int EventID => mLastEventID++;
   int mLastEventID = 0;

   /// <summary>This represents an event we gathered for the sweeping.</summary>
   /// There can be three possible types specified by the event 'Kind' field.
   public readonly struct Event (int id, int edge, Event.EKind kind, Point2 pt, int otheredge = -1) : IComparable<Event> {
      /// <summary>The event type enumeration.</summary>
      public enum EKind { 
         /// <summary>'Enter' event. An edge enters to the active list.</summary>
         E, 
         /// <summary>'Intersection' event. Two edges intersect here.</summary>
         X, 
         /// <summary>'Leave' event. An edge exits the active list.</summary>
         L 
      };

      /// <summary>The unique event ID.</summary>
      public readonly int ID = id;
      /// <summary>Edge ID (or first edge in case of intersection event)</summary>
      public readonly int Edge = edge;
      /// <summary>The event point.</summary>
      public readonly Point2 Pt = pt;
      /// <summary>The event types (possible values: 'enter', 'leave' or 'cross')</summary>
      public readonly EKind Kind = kind;
      /// <summary>The 'other' edge id of the cross event.</summary>
      public readonly int OtherEdge = otheredge;

      /// <summary>Compares two events for event sorting.</summary>
      /// This is the most important event method.
      public int CompareTo (Event other) {
         if (ID == other.ID) return 0;
         Point2 a = Pt, b = other.Pt;
         int n = b.Y.CompareTo (a.Y);
         if (n != 0) return n;
         // Record 'Enter' events before the 'Leave' events
         n = Kind.CompareTo (other.Kind);
         if (n != 0) return n;
         n = a.X.CompareTo (b.X);
         if (n != 0) return n;
         n = Edge.CompareTo (other.Edge);
         if (n != 0) return n;
         return ID.CompareTo (other.ID);
      }

      public override string ToString () {
         var str = $"{Kind}: {Pt}, {Edge}";
         if (OtherEdge > 0) str += $"x{OtherEdge}";
         return str;
      }
   }

   // Represents an edge element scanline is working with.
   class Edge (int id, int contour, Point2 a, Point2 b) {
      // The unique edge ID (this is also the edge index in the Edges collection).
      public readonly int ID = id;
      // The original contour ID
      public readonly int Contour = contour;
      // Start and end points of this edge.
      public readonly Point2 A = a, B = b;
      // The slope inverse used for the faster X computation
      public readonly double D = (a.X - b.X) / (a.Y - b.Y);

      public override string ToString () => $"{Contour}.{ID}: {A},{B}";
   }

   // Compares two AEL edges for the sorting and binary searching.
   class EdgeComparer : IComparer<Edge> {
      // Sets the 'current' sweepline position
      public void SetY (double y) => Y = y;
      double Y;

      public int Compare (Edge? e1, Edge? e2) {
         if (e1 == null || e2 == null) throw new NullReferenceException ();
         if (e1.ID == e2.ID) return 0;
         var (x1, x2) = (GetX (e1, Y), GetX (e2, Y));
         if (x1 > x2 + Epsilon) return 1;
         if (x1 < x2 - Epsilon) return -1;
         // Try and resolve edges by slope.
         var y = Y - YFuzz;
         (x1, x2) = (GetX (e1, y), GetX (e2, y));
         var n = x1.CompareTo (x2);
         if (n != 0) return n;
         n = e1.ID.CompareTo (e2.ID);
         return n;
      }

      static double GetX (Edge e, double y) {
         if (!GetXOnLineAtY (e, y, out var x)) {
            if (!GetXOnLineAtY (e, y, out x, true)) throw new InvalidOperationException ();
         }
         return x;
      }
      const double YFuzz = 1E-5;

      // Returns X on an infinite line passing through points a and b at a given Y.
      // The edges in AEL are sorted by their x-position at a given Y. This routine is used for
      // sorting the edges in the AEL. 
      // If edge is parallel to x-axis, then it returns minimum x if y lies on the line, returns
      // false otherwise. It also accepts a tilt flag which when true, a yFuzz is applied to the
      // edge to tilt it slightly down to get an X deterministically. 
      static bool GetXOnLineAtY (Edge e, double y, out double x, bool canTiltEdge = false) {
         x = double.NaN;
         var (a, b, d) = (e.A, e.B, e.D);
         if (a.Y == b.Y) {
            if (canTiltEdge) {
               b = b.WithY (b.Y - YFuzz);
               d = (a.X - b.X) / (a.Y - b.Y);
            } else {
               if (y.EQ (a.Y)) {
                  x = Math.Min (a.X, b.X);
                  return true;
               }
               return false;
            }
         }
         x = a.X + (y - a.Y) * d;
         return true;
      }
   }
}
