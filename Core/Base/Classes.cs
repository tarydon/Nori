// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Classes.cs
// ║║║║╬║╔╣║ Various utility classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class MultiDispose -------------------------------------------------------------------------
/// <summary>Helper to hold on to, and dispose, multiple IDisposables</summary>
public class MultiDispose (params IDisposable?[] disps) : IDisposable {
   public void Dispose () => mDisposables.ForEach (a => a?.Dispose ());
   IDisposable?[] mDisposables = disps;
}
#endregion
