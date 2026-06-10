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

      foreach (var node in pose.Nodes) {
         E3Thick ent = node.Ent;
         var xfm = ent.ToXfm * node.Xfm * xfmRoot;
         foreach (var shape in ent.Shape) AddPoly (dwg, shape, xfm);
      }
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
