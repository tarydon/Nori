// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Collections.cs
// ║║║║╬║╔╣║ Implements collections (including the AList - active list)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections;
using System.Reactive.Subjects;
namespace Nori;

#region interface IAList ---------------------------------------------------------------------------
/// <summary>The IAList interface signals a collection as an 'active list'</summary>
/// Any type that implements IList (to fetch object at given index) and IObservable(ListChange)
/// (to know when items are added / removed from that list) can serve as an IAList interface.
/// In particular, AList(T) below implements IAList
public interface IAList : IList, IObservable<ListChange>;
#endregion

#region class AList<T> -----------------------------------------------------------------------------
/// <summary>AList implements an Observable list that notifies subscribers when it is modified</summary>
public class AList<T> : IReadOnlyList<T>, IList<T>, IAList {
   // Properties ---------------------------------------------------------------
   /// <summary>The count of elements in this list</summary>
   public int Count => mList.Count;

   // Methods ------------------------------------------------------------------
   /// <summary>Add an item to the list, fires the 'Added' notification after adding</summary>
   public void Add (T item) { mList.Add (item); Fire (ListChange.E.Added, mList.Count - 1); }

   /// <summary>Clear the list, fires the 'Clearing' notification before clearing</summary>
   public void Clear () { Fire (ListChange.E.Clearing, mList.Count - 1); mList.Clear (); }

   /// <summary>Returns true if the list contains the given item</summary>
   public bool Contains (T item) => mList.Contains (item);

   /// <summary>Returns the index of the item in the list (or -1 if it is not found)</summary>
   public int IndexOf (T item) => mList.IndexOf (item);

   /// <summary>Insert an item at a given index, and fires the 'Added' notification afterwards</summary>
   public void Insert (int index, T item) { mList.Insert (index, item); Fire (ListChange.E.Added, index); }

   /// <summary>Removes the first occurance of the given item from the list (fires 'Removing' event before removing)</summary>
   /// Returns true if the item was found and removed. If the item is not found, the
   /// 'Removing' event will not be fired
   public bool Remove (T item) {
      int index = IndexOf (item); if (index == -1) return false;
      RemoveAt (index); return true;
   }

   /// <summary>Removes the element at a given index (fires the 'Removing' event before removing)</summary>
   public void RemoveAt (int index) { Fire (ListChange.E.Removing, index); mList.RemoveAt (index); }

   /// <summary>Implementation of the Observable(ListChange) interface</summary>
   public IDisposable Subscribe (IObserver<ListChange> observer) => (mSubject ??= new ()).Subscribe (observer);

   // Indexer ------------------------------------------------------------------
   /// <summary>Indexer used to read / write elements at a particular index</summary>
   /// When an element is modified, we treat it as a Remove followed by an insert,
   /// so there is a Removing event, followed by an Added event
   public T this[int index] {
      get => mList[index];
      set {
         Fire (ListChange.E.Removing, index);
         mList[index] = value;
         Fire (ListChange.E.Added, index);
      }
   }

   // Type convertors ----------------------------------------------------------
   /// <summary>Implicit conversion from AList to ReadOnlySpan</summary>
   public static implicit operator ReadOnlySpan<T> (AList<T> list) => list.mList.AsSpan ();

   // IList implementation -----------------------------------------------------
   public bool IsReadOnly => false;
   public bool IsFixedSize => false;
   public bool IsSynchronized => false;
   public object SyncRoot => mList;
   public int Add (object? value) { Add ((T)value!); return mList.Count - 1; }
   public bool Contains (object? value) => Contains ((T)value!);
   public int IndexOf (object? value) => IndexOf ((T)value!);
   public void Insert (int index, object? value) => Insert (index, (T)value!);
   public void Remove (object? value) => Remove ((T)value!);
   public void CopyTo (Array array, int index) => throw new NotImplementedException ();
   object? IList.this[int index] { get => mList[index]; set => mList[index] = (T)value!; }
   IEnumerator IEnumerable.GetEnumerator () => mList.GetEnumerator ();

   // IList<T> implementation --------------------------------------------------
   /// <summary>Copies the contents of this AList into the given array, starting at the given index</summary>
   /// If the array is not large enough to hold the elements, this throws an exception.
   public void CopyTo (T[] array, int arrayIndex) => mList.CopyTo (array, arrayIndex);

   /// <summary>Creates an enumerator that can be used to walk through the elements in the AList</summary>
   /// This is used when the AList is used as the target for a foreach expression
   public IEnumerator<T> GetEnumerator () => mList.GetEnumerator ();

   // Implementation -----------------------------------------------------------
   void Fire (ListChange.E action, int index) {
      if (mFiring) throw new Exception ("AList modified inside change observer");
      mFiring = true;
      try { mSubject?.OnNext (new (action, index)); } finally { mFiring = false; }
   }
   Subject<ListChange>? mSubject;
   readonly List<T> mList = [];
   bool mFiring;
}
#endregion

