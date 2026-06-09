// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Dispatcher.cs
// ║║║║╬║╔╣║ Implements the IDispatcher interface when we are using GLFW
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections.Concurrent;
namespace Nori;

#region class GLFWDispatcher -----------------------------------------------------------------------
/// <summary>GLFWDispatcher implements an IDispatcher that works with the GLFW message loop</summary>
/// After a GLFWHost.Init, this dispatcher is constructed and installed into Hub.Dispatcher, 
/// and can be used by clients. In particular, it is used to implement things like Lib.Post(), or
/// Lib.Timer(). The thread in which this call happens effectively becomes the UI thread, and the
/// normal mechanism would be to do this in the main thread of your application. Unlike WPF, there
/// is NO requirement for this thread to be labelled [STAThread]. 
/// 
/// When using GLFW, we are in charge of the message loop (you can see that implemented in 
/// Window.Run). Each iteration of that message pumping loop works like this:L
/// - If there is any work queued up (using IDispatcher.Post, for example), execute those
///   work items.
/// - Call Lux.OnPaint, which does the following:
///    = if there is a UIScene currently installed, this draws the scene
///    = if not, it simply clears the screen with a default color and returns
/// - Do a SwapBuffers to make the scene that was just drawn in the step above visible
///   on the monitor
/// - If an animation is running, do glfwPollEvents (non-blocking), otherwise do a 
///   glfwWaitEvents (blocking). This processes all the messages from keyboard / mouse etc and
///   then either goes to sleep (if wait) or returns immediately
/// - Go back to the beginning of the loop and continue. 
/// 
/// So, when an animation is running, the loop is continuously running at monitor refresh rate
/// (or slower if your draw code is complex and cannot finish within one frame time). Otherwise,
/// the system is sleeping until some events come in (keyboard / mouse / timers etc)
class GLFWDispatcher : IDispatcher {
   // Methods ------------------------------------------------------------------
   /// <summary>Returns true if we are currently on the UI thread</summary>
   public bool CheckAccess () 
      => mThreadID == Environment.CurrentManagedThreadId;

   /// <summary>Schedules a action to be executed asynchronously on the UI thread</summary>
   /// Even if we are currently on the UI thread, the action is posted, and never executed
   /// immediately. Consider this code:
   ///    var task = Hub.Dispatcher.InvokeAsync (() => Lib.Trace ("A"));
   ///    Lib.Trace ("B");
   /// This will print B before it prints A, even if we are currently in the UI thread. This is
   /// similar to WPF dispatcher behavior, but different from Windows Forms dispatcher behavior. 
   /// 
   /// This returns a Task that can be awaited if you want to know when the action is complete.
   /// In practice, you rarely use this directly, but this is used indirectly by the 
   /// SynchronizationContext implementation below (which in turn is used by async/await)
   public Task InvokeAsync (Action act) {
      var item = new WorkItemAct (act);
      mWorkQueue.Enqueue (item);
      GLFW.PostEmptyEvent ();
      return item.Task;
   }

   /// <summary>Schedules a function to be executed on the UI thread</summary>
   /// Return a Task(T) that can be awaited to know when the function finishes, and to get its
   /// result after that. 
   public Task<T> InvokeAsync<T> (Func<T> func) {
      var item = new WorkItemFunc<T> (func);
      mWorkQueue.Enqueue (item);
      GLFW.PostEmptyEvent ();
      return item.Task;
   }

   /// <summary>Posts an action to be executed on the UI thread</summary>
   /// This is like a fire-and-forget action that you can post and know that it will be
   /// executed on the UI thread (typically on the next frame)
   public void Post (Action act) {
      var item = new WorkItemPost (act);
      mWorkQueue.Enqueue (item);
      GLFW.PostEmptyEvent ();
   }

   // Implementation -----------------------------------------------------------
   // This helper is called on each iteration of the message pump to execute any queued
   // work items (could be queued by Post, InvokeAsync, InvokeAsync<T>)
   internal void ProcessWorkQueue () {
      while (mWorkQueue.TryDequeue (out WorkItem? item)) {
         try {
            item.Execute ();
         } catch (Exception ex) {
            Lib.Trace ($"Work queue exception: {ex}");
         }
      }
   }

