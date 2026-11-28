using static Nori.STEP.EKind;
namespace Nori;

public partial class STEPReader {
   // Parse routines -----------------------------------------------------------
   // This parses a PLANE_ANGLE_UNIT (either a SI_UNIT of "Radian", or a 
   // CONVERSION_BASED_UNIT of "Degree"). For more details, see ParseLengthUnit below
   string? ParseAngleUnit (int id) {
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
      return GetCompoundString (id); ;
   }

   // This parses a REPRESENTATION_CONTEXT of this form:
   // 
   // #2799=(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT((
   // #2798))GLOBAL_UNIT_ASSIGNED_CONTEXT((#2792,#2796,#2797))REPRESENTATION_CONTEXT(
   // 'ID1','3'));
   // 
   // From this, we want to extract a few important bits of information. 
   // - The Scale of the model, relative to mm (for example, if this model is an inch model,
   //   this will be set to 25.4)
   // - The AngScale of the model, relative to radians (for example, if the angular units for
   //   this are degrees, this would be set to PI/180)
   // - The Fudge tolerance for checking whether curve connect, for planarity etc
   // The first two are obtained from the GLOBAL_UNIT_ASSIGNED_CONTEXT, and the final one from
   // the GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT
   (double Scale, double AngScale, double Fudge) ParseContext (int nContext) {
      // Fetch the GLOBAL_UNIT_ASSIGNED_CONTEXT, this contains the units for linear measure,
      // plane angle measure and solid angle measure
      Check (FindInCompound (nContext, "GLOBAL_UNIT_ASSIGNED_CONTEXT"u8), 5);
      mR.Match ('('); int[] ids = RRefs ();
      string? lengthUnit = null, angleUnit = null, lengthUnit2 = null;
      foreach (var id in ids) {
         lengthUnit ??= ParseLengthUnit (id);
         angleUnit ??= ParseAngleUnit (id);
      }
      Check (lengthUnit != null, 6);
      Check (lengthUnit is "MILLIMETRE" or "METRE" or "INCH" or "CENTIMETRE", 7);
      Check (angleUnit != null, 8);
      Check (angleUnit is "DEGREE" or "RADIAN", 9);
      double scale = lengthUnit switch { "METRE" => 1000, "INCH" => 25.4, "CENTIMETRE" => 10, _ => 1 };
      double angScale = angleUnit switch { "DEGREE" => Lib.PI / 180, _ => 1 };

      // Fetch the GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT, from this we can get the fudge factor
      // to use for the import
      double fudge = 0.001;
      Check (FindInCompound (nContext, "GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT"u8), 27);
      mR.Match ('('); ids = RRefs ();
      foreach (var id in ids) {
         var tup = Data[id];
         if (tup.Kind == UNCERTAINTY_MEASURE_WITH_UNIT) {
            var uc = Uncertainty[tup.Index];
            lengthUnit2 = ParseLengthUnit (uc.Unit) ?? "";     // TO-DO : remove this entirely?
            fudge = scale * uc.Factor;
            break;
         }
      }
      lengthUnit2 ??= lengthUnit;
      Check (lengthUnit == lengthUnit2, 28);
      return (scale, angScale, fudge);
   }

   // Parses a LENGTH_UNIT and returns a text like "MILLIMETER" or "INCH". 
   // This is one form of LENGTH_UNIT:
   // 
   // #12=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.));
   // 
   // and this is the other form:
   // 
   // #15=(CONVERSION_BASED_UNIT('INCH',#16)LENGTH_UNIT()NAMED_UNIT(*));
   // #16=LENGTH_MEASURE_WITH_UNIT(LENGTH_MEASURE(2.54E1),#17);
   // #17=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.));
   // 
   // For the second form, we are not actually doing a detailed parsing, we are just
   // using the label of the CONVERSION_BASED_UNIT which seems typically set to "INCH"
   // This is not robust, and needs to be improved
   string? ParseLengthUnit (int id) {
      var span = GetCompoundBytes (id);
      if (!Seek (span, "LENGTH_UNIT"u8)) return null;
      if (Seek (span, "SI_UNIT"u8)) {
         mR.Match ('('); return REnum () + REnum ();
      }
      if (Seek (span, "CONVERSION_BASED_UNIT"u8)) {   // TO-DO: 
         // The conversion-based unit of 'INCH' is much less common
         mR.Match ('(');
         string s = RString ().ToUpperInvariant ();
         if (s.Length > 5 && s.EndsWith (" INCH")) return "INCH";
         return s;
      }
      return null;
   }
}