#region class Chains<T> ----------------------------------------------------------------------------
/// <summary>Implements a collection of linked-lists efficiently in a single array</summary>
/// A collection of multiple linked lists residing in a single array is called a 'Chains' data
/// structure. Suppose we want to maintain multiple linked-lists of T with efficient adding, removal
/// and flushing of chains. We don't want to use a conventional linked-list structure since we don't want
/// the expense of a separate memory allocation for every node (especially important if each node is a tiny
/// payload, such as an integer).
///
/// We create a Chains&lt;T&gt; structure and allocate integers to hold the 'chain numbers'. These are like
/// linked-list 'handles'. We create a new chain, or add to an existing chain by using the Add method. We
/// pass in the chain-handle as the first parameter to this. If this is 0, we are creating a new chain (and
/// that parameter gets set to the freshly allocated chain-handle). If this is non-zero, we are adding a new
/// value to that chain. Note that even when we are adding a new value to an existing chain, the chain-handle
/// will actually be changed by the Add method. In other words, each time we add to a chain, the chain
/// handle changes! Since this is a ref parameter, the handle changing underneath will not require any
/// special handling.
///
/// We can later remove one item out of a chain, or 'release' all the elements from a chain completely.
/// During all these operations, no actual movement of the elements takes place; links are adjusted to
/// effect these changes, and the 'empty spaces' are used subsequently when we add something to a chain.
/// Thus, the memory used by a Chains structure monotonically increases while it is in use; it never
/// reduces.
public class Chains<T> {
   /// <summary>Add a value to an already existing chain, or create a new chain with the value (takes O(1) time)</summary>
   /// <param name="chain">The chain-handle; if this is 0, we are effectively creating a new chain</param>
   /// <param name="value">The value to be added to that chain</param>
   /// This returns nothing, but always modifies the chain-handle parameter 'chain'. If this is a new
   /// chain, the value will change from 0 to some non-zero value. Even if this is an existing chain,
   /// the value will change (in effect, imagine that a new chain is always created by appending this
   /// new value to the previous chain).
   public void Add (ref int chain, T value) {
      if (mFree.Count == 0) {
         // There are no free spaces left in the Data array. Create some
         if (mLinks.Length == 0) {
            // This is the first time add has been called, so initialize all the arrays
            Data = new T[8]; mLinks = new int[8];
            for (int i = 7; i >= 1; i--) mFree.Push (i);
         } else {
            int n = Data.Length;
            Array.Resize (ref Data, n * 2); Array.Resize (ref mLinks, n * 2);
            for (int i = n * 2 - 1; i >= n; i--) mFree.Push (i);
         }
      }
      int next = mFree.Pop ();
      mLinks[next] = chain; Data[next] = value;
      chain = next;
   }

   /// <summary>Returns true if the chain contains a given value (takes O(N) time, where N is the chain length)</summary>
   /// <param name="chain">The chain-handle of the chain</param>
   /// <param name="value">The value to search for (the Equals method is used to compare)</param>
   /// <returns>True if the chain contains the given value, false otherwise</returns>
   public bool Contains (int chain, T value) {
      while (chain > 0) {
         if (Data[chain]?.Equals (value) == true) return true;
         chain = mLinks[chain];
      }
      return false;
   }

   /// <summary>Enumerates the values from a given chain</summary>
   /// <param name="chain">The chain-handle from which we want to get the values</param>
   public IEnumerable<T> Enum (int chain) {
      while (chain != 0) {
         yield return Data[chain];
         chain = mLinks[chain];
      }
   }

   /// <summary>Gathers the raw indices of the elements from a given chain</summary>
   /// <param name="chain">The chain-handle whose elements we want to gather</param>
   /// <param name="indices">A list into which the indicaes are added</param>
   /// This is a fairly 'low-level' method that you should not really be required to use, except if
   /// you want to manipulate some 'value-type' elements in the chain, in-situ. The indices returned here
   /// can be used to index into the Data[] array, which holds the raw elements in the chain. Use with
   /// great care!
   ///
   /// For most normal use, the Enum() method gives a simpler way to enumerate the values from a chain.
   public void GatherRawIndices (int chain, List<int> indices) {
      indices.Clear ();
      while (chain != 0) { indices.Add (chain); chain = mLinks[chain]; }
   }

   /// <summary>This releases an entire chain of values (takes O(N) time, where N is the length of the chain)</summary>
   /// After this, the chain-handle will be 0, to indicate an empty chain, and the space
   /// used by the erstwhile values will be released into the free-pool for subsequent reuse.
   public void ReleaseChain (ref int chain) {
      while (chain > 0) {
         mFree.Push (chain);
         chain = mLinks[chain];
      }
   }

