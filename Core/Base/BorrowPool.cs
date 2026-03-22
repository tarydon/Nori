// ────── ╔╗
// ╔═╦╦═╦╦╬╣ BorrowPool.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Threading;
namespace Nori;

#region interface IBorrowable<T> -------------------------------------------------------------------
/// <summary>Borrowable objects implement this interface</summary>
public interface IBorrowable<T> : IDisposable where T : class {
   static abstract T Make ();
   static abstract ref T? Next (T item);
}
#endregion

#region BorrowPool<T> ------------------------------------------------------------------------------
/// <summary>Implements a pool from which objects can be borrowed, used, and returned</summary>
/// Typically this is used to store algorithm objects like PolyBuilder, MeshSlicer etc. 
/// We don't want to keep re-creating those kinds of objects for each poly-build or mesh-slice
/// operation, but would like to reuse one that we create. There are different solutions to 
/// this:
/// 
/// 1. Singleton - created on first use
/// This obvious solution of a singleton will break down in multi-threaded code, since 
/// multiple threads may try to borrow the object for use at the same time. It is used in Nori
/// for some objects like Shaders, since those can only be used from the UI thread, but is not
/// suitable for general purpose use.
/// 
/// 2. [ThreadStatic] - one instance per thread, created on first use
/// This works better, but cannot protect against re-entrancy when function A has fetched and
/// is using MeshBuilder, and it calls function B, which also tries to fetch and use MeshBuilder.
/// Both being on the same thread, they will both get the same instance, with bad results
/// 
/// 3. BorrowPool - objects borrowed from and returned to a pool
/// A pool is created with objects that are idling and ready for use (initially this pool is empty).
/// Each Borrow() call will remove and return an object from the pool. If the pool is empty, it
/// will create a new object and return it. Each return call will add the object back to the pool.
/// So the pool grows only as big as the maximum number of concurrent objects that were ever in use.
/// 
/// Some optimizations are done to make this very fast.
/// a. The pool has a sFast slot that holds exactly one object. If this slot is occupied, it
///    is returned and the slot set to null. When an object is returned, if this slot is null,
///    it is placed in the slot. So in the (common) case where only one object is in use at one 
///    time, this slot acts simply like a Singleton (except that it is cleared to null when the
///    object is borrowed).
/// b. Only overflows from this sFast slot go into the pool. The pool itself is maintained as a
///    simple linked list (by means of an intrusive pointer stored in T, and exposed via the 
///    IBorrowable interface). 
///
/// Safety:
/// The sFast is read/updated via Interlocked.CompareExchange so there is never any
/// race condition or garbling of that even under heavy contention. The linked-list update
/// (which is always O(1) time) is protected by a SpinLock. A SpinLock is acceptable in this 
/// situation since the code inside the lock will take only a few clock cycles deterministically.
/// 
/// Sentinels are used to ensure that only objects that are borrowed are returned. Double-returns
/// are also caught in debug mode. If objects are borrowed and never returned, the pool will
/// eventually detect that (when 4*CPUCount objects are borrowed). 
/// 
/// Ergonomics: 
/// The objects borrowed implement IDisposable, and one must return them back to the pool
/// by calling Dispose(). The [MustDisposeResource] JetBrains attribute is used to decorate
/// the Borrow call so that Resharper will detect and warn if the borrow is not disposed. 
/// The correct usage would be like this:
/// <code>
/// using var tess = Tessellator.Borrow ();           // Note the using statement!
/// tess.AddVertex (...); tess.AddVertex (...);
/// var tris = tess.Process ();
/// </code>
/// 
/// Resharper will warn if the result of Borrow() is not disposed (either directly, or
/// via a using statement). If you do not have Resharper installed, you can use the free
/// command line 'jb' tool which can perform JetBrains code inspections to catch this
/// (and other errors) in your code. 
public static partial class BorrowPool<T> where T : class, IBorrowable<T> {
   public static T Borrow () {
      // First, try to take the fast path - there could be a T stored in the 
      // sFast static. If there, return it. Otherwise, that 'hot T' is being used, and we
      // need to fetch / make another one. 
      var item = Interlocked.Exchange (ref sFast, null);
      if (item == null) {
         // Couldn't grab the sFast - get the first item from the linked-list
         // of T (unless that list is empty). Note that we protect this linked-list access
         // with a SpinLock (OK since the protected code is very tiny, deterministic and cannot
         // block). 
         bool tmp = false; sLock.Enter (ref tmp);
         if ((item = sHead) != null) sHead = T.Next (item);
         sLock.Exit ();
      }
      // If the linked-list is also empty, we have to create a new item
      item ??= T.Make ();
      TrackBorrow (item);
      return item;
   }

   public static void Return (T item) {
      TrackReturn (item);
      // If the sFast slot is empty, we can simply return this item to that slot
      // (so it will get used on the next Borrow)
      if (Interlocked.CompareExchange (ref sFast, item, null) == null) return;
      // Otherwise, we have to add it to the linked-list (protected with a 
      // SpinLock)
      bool tmp = false; sLock.Enter (ref tmp);
      T.Next (item) = sHead; sHead = item;
      sLock.Exit ();
   }

   // Debug-only methods -------------------------------------------------------
   static partial void TrackBorrow (T item);
   static partial void TrackReturn (T item);

   // Private data -------------------------------------------------------------
   static SpinLock sLock = new ();
   static T? sFast, sHead;
}

#if DEBUG
static partial class BorrowPool<T> {
   // Debug code runs on each Borrow call
   static partial void TrackBorrow (T item) {
      // First, we ensure that we are not having too many objects in-flight. This almost always
      // means that somewhere we forgot to Return() the objects we are borrowing
      Debug.Assert (sBorrowed <= 4 * Environment.ProcessorCount, 
                    $"BorrowPool<{typeof (T).Name}>: too many rented, probable leak");
      // Then, we set item.Next=item as a Sentinel. (When the item is returned, that will get 
      // reset to null). This serves two purposes:
      // 1. We ensure only objects that are Borrowed() are ever Returned(). 
      // 2. We ensure an object is not returned twice
      Interlocked.Increment (ref sBorrowed);
      // Note that this is a clever use of self-reference as a sentinel. T.Next==T can never occur
      // for items in the pool. Either the item is in the sFast slot (and Next=null), or the item
      // is in the linked list (and Next=next-item or null). The only case where T.Next==T is
      // for an item that is borrowed, and 'in-use'
      T.Next (item) = item;
   }

   // Debug code that runs on each Return call
   static partial void TrackReturn (T item) {
      // First, ensure that we are only returning an item that was earlier borrowed
      Debug.Assert (T.Next (item) == item, "Pool: double return or returning unrented object");
      Interlocked.Decrement (ref sBorrowed);
      // Reset the sentinel (to detect double-returns later)
      T.Next (item) = null;
   }
   static int sBorrowed;
}
#endif
#endregion
