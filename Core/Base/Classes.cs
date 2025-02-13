// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Classes.cs
// ║║║║╬║╔╣║ Various utility classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class MultiDispose -------------------------------------------------------------------------
/// <summary>Helper to hold on to, and dispose, multiple IDisposables</summary>
public class MultiDispose : IDisposable {
   public MultiDispose (params IDisposable?[] disps) => mDisposables = disps;
   public void Dispose () => mDisposables.ForEach (a => a?.Dispose ());
   IDisposable?[] mDisposables;
}
#endregion