   /// <summary>Removes one value from a chain - takes O(N) time, where N is the length of the chain.</summary>
   /// <param name="chain">The chain-handle in question (this may be modified by the routine)</param>
   /// <param name="value">The value to search for and remove</param>
   public void Remove (ref int chain, T value) {
      if (chain == 0) return;

      // Handle the case where Data is the first element in the array
      if (Data[chain]?.Equals (value) == true) {
         mFree.Push (chain);
         chain = mLinks[chain];
         return;
      }

      int prev = 0, elem = chain;
      while (elem > 0) {
         if (Data[elem]?.Equals (value) == true) {
            // Found the item; connect the previous to the next to skip this
            // element in the chain
            mFree.Push (elem);
            mLinks[prev] = mLinks[elem];
            return;
         }
         prev = elem; elem = mLinks[elem];
      }
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The raw Data making up the chain</summary>
   /// This should not normally be required to be accessed. Advanced scenarios which require
   /// efficient in-place modification of value-type elements within some chains may require
   /// this. In that case, use GatherRawIndices to get the indices into this array of the elements
   /// within a chain.
   public T[] Data = [];

   // Private data -------------------------------------------------------------
   // Contains the 'links' connecting elements into chains
   int[] mLinks = [];
   // Contains the indices of slots in the Data array that are free
   readonly Stack<int> mFree = [];
}
#endregion

#region class IdxHeap<T> ---------------------------------------------------------------------------
/// <summary>IdxHeap maintains a collection of IIndexed objects</summary>
/// Alloc() allocates a new object, assigns an index to it, and stores it in the heap.
/// Release() releases that object, and adds the slot it previously used to a free-list,
/// which allows these slots to be recycled. Internally, the IdxHeap just maintains a
/// list-of-T, except that some of the slots in the list could now be empty, and reused.
/// This mechanism allows O(1) time for allocation, freeing and for indexing.
public class IdxHeap<T> where T : IIndexed, new() {
   /// <summary>Create an empty IdxHeap</summary>
   public IdxHeap () => Resize (8);

   /// <summary>Count of live objects in the IdxHeap</summary>
   /// The final -1 is because we never use index 0 - it is reserved so that
   /// we can reliable use 0 as a sentinel to mean 'no object'
   public int Count => mData.Length - mFree.Count - 1;

   /// <summary>Allocate a new object and return it</summary>
   /// This stores the object in the mData array at a given index, and
   /// sets the Idx of that object to that index
   public ref T Alloc () {
      if (mFree.Count == 0) Resize (mData.Length * 2);
      int idx = mRecent = mFree.Pop ();
      mData[idx] = new ();
      ref T obj = ref mData[idx]!;
      obj.Idx = idx;
      return ref obj;
   }
   // The object we most recently allocated
   int mRecent;

   /// <summary>Reference to the most recently allocated T</summary>
   public ref T Recent => ref this[mRecent];

   /// <summary>Release the object at a specified index</summary>
   public void Release (int idx) {
      mData[idx] = default;
      mFree.Push (idx);
   }

   /// <summary>Retrieves the object at a given index</summary>
   public ref T this[int idx]
      => ref mData[idx]!;

   // Implementation -----------------------------------------------------------
   public List<T> GetSnapshot () {
      List<T> items = [];
      for (int i = 0; i < mData.Length; i++) 
         if (!mFree.Contains (i) && mData[i] != null) items.Add (mData[i]!);
      return items;
   }

   void Resize (int n) {
      if (n > mData.Length) {
         int n0 = mData.Length;
         Array.Resize (ref mData, n);
         for (int i = n - 1; i >= n0; i--) mFree.Push (i);
      }
   }

   public override string ToString ()
      => $"IdxHeap<{typeof (T).Name}>, Count={Count}";

   // Private data -------------------------------------------------------------
   // The indices of free slots, if any
   readonly Stack<int> mFree = [];
   // The array that maintains the actual objects (we grow this as needed)
   T?[] mData = [default];
}
#endregion

#region struct ListChange --------------------------------------------------------------------------
/// <summary>Represents a change happening in a list (adding / removing)</summary>
public readonly struct ListChange {
   internal ListChange (E action, int index) => (Action, Index) = (action, index);
   public override string ToString () => $"{Action}({Index})";

   /// <summary>The type of action happening on the List</summary>
   public enum E {
      /// <summary>Element added at given index</summary>
      Added,
      /// <summary>About to remove the element at the given index</summary>
      Removing,
      /// <summary>About to remove all elements from the set</summary>
      Clearing
   }

   /// <summary>The action that happened (add / remove etc)</summary>
   public readonly E Action;
   /// <summary>The index at which the action happened</summary>
   public readonly int Index;
}
#endregion
