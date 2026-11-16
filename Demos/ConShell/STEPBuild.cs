using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Nori.STEP;
namespace Nori;
using static Nori.STEP.EKind;

public partial class STEPReader {
   bool CheckIt () {
      if (mProduct.Count != 1) return false;
      if (mAssyUsage.Count != 0) return false;
      if (mManifold.Count != 1) return false;
      if (mInts.Count (a => a.Kind is CLOSED_SHELL) != 1) return false;
      if (mShapeRepr.Count (a => a.Kind is ADVANCED_BREP_SHAPE_REPRESENTATION) != 1) return false; 
      return true;
   }

   void Build () {
      int ctx = 0, brep = -1; 
      var span = mShapeRepr.AsSpan ();
      for (int i = 0; i < span.Length; i++) {
         ref readonly ShapeRepr sr = ref span[i];
         if (sr.Kind == ADVANCED_BREP_SHAPE_REPRESENTATION) {
            Check (ctx == 0, 2);
            ctx = sr.Context;
            foreach (var n in sr.Items)
               if (mData[n].Kind == MANIFOLD_SOLID_BREP) brep = mData[n].Item2;
            Check (brep >= 0, 3);
         }
      }
      PManifold (mManifold[brep], ctx);
   }

   void PManifold (int nShell, int nContext) {
      // First, try to get the units from the CONTEXT
      Check (FindInCompound (nContext, "GLOBAL_UNIT_ASSIGNED_CONTEXT"u8), 5);
      mR.Match ('('); int[] ids = RRefs ();

      string? lengthUnit = null, angleUnit = null;
      foreach (var id in ids) {
         lengthUnit ??= GetLengthUnit (id);
         angleUnit ??= GetAngleUnit (id);
      }
      Check (lengthUnit != null, 6);
      Check (lengthUnit is "MILLIMETRE" or "METRE" or "INCH" or "CENTIMETRE", 7);
      Check (angleUnit != null, 8);
      Check (angleUnit is "DEGREE" or "RADIAN", 9);
   }

   string? GetAngleUnit (int id) {
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

   string? GetLengthUnit (int id) {
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

   ReadOnlySpan<byte> GetCompoundBytes (int id) {
      var tup = mData[id]; if (tup.Kind != COMPOUND) return [];
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
