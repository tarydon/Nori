// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Ledger.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections;
using System.Reactive.Subjects;
namespace Nori;

#region enum ELChange ------------------------------------------------------------------------------
/// <summary>Enumerates the types of changes that can happen on a ledger</summary>
public enum ELChange {
   /// <summary>Notification fired when an item is added</summary>
   Add = 1,
   /// <summary>Notification when an item is removed</summary>
   Remove = 2,
   /// <summary>Notification fired when an item is replaced</summary>
   Replace = 3,
   /// <summary>Notification that the 'current' value has changed</summary>
   Current = 5,
}
#endregion

#region struct LChange<T> --------------------------------------------------------------------------
/// <summary>Represents a change on a Ledger(T)</summary>
public readonly struct LChange<T> {
   public LChange (ELChange kind, int index, T? oldValue, T? newValue) {
      Kind = kind; Index = index; OldValue = oldValue; NewValue = newValue;
   }

   public string UndoDescription => $"{Kind} {typeof (T).Name}";

   /// <summary>The kind of change this encapsulate</summary>
   public readonly ELChange Kind;
   /// <summary>The index at which the change occurs (also index of current value)</summary>
   public readonly int Index;
   /// <summary>
   /// Removing: the value that we are removing
   /// Replacing: the old value (one being replaced)
   /// </summary>
   public readonly T? OldValue;
   /// <summary>
   /// Added: the new value that was added
   /// Replacing: the new (replacement) value
   /// Current: The new 'Current' value
   /// </summary>
   public readonly T? NewValue;
}
#endregion

#region class Ledger<T> ----------------------------------------------------------------------------
/// <summary>Implements a Ledger of T (a 'managed' collection)</summary>
public class Ledger<T> : IReadOnlyList<T>, IList<T>, IList, IObservable<LChange<T>> {
   public Ledger (Action<LChange<T>>? observer = null) {
      if (observer != null) this.Subscribe (observer);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Count of elements in the table</summary>
   public int Count => _list?.Count ?? 0;

   /// <summary>The currently selected T (Current Layer, Current DimStyle etc)</summary>
   [DebuggerBrowsable (DebuggerBrowsableState.Never)]
   public T Current {
      get {
         // Suppose the collection is empty, and we are asked for the Current value. 
         // If there is a 'Maker' supplied, we use that to make a default object and return it. 
         // For example, Dwg.Styles.Current will create a "STANDARD" text style and return it if
         // none have been created yet. 
         if (Count == 0) {
            if (Maker != null) { Add (Maker ()); return List[mCurrent = 0]; }
            return default!;
         }
         if (mCurrent >= Count) mCurrent = 0;
         return List[mCurrent];
      }
      set {
         // When we try to set a Current value, it should belong to the list. Otherwise, 
         // we throw an exception
         T? oldValue = List.SafeGet (mCurrent);
         int n = List.IndexOf (value);
         if (n == -1) Fatal (EError.NotInList);
         mCurrent = n;
         var c = new (ELChange.Current, n, oldValue, value));
         Notify (ref c);
      }
   }
   int mCurrent;  // Index of current object

   /// <summary>Indexer to get/set elements</summary>
   public T this[int index] { get => Get (index); set => Set (index, value); }

   /// <summary>Returns a T by name (uses the dictionary to search through)</summary>
   public T? this[string name] {
      get {
         if (Namer != null && Dict.TryGetValue (name, out T? value) == true) return value;
         return default;
      }
   }

   // Callbacks ----------------------------------------------------------------
   /// <summary>Validator is called before every operation</summary>
   /// It returns null if the validation is successful, a non-null object describing
   /// the error otherwise
   public Func<LChange<T>, object?> Validator { get => DefValidate; init => mValidator = value; }
   Func<LChange<T>, object?>? mValidator;

   /// <summary>Makes a default object (needed if this has a 'Current' property that is never null)</summary>
   public Func<T>? Maker { get; init; }

   /// <summary>Function to extract the 'name' from a list</summary>
   public Func<T, string>? Namer { get; init; }

   // Methods ------------------------------------------------------------------
   /// <summary>Adds a given item at the end of the list</summary>
   /// Before adding, the Validate is called to check the new item can be indeed added.
   /// The default validator will perform some checks:
   /// - Ensure item is not null
   /// - Ensure item.Name is not blank (if a Namer function is supplied)
   /// - Ensure item.Name is unique within the Ledger (if a Namer function is supplied)
   public void Add (T item) {
      var c = new LChange<T> (ELChange.Add, Count, default, item);
      if (DefValidate (c) is EError err) Fatal (err);
      List.Add (item); _dict?.Add (Namer! (item), item);
      Notify (ref c);
   }

   /// <summary>Removes all elements from the list</summary>
   public void Clear () { for (int i = (_list?.Count ?? 0) - 1; i >= 0; i--) RemoveAt (i); }

   /// <summary>Copies the contents to a new array</summary>
   public void CopyTo (T[] array, int arrayIndex) => List.CopyTo (array, arrayIndex);

   /// <summary>Checks if the list contains the given item</summary>
   public bool Contains (T item) => _list?.Contains (item) ?? false;

