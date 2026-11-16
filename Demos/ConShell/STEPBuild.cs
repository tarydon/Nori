using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Nori.STEP;
namespace Nori;
using static Nori.STEP.EKind;

public partial class STEPReader {
   bool CheckIt () {
      return true; 
      if (Product.Count != 1) return false;
      if (AssyUsage.Count != 0) return false;
      if (Manifold.Count != 1) return false;
      if (Ints.Count (a => a.Kind is CLOSED_SHELL) != 1) return false;
      if (ShapeRepr.Count (a => a.Kind is ADVANCED_BREP_SHAPE_REPRESENTATION) != 1) return false; 
      return true;
   }

   void Build () {
      var span = ShapeRepr.AsSpan ();
      for (int i = 0; i < span.Length; i++) {
         ref readonly ShapeRepr sr = ref span[i];
         if (sr.Kind == ADVANCED_BREP_SHAPE_REPRESENTATION) {
            bool done = false;
            foreach (var n in sr.Items) {
               var (kind, index) = Data[n];
               switch (kind) {
                  case MANIFOLD_SOLID_BREP:
                     DoShell (Manifold[index], sr.Context); done = true;
                     break;
                  case BREP_WITH_VOIDS:
                     var voids = BRepVoids[index];
                     DoShell (voids.Outer, sr.Context);
                     foreach (var k in voids.Inner) DoShell (k, sr.Context);
                     done = true;
                     break;
               }
            }
            Check (done, 3);
         }
      }
   }

   void DoShell (int nShell, int nContext) {
      // First, try to get the units from the CONTEXT
      Check (FindInCompound (nContext, "GLOBAL_UNIT_ASSIGNED_CONTEXT"u8), 5);
      mR.Match ('('); int[] ids = RRefs ();

      string? lengthUnit = null, angleUnit = null;
      foreach (var id in ids) {
         lengthUnit ??= BLengthUnit (id);
         angleUnit ??= BAngleUnit (id);
      }
      Check (lengthUnit != null, 6);
      Check (lengthUnit is "MILLIMETRE" or "METRE" or "INCH" or "CENTIMETRE", 7);
      Check (angleUnit != null, 8);
      Check (angleUnit is "DEGREE" or "RADIAN", 9);

      var shell = Ints[GetIndex (nShell, CLOSED_SHELL)];
      foreach (var n in shell.Items) mModel.Ents.Add (DoFace (n));
   }

   // Builds a face (surface), given a pointer to an ADVANCED_FACE. 
   Ent3 DoFace (int nFace) {
      Face face = Face[GetIndex (nFace, ADVANCED_FACE)];
      ImmutableArray<Contour3> trims = [.. DoContours (face.Contours)];
      var (kind, index) = Data[face.Surface];
      switch (kind) {
         case PLANE: return new E3Plane (nFace, trims, MakeCoordSys (Int[index], face.Reverse));
      }
      throw new STEPException (16, kind);
   }

   List<Contour3> DoContours (int[] contours) {
      mTmpContours.Clear ();
      Check (contours.Length > 0, 17);
      for (int i = 0; i < contours.Length; i++) {
         mTmpCurves.Clear ();
         FaceBound faceBound = FaceBound[GetIndex (contours[i], FACE_BOUND, FACE_OUTER_BOUND)];
         var oedges = Ints2[GetIndex (faceBound.EdgeLoop, EDGE_LOOP)];
         if (faceBound.Reverse) {
            for (int j = oedges.Length - 1; j >= 0; j--) mTmpCurves.Add (DoCurve (oedges[j], true));
         } else {
            foreach (var oe in oedges) mTmpCurves.Add (DoCurve (oe, false));
         }
         mTmpContours.Add (new Contour3 ([.. mTmpCurves]));
      }
      return mTmpContours;
   }
   List<Curve3> mTmpCurves = [];
   List<Contour3> mTmpContours = [];

   Curve3 DoCurve (int id, bool reverse) {
      var oedge = OEdge[GetIndex (id, ORIENTED_EDGE)];
      reverse ^= oedge.Reverse;
      int edgeId = oedge.Edge;
      if (mEdges.TryGetValue (edgeId, out var curve)) 
         return reverse ? new RevCurve3 (curve) : curve;

      var ecurve = Edge[GetIndex (edgeId, EDGE_CURVE)];
      Point3 start = DoPoint (ecurve.Start);
      Point3 end = DoPoint (ecurve.End);

      var tup = Data[ecurve.Curve];
      if (ecurve.Reverse) (start, end) = (end, start);
      switch (tup.Kind) {
         case LINE: curve = new Line3 (edgeId, start, end); break;
         case CIRCLE: 
            Circle circle = Circle[GetIndex (ecurve.Curve, CIRCLE)];
            curve = MakeArc (edgeId, circle, start, end, ecurve.Reverse);
            break;
         default: throw new STEPException (16, tup.Kind);
      }
      mEdges.Add (edgeId, curve);
      return reverse ? new RevCurve3 (curve) : curve;
   }
   Dictionary<int, Curve3> mEdges = [];

