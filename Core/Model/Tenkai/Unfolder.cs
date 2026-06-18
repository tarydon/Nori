// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Unfolder.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Unfolder -----------------------------------------------------------------------------
public class Unfolder {
   public Unfolder (Model3 model) => mModel = model;

   public Result<Dwg2, EResult> Process () {
      BendPose pose = new (mModel);
      if (!pose.Valid) return EResult.NotSheetMetalModel;

      pose.SetLie (0);
      Dwg2 dwg = new ();
      var plane = (E3Flat)pose.Nodes.First ().Ent;
      var xfmRoot = Matrix3.From (plane.CS);
      var b = pose.GetBound () * xfmRoot;
      if (b.Y.Length > b.X.Length + Lib.Delta) xfmRoot *= Matrix3.Rotation (EAxis.Z, Lib.HalfPI);
      xfmRoot *= new Matrix3 (1, 0, 0, 0, -1, 0, 0, 0, 1, 0, 0, 0);

      List<double> ends = []; double yMid = 0; 
      Span<Point2> buffer = stackalloc Point2[2];
      foreach (var node in pose.Nodes) {
         ends.Clear (); 
         E3Thick ent = node.Ent;
         var xfm = ent.ToXfm * node.Xfm * xfmRoot;
         foreach (var shape in ent.Shape) {
            AddPoly (dwg, shape, xfm);
            if (ent is E3Flex flex) {
               // If this is a flex, compute the bend-line and add it
               yMid = flex.Spine.FlatWidth / 2;
               Point2 pa = new (0, yMid), pb = new (100, yMid); 
               foreach (var seg in shape.Segs) 
                  foreach (var pt in seg.Intersect (pa, pb, buffer, true)) ends.Add (pt.X);
            }
         }
         if (ent is E3Flex flex1) { 
            ends.Sort ();
            var spine = flex1.Spine;
            for (int i = ends.Count - 1; i > 1; i--)
               if (ends[i - 1].EQ (ends[i])) ends.RemoveAt (i);
            var pts = ends.Select (x => new Point2 (x, yMid));
            dwg.Ents.Add (new E2Bendline (dwg, pts.Select (a => (Point2)(a * xfm)), spine.Angle * (spine.Upward ? -1 : 1), spine.Radius, spine.KFactor, flex1.Thickness));
         }
      }
      new DwgStitcher (dwg).Process ();
      return dwg;
   }

   void AddPoly (Dwg2 dwg, Poly poly, Matrix3 xfm) {
      if (!poly.HasOverlaps) dwg.Add (poly * xfm);
      else {
         foreach (var seg in poly.Segs) {
            if (seg.IsOverlap) continue;
            dwg.Add (seg.ToPoly () * xfm);
         }
      }
   }

   readonly Model3 mModel;
}
#endregion

public readonly struct Defer (Action action) : IDisposable {
   public readonly void Dispose () => action ();
}

public struct PT3 {
   public double X;
   public double Y;
   public double Z;

   public void Read (Stream stm) => stm.ReadExactly (MemoryMarshal.AsBytes (MemoryMarshal.CreateSpan (ref this, 24)));
}

public readonly struct Result<T, TError> where T : notnull where TError : notnull {
   public static implicit operator Result<T, TError> (T value) => new (value, default, true);
   public static implicit operator Result<T, TError> (TError error) => new (default, error, false);

   Result (T? v, TError? e, bool s) { mValue = v; mError = e; mSuccess = s; }

   public T Value {
      get {
         if (mSuccess) return mValue!;
         throw new InvalidOperationException ();
      }
   }

   public bool TryGetValue ([NotNullWhen (true)] out T? value) {
      value = mSuccess ? mValue : default;
      return mSuccess;
   }

   public TError Error {
      get {
         if (!mSuccess) return mError!;
         throw new InvalidOperationException ();
      }
   }

   public bool IsOk => mSuccess;
   public bool IsErr => !mSuccess;

   public static implicit operator bool (Result<T, TError> t) => t.mSuccess;

   public TResult Match<TResult> (Func<T, TResult> success, Func<TError, TResult> error) 
      => mSuccess ? success (mValue!) : error (mError!);

   readonly T? mValue;
   readonly TError? mError;
   readonly bool mSuccess;
}