   readonly int mThreadID = Environment.CurrentManagedThreadId;
   readonly ConcurrentQueue<WorkItem> mWorkQueue = [];
}
#endregion

#region class WorkItem -----------------------------------------------------------------------------
// Base class for work that will be placed in the dispatcher queue.
// Each of Post, InvokeAsync, InvokeAsync<T> have a slightly different _shape_ of work item 
// (see the derived classes below), but all of them have a common Execute method. 
abstract class WorkItem {
   public abstract void Execute (); 
}
#endregion

#region class WorkItemPost -------------------------------------------------------------------------
// WorkItem implementation to handle Dispatcher.Post.
// In this case, we don't create a Task, since it is not possible to await or continue
// an action that is fed to Dispatcher.Post(). Also, we don't handle any executions at this 
// level, and leave it to the dispatcher message pump to handle (similar in style to WPF 
// dispatcher Post)
sealed class WorkItemPost (Action action) : WorkItem {
   public override void Execute () => mAction ();
   readonly Action mAction = action;
}
#endregion

#region class WorkItemAct --------------------------------------------------------------------------
// WorkItem implementation to handle Dispatcher.InvokeAsync. 
// In this case, we create a Task (which is returned from Dispatcher.InvokeAsync). 
// The task is used by the caller to await, or also to pass back any execptions that were 
// raised during the execution of the action
sealed class WorkItemAct (Action action) : WorkItem {
   // Interface ----------------------------------------------------------------
   public override void Execute () {
      try {
         mAction ();
         mTCS.SetResult (null);
      } catch (Exception ex) {
         mTCS.SetException (ex);
      }
   }

   public Task Task => mTCS.Task;

   // Implementation -----------------------------------------------------------
   // Even when the task trivially completes, we want the continuation to run only
   // asnychronously, never 'inline' (this matches WPF semantics and is the least surprising
   // behavior). So the RunContinuationsAsynchronously flag is important. 
   readonly TaskCompletionSource<object?> mTCS = new (TaskCreationOptions.RunContinuationsAsynchronously);
   readonly Action mAction = action;
}
#endregion

#region class WorkItemFunc -------------------------------------------------------------------------
// WorkItem implemetnation to handle Dispatcher.InvokeAsync<T>
// In this case, we create a Task<T> which is returned from Dispatcher.InvokeAsync<T>. 
// The task can be used by callers to await and get the result, and is also used to pass back
// any exceptions that were raised during the computation of the function
sealed class WorkItemFunc<T> (Func<T> func) : WorkItem {
   // Interface ----------------------------------------------------------------
   public override void Execute () {
      try {
         mTCS.SetResult (mFunc ());
      } catch (Exception ex) {
         mTCS.SetException (ex);
      }
   }

   public Task<T> Task => mTCS.Task;

   // Implementation -----------------------------------------------------------
   readonly Func<T> mFunc = func;
   readonly TaskCompletionSource<T> mTCS = new (TaskCreationOptions.RunContinuationsAsynchronously);
}
#endregion

#region class GLFWSyncContext ----------------------------------------------------------------------
// Customized SynchronizationContext used with GLFWDispatcher
// 
// During GLFWHost.Init(), an instance of this is created and set as the current synchronization 
// context. The implementation is quite trivial since it just delegates Post and Send to the 
// corresponding methods in the dispatcher. The other methods of SynchronizationContext are not
// overridden, since their default implementations are already acceptable, or use Post and Send 
// internally anyway. 
// Note: we also override CreateCopy to trivially return self, since this has no state (other than
// the dispatcher, which has to be shared with the copy anyway)
class GLFWSyncContext (IDispatcher dispatcher) : SynchronizationContext {
   // Interface ----------------------------------------------------------------
   public override void Post (SendOrPostCallback d, object? state) => mDispatcher.Post (() => d (state));
   public override void Send (SendOrPostCallback d, object? state) => mDispatcher.Send (() => d (state));
   public override SynchronizationContext CreateCopy () => this;

   // Implementation -----------------------------------------------------------
   readonly IDispatcher mDispatcher = dispatcher;
}
#endregion