   Arc3 MakeArc (int pairId, Circle circle, Point3 start, Point3 end, bool reverse) {
      CoordSystem cs = MakeCoordSys (circle.CoordSys, reverse);
      OptCheck (cs.Org.DistTo (start).EQ (circle.Radius), 19); 
      OptCheck (cs.Org.DistTo (end).EQ (circle.Radius), 19);

      // Try to compose a local coordinate system for this Arc3
      // 1. The center is just cs.Org (the original center of the underlying circle)
      // 2. The x-axis is the direction of the start point from the origin (we divide
      //    by Radius to normalize this, avoiding a square-root calculation)
      // 3. If the Arc was winding CCW about the original ZAxis, then we can keep
      //    that as our final Zaxis as well, otherwise we flip it
      Vector3 xaxis = (start - cs.Org).Normalized ();

      // Now, compose a new coordinate system in which this arc is canonically
      // defined
      double angSpan;
      var csFinal = new CoordSystem (cs.Org, xaxis, cs.VecZ * xaxis);
      if (start.EQ (end)) angSpan = Lib.TwoPI;
      else {
         Vector3 endV = (end - cs.Org) / circle.Radius;
         angSpan = Lib.Acos (csFinal.VecX.CosineToAlreadyNormalized (endV));
         if (endV.Opposing (csFinal.VecY)) angSpan = Lib.TwoPI - angSpan;
      }
      var a3 = new Arc3 (pairId, csFinal, circle.Radius, angSpan);
      OptCheck (a3.Start.EQ (start), 19);
      OptCheck (a3.End.EQ (end), 19);
      return a3;
   }

   CoordSystem MakeCoordSys (int nCS, bool flipped) {
      var (kind, index) = Data[nCS];
      Check (kind == AXIS2_PLACEMENT_3D, 11);
      var cs = Axis3D[index];
      Vector3 zAxis = BVector (cs.ZAxis), xAxis = BVector (cs.XAxis);
      Vector3 yAxis = flipped ? xAxis * zAxis : zAxis * xAxis;
      return new (BPoint (cs.Origin), xAxis, yAxis);
   }

   Point3 DoPoint (int nVertex) {
      int nCartesian = Int[GetIndex (nVertex, VERTEX_POINT)];
      return Point3[GetIndex (nCartesian, CARTESIAN_POINT)];
   }

   Point3 BPoint (int n) => Point3[Data[n].Index];

   Vector3 BVector (int n) => Vector3[Data[n].Index];

   string? BAngleUnit (int id) {
      var span = GetCompoundBytes (id);
      if (!Seek (span, "PLANE_ANGLE_UNIT"u8)) return null;
      if (Seek (span, "CONVERSION_BASED_UNIT"u8)) { 
         // The conversion based unit of 'DEGREE' is most common, so we check that first
         mR.Match ('(');
         return RString ().ToUpperInvariant ();
      }
      if (Seek (span, "SI_UNIT"u8)) {
         // The SI_UNIT of 'RADIAN' is much less common
         mR.Match ('('); return REnum () + REnum (); 
      }
      Console.WriteLine (GetCompoundString (id));
      Environment.Exit (-1);
      return "";
   }

   string? BLengthUnit (int id) {
      var span = GetCompoundBytes (id);
      if (!Seek (span, "LENGTH_UNIT"u8)) return null;
      if (Seek (span, "SI_UNIT"u8)) {
         // The SI_UNIT of 'MILLIMETRE' is the most common, so we check that first
         mR.Match ('('); return REnum () + REnum (); 
      }
      if (Seek (span, "CONVERSION_BASED_UNIT"u8)) {
         // The conversion-based unit of 'INCH' is much less common
         mR.Match ('(');
         string s = RString ().ToUpperInvariant ();
         if (s.Length > 5 && s.EndsWith (" INCH")) return "INCH";
         return s; 
      }
      return null;
   }

   // Helpers ------------------------------------------------------------------
   // Gets the index given an object id, after verifying that the expected kind 
   // of object is stored at that id
   int GetIndex (int id, EKind kind) {
      var tup = Data[id];
      if (tup.Kind != kind) throw new STEPException (15, kind, tup.Kind);
      return tup.Index;
   }

   int GetIndex (int id, EKind k1, EKind k2) {
      var tup = Data[id];
      if (tup.Kind != k1 && tup.Kind != k2) throw new STEPException (18, k1, k2, tup.Kind);
      return tup.Index;
   }

   ReadOnlySpan<byte> GetCompoundBytes (int id) {
      var tup = Data[id]; if (tup.Kind != COMPOUND) return [];
      int end = mRaw.AsSpan (tup.Index).IndexOf (';'); if (end < 0) return [];
      return mRaw.AsSpan (tup.Index, end);
   }

   string GetCompoundString (int id) => Encoding.ASCII.GetString (GetCompoundBytes (id));

   bool Seek (ReadOnlySpan<byte> line, ReadOnlySpan<byte> word) {
      int pos = line.IndexOf (word); if (pos < 0) return false;
      int start = (int)Unsafe.ByteOffset (ref mRaw[0], ref MemoryMarshal.GetReference (line));
      mR.Pos = start + pos + word.Length;
      return true; 
   }

   bool FindInCompound (int id, ReadOnlySpan<byte> word) => Seek (GetCompoundBytes (id), word);
}
