using System.Collections.Concurrent;
using System.Data;
namespace Nori;

class GLFWSyncContext (IDispatcher dispatcher) : SynchronizationContext {
   public override void Post (SendOrPostCallback d, object? state) {
      mDispatcher.Post (() => d (state));
   }

   public override void Send (SendOrPostCallback d, object? state) => mDispatcher.Send (() => d (state));

   public override SynchronizationContext CreateCopy () => this;

   readonly IDispatcher mDispatcher = dispatcher;
}

class GLFWDispatcher : IDispatcher {
   public GLFWDispatcher () => mThreadID = Environment.CurrentManagedThreadId;

   public bool CheckAccess () 
      => mThreadID == Environment.CurrentManagedThreadId;

   public Task InvokeAsync (Action act) {
      var item = new WorkItemAct (act);
      mWorkQueue.Enqueue (item);
      GLFW.PostEmptyEvent ();
      return item.Task;
   }

   public Task<T> InvokeAsync<T> (Func<T> func) {
      var item = new WorkItemFn<T> (func);
      mWorkQueue.Enqueue (item);
      GLFW.PostEmptyEvent ();
      return item.Task;
   }

   public void Post (Action act) {
      var item = new WorkItemPost (act);
      mWorkQueue.Enqueue (item);
      GLFW.PostEmptyEvent ();
   }

   internal void ProcessWorkQueue () {
      while (mWorkQueue.TryDequeue (out WorkItem? item)) {
         try {
            item.Execute ();
         } catch (Exception ex) {
            Lib.Trace ($"Work queue exception: {ex}");
         }
      }
   }

   readonly int mThreadID;
   readonly ConcurrentQueue<WorkItem> mWorkQueue = [];
}

abstract class WorkItem {
   public abstract void Execute (); 
}

sealed class WorkItemAct (Action action) : WorkItem {
   public override void Execute () {
      try {
         mAction ();
         mTCS.SetResult (null);
      } catch (Exception ex) {
         mTCS.SetException (ex);
      }
   }

   public Task Task => mTCS.Task;

   readonly Action mAction = action;
   readonly TaskCompletionSource<object?> mTCS = new (TaskCreationOptions.RunContinuationsAsynchronously);
}

sealed class WorkItemFn<T> (Func<T> func) : WorkItem {
   public override void Execute () {
      try {
         mTCS.SetResult (mFunc ());
      } catch (Exception ex) {
         mTCS.SetException (ex);
      }
   }

   public Task<T> Task => mTCS.Task;

   readonly Func<T> mFunc = func;
   readonly TaskCompletionSource<T> mTCS = new (TaskCreationOptions.RunContinuationsAsynchronously);
}

sealed class WorkItemPost (Action action) : WorkItem {
   public override void Execute () => mAction ();
   readonly Action mAction = action; 
}