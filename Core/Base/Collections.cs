// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Collections.cs
// ║║║║╬║╔╣║ Implements collections (including the AList - active list)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections;
using System.Reactive.Subjects;
namespace Nori;

#region struct ListChange --------------------------------------------------------------------------
/// <summary>Represents a change happening in a list (adding / removing)</summary>
public readonly struct ListChange {
   internal ListChange (E action, int index) => (Action, Index) = (action, index);
   public override string ToString () => $"{Action}({Index})";

   public enum E {
      /// <summary>Element added at given index</summary>
      Added,
      /// <summary>About to remove the element at the given index</summary>
      Removing,
      /// <summary>About to remove all elements from the set</summary>
      Clearing,
   }

   /// <summary>The action that happened (add / remove etc)</summary>
   public readonly E Action;
   /// <summary>The index at which the action happened</summary>
   public readonly int Index;
}
#endregion

#region class AList<T> -----------------------------------------------------------------------------
/// <summary>AList implements an Observable list that notifies subscribers when it is modified</summary>
public class AList<T> : IList, IList<T>, IObservable<ListChange> {
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
   static public implicit operator ReadOnlySpan<T> (AList<T> list) => list.mList.AsSpan ();

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
   public void CopyTo (T[] array, int arrayIndex) => mList.CopyTo (array, arrayIndex);
   public IEnumerator<T> GetEnumerator () => mList.GetEnumerator ();

   // Implementation -----------------------------------------------------------
   void Fire (ListChange.E action, int index) => mSubject?.OnNext (new (action, index));
   Subject<ListChange>? mSubject;
   List<T> mList = [];
}
#endregion