   /// <summary>Returns an enumerator over the list</summary>
   /// <returns></returns>
   public IEnumerator<T> GetEnumerator () => (_list ?? []).GetEnumerator ();

   /// <summary>Returns the index of the given item</summary>
   public int IndexOf (T item) => _list?.IndexOf (item) ?? -1;

   /// <summary>Insert an element at a given index</summary>
   public void Insert (int index, T item) {
      var c = new LChange<T> (ELChange.Add, index, default, item);
      if (DefValidate (c) is EError err) Fatal (err);
      List.Insert (index, item);
      Notify (ref c);
   }

   /// <summary>Remove an element at a given index</summary>
   public void RemoveAt (int index) {
      var c = new LChange<T> (ELChange.Remove, index, List[index], default);
      if (DefValidate (c) is EError err) Fatal (err);
      List.RemoveAt (index); _dict?.Clear ();
      Notify (ref c);
   }

   /// <summary>Finds the given item and removes it</summary>
   /// If the given item is not found, raises a Suspicious in developer mode. 
   /// Returns false if the item is not found
   public bool Remove (T item) {
      int n = IndexOf (item);
      if (n == -1) { Lib.Suspicious (); return false; }
      RemoveAt (n);
      return true;
   }

   /// <summary>Remove all items that pass the filter</summary>
   public void RemoveAll (Predicate<T> filter) {
      for (int i = List.Count - 1; i >= 0; i--)
         if (filter (List[i])) RemoveAt (i);
   }

   /// <summary>Subscribes to the change notifications</summary>
   public IDisposable Subscribe (IObserver<LChange<T>> observer) => (mSubject ??= new ()).Subscribe (observer);
   Subject<LChange<T>>? mSubject;

   // Interface implementations ------------------------------------------------
   bool ICollection<T>.IsReadOnly => false;
   bool IList.IsReadOnly => false;
   bool IList.IsFixedSize => false;
   bool ICollection.IsSynchronized => false;
   object ICollection.SyncRoot => List;
   object? IList.this[int index] { get => Get (index); set => Set (index, (T)value!); }
   T IList<T>.this[int index] { get => Get (index); set => Set (index, value); }
   int IList.Add (object? value) { Add ((T)value!); return List.Count - 1; }
   bool IList.Contains (object? value) => Contains ((T)value!);
   int IList.IndexOf (object? value) => IndexOf ((T)value!);
   void IList.Insert (int index, object? value) => Insert (index, (T)value!);
   void IList.Remove (object? value) => Remove ((T)value!);
   void ICollection.CopyTo (Array array, int index) => ((ICollection)(_list ?? [])).CopyTo (array, index);
   IEnumerator IEnumerable.GetEnumerator () => (_list ?? []).GetEnumerator ();

   // Implementation -----------------------------------------------------------
   // The default validator 
   object? DefValidate (LChange<T> c) {
      switch (c.Kind) {
         case ELChange.Add or ELChange.Replace:
            if (c.NewValue == null) return EError.NoNulls;
            if (Namer != null) {
               // If this is a named collection, ensure the name is valid and that there is no
               // duplicate.
               string name = Namer (c.NewValue);
               if (name.IsBlank ()) return EError.BadName;
               // If we're adding a new item, or if we're replacing an item with a differently
               // named item, check there is no name collision
               if (c.Kind == ELChange.Add || name != Namer (c.OldValue!))
                  if (Dict.ContainsKey (name)) return EError.DuplicateName;
            }
            break;
      }
      return mValidator?.Invoke (c);
   }

   // Fatal error
   void Fatal (EError e) => throw new NoriException (e);

   // Called at the end of every action to 
   // 1. Push an action on the undo stack if needed
   // 2. Notify subscribers
   void Notify (ref LChange<T> c) {
      if (UndoStack.Current != null) new ModifyLedger<T> (this, ref c).Push (false);
      mSubject?.OnNext (c);
   }

   // Internal getter used to return an element from the list
   T Get (int index) => List[index];

   // Internal setter used to update an element in the list
   void Set (int index, T item) {
      var c = new LChange<T> (ELChange.Replace, index, List[index], item);
      if (DefValidate (c) is EError err) Fatal (err);
      List[index] = item; _dict?[Namer! (item)] = item;
      Notify (ref c);
   }

   // Private data -------------------------------------------------------------
   [DebuggerBrowsable (DebuggerBrowsableState.Never)]
   Dictionary<string, T> Dict {
      get {
         if ((_dict?.Count ?? 0) == 0 && Namer != null) {
            _dict ??= [];
            if (_list != null) 
               foreach (var item in List) _dict[Namer (item)] = item;
         }
         return _dict!;
      }
   }
   Dictionary<string, T>? _dict;

   [DebuggerBrowsable (DebuggerBrowsableState.Never)]
   List<T> List => _list ??= [];
   List<T>? _list;
}
#endregion

public class ModifyLedger<T> : UndoStep {
   public ModifyLedger (Ledger<T> ledger, ref LChange<T> change) : base (ledger, UndoStack.DescribeNext ?? change.UndoDescription) {
      mLedger = ledger; mChange = change;
   }
   readonly Ledger<T> mLedger;
   readonly LChange<T> mChange;

   public override void Step (EUndoDir dir) {
   }
}
