// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dispatcher.cs
// ║║║║╬║╔╣║ Implements the IDispatcher interface when we are using WPF
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Threading;
namespace Nori;

#region class WPFDispatcher -----------------------------------------------------------------------
/// <summary>WPFDispatcher implements an IDispatcher that works with WPF</summary>
/// This implementation is trivial, since WPF already implements a Disptcher with a close-enough
/// interface, and we can just delegate calls to that. 
class WPFDispatcher : IDispatcher {
   public WPFDispatcher (Dispatcher disp) => mDispatcher = disp;
   readonly Dispatcher mDispatcher;

   public bool CheckAccess () => mDispatcher.CheckAccess ();
   public Task InvokeAsync (Action act) => mDispatcher.InvokeAsync (act).Task;
   public Task<T> InvokeAsync<T> (Func<T> func) => mDispatcher.InvokeAsync (func).Task;
   public void Post (Action act) => mDispatcher.BeginInvoke (act);
}
#endregion
