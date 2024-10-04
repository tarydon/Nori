// ────── ╔╗ Nori™
// ╔═╦╦═╦╦╬╣ Copyright © 2025 Arvind
// ║║║║╬║╔╣║ RBTree.cs ~ Implements a balanced binary tree that maintains elements in sorted order
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections;
namespace Nori;

#region class RBTree -------------------------------------------------------------------------------
/// <summary>This implements a balanced binary tree (implemented as a left-leaning red-black tree)</summary>
/// The tree stores values of a particular type TVal, and these values are ordered using keys
/// of a particular type TKey. When the tree is constructed, a _keyGetter_ function is provided, 
/// that can extract (or synthesize) the key for any given value. The key type TKey should be
/// comparable, and a given value's key should not change over time (like a HashCode). 
/// 
/// - When a value is added with a key that already exists in the tree, the existing value
///   with that key is overwritten (thus, this behaves like a set)
/// - Like a hashtable, the tree supports finding a value by a given key
/// - Additionally, it also provides an enumerator that can return the values sorted by the key
///   (in a non-destructive manner). In short, the Tree implements IEnumerable of TVal
/// - These functions build on the 'sorted' nature of the collection:
///     * Min() returns the value with the smallest key stored in the Tree
///     * Max() returns the value with the largest key stored in the Tree
///     * GetFloor(key) returns the largest value whose key is less than or equal to the given key
///     * GetCeiling(key) returns the smallest value whose key is greater than or equal to the given key
/// - Methods like Get, GetFloor, GetCeiling etc all return a readonly reference to the value stored
///   in the tree. The point is to avoid copying the values as far as possible, so the Tree structure
///   is very performant even when you use a largish struct as the TVal type (for example, it is 
///   efficient to store an Edge, or an Event or a Segment directly as values in the Tree). In fact,
///   the primary reason to build this Tree data structure was because the .Net SortedSet (which uses
///   a similar implementation) lacks the Floor and Ceiling functions, which are necessary for many
///   computational geometry algorithms. 
/// - In the simplest case where the TVal type is itself ordered, and can serve as its own key,
///   you can construct the tree with code like this (the key-getter is an identity function):
///      var tree = new RBTree<double, double> (a => a);
///   
/// Implementation notes: the Tree is implemented so it does not allocate one _node_ on the heap
/// for each element stored in it - all data is stored in a contiguous block that is grown as needed,
/// so the Tree is very memory efficient (compared with a .Net SortedSet, for example). 
/// 
/// All operations on the Tree take log(N) time, even in the worst case with degenerate or already
/// sorted inputs. The tree is kept balanced with the constraint that the maximum path length from the
/// root to any of the leaves is not more than 2 * log(N).
public partial class RBTree<TVal, TKey> : IEnumerable<TVal> where TKey : IComparable<TKey> {
   // Constructor --------------------------------------------------------------
   /// <summary>Construct a RBTree, given the key-getter function that provides a key for each value</summary>
   public RBTree (Func<TVal, TKey> keyGetter) {
      mKeyer = keyGetter;
      EnsureCapacity (7);
   }
   readonly Func<TVal, TKey> mKeyer;

   // Properties ---------------------------------------------------------------
   /// <summary>Returns the number of key-value pairs in the tree</summary>
   public int Count => mA.Length - mFree.Count - 1;

   // Methods ------------------------------------------------------------------
   /// <summary>Adds an value to the RBTree</summary>
   /// The keyGetter function is used to extract a key from this value, and that key is used
   /// to index this value into the tree. If another value exists with the same key, that will be
   /// overwritten
   public void Add (TVal value) {
      if (mFree.Count == 0) EnsureCapacity (mA.Length * 2 - 1);
      mRoot = AddImp (mRoot, mKeyer (value), value);
      mA[mRoot].Color = EColor.Black;
   }

   /// <summary>Returns true if the tree contains a value with the given key</summary>
   public bool Contains (TKey key)
      => Find (key) != 0;

   /// <summary>Reserves space for future Add methods (this is only a performance tweak, and rarely needed)</summary>
   /// Any requests to reduce the capacity below the current count will be quietly ignored.
   public void EnsureCapacity (int n) {
      n++;
      if (mA.Length < n) {
         mFree.EnsureCapacity (n);
         for (int min = mA.Length, i = n - 1; i >= min; i--) mFree.Push (i);
         Array.Resize (ref mA, n);
      }
   }

   /// <summary>Gets a reference to the element with the given key</summary>
   /// If the element exists, this returns a readonly reference to the element within the 
   /// Tree structure (no copying). If the element does not exist, this returns a null reference,
   /// which can be checked with code like:
   ///    ref readonly Segment seg = tree.Get (12);    // Assuming Segment is indexed with an integer key
   ///    if (Sys.IsNull (in seg)) Console.WriteLine ("NULL");
   ///    else Console.WriteLine ($"{seg.A} -> {seg.B}");
   /// Note that the Sys.IsNull is just a wrapper around the System.Runtime.CompilerServices.Unsafe.IsNullRef
   /// method. 
   public ref readonly TVal Get (TKey key)
      => ref Return (Find (key));

   /// <summary>Returns a reference to the smallest value whose key is greater than or equal to the given key</summary>
   /// If there is no such value, this returns a null reference (use Sys.IsNull(ref) to check that)
   public ref readonly TVal GetCeiling (TKey key)
      => ref Return (FindCeiling (mRoot, key));

   /// <summary>Returns a reference to the largest value whose key is less than or equal to the given key</summary>
   /// If there is no such value, this returns a null reference (use Sys.IsNull(ref) to check that)
   public ref readonly TVal GetFloor (TKey key)
      => ref Return (FindFloor (mRoot, key));

   /// <summary>Returns the smallest key stored in the tree</summary>
   /// If the tree is empty, this returns a null reference (use Sys.IsNull(ref) to check that)
   public ref readonly TVal Min ()
      => ref Return (FindMin (mRoot));

   /// <summary>Returns the largest key stored in the tree</summary>
   /// If the tree is empty, this returns a null reference (use Sys.IsNull(ref) to check that)
   public ref readonly TVal Max ()
      => ref Return (FindMax (mRoot));

   /// <summary>Removes the value with a given key from the tree</summary>
   /// If the given key does not exist in the tree, this throws an exception 
   public void Remove (TKey key) {
      // If both children of root are black, set root to red
      ref Node node = ref mA[mRoot];
      if (!IsRed (node.Left) && !IsRed (node.Right))
         node.Color = EColor.Red;
      mRoot = Remove (mRoot, key);
      if (mRoot > 0) mA[mRoot].Color = EColor.Black;
   }

   // Nested types -------------------------------------------------------------
   // Marker for whether each node is Red or Black 
   enum EColor : byte { Black, Red };

   // The actual Node structure. We maintain nodes in the mA array - there are no pointers
   // used and the links between nodes are maintained using the Left / Right indices into this
   // array. When a node is freed, its index is added to the mFree list, and it is eventually
   // reused. The mA array never shrinks, even when elements are removed. Note that mA[0] is
   // not a valid node, and the index value 0 is effectively the same as NULL. 
   struct Node {
      public int Left, Right;    // Links to left and right subtrees (or 0)
      public TVal Value;         // Value stored at this node
      public EColor Color;       // Color of parent link (link leading into this node)

      public override readonly string ToString () {
         string s = $"{Color}Node {Value}";
         if (Left != 0) s += $" L:{Left}";
         if (Right != 0) s += $" R:{Right}";
         return s;
      }

      public Node (TVal value, EColor color)
         => (Value, Color) = (value, color);
   }
   Stack<int> mFree = new ();    // Indices of free slots in the mA array (for reuse)
   Node[] mA = new Node[1];      // The array containing all the nodes (mA[0] is not used, and is a sentinel)
   int mRoot;                    // Root node of the tree (or 0 when the tree is empty)

   // Implementation -----------------------------------------------------------
   // Recursive helper used by Add. This adds a given value (with a given key)
   // into the subtree rooted at h. If it needs to allocate a new node for this
   // (which it does if the given key does not already exist in the subtree), this
   // calls Alloc to allocate a new node (or reuse one from the free list). 
   int AddImp (int h, TKey key, TVal value) {
      if (h == 0) {
         // mFree is guaranteed not to be empty since we have already checked for
         // this condition (and reserved more space if so) in Add
         h = mFree.Pop ();
         mA[h] = new Node (value, EColor.Red);
         return h;
      }

      ref Node node = ref mA[h];
      int comp = key.CompareTo (mKeyer (node.Value));
      if (comp < 0) node.Left = AddImp (node.Left, key, value);
      else if (comp > 0) node.Right = AddImp (node.Right, key, value);
      else node.Value = value;

      // Fix up any right-leaning links
      if (IsRed (node.Right) && !IsRed (node.Left))
         node = ref mA[h = RotateLeft (h, true)];
      if (IsRed (node.Left) && IsRed (mA[node.Left].Left))
         node = ref mA[h = RotateRight (h, true)];
      if (IsRed (node.Left) && IsRed (node.Right))
         FlipColors (ref node);
      return h;
   }

   // Internal test routine
   partial void Assert ([DoesNotReturnIf (false)] bool condition);
   // partial void Assert (bool condition) {
   //    if (!condition) throw new Exception ("Assertion failed");
   // }

   // Restore red-black tree invariant
   int Balance (int h) {
      Assert (h != 0);
      ref Node node = ref mA[h];
      if (IsRed (node.Right) && !IsRed (node.Left)) node = ref mA[h = RotateLeft (h)];
      if (IsRed (node.Left) && IsRed (mA[node.Left].Left)) node = ref mA[h = RotateRight (h)];
      if (IsRed (node.Left) && IsRed (node.Right)) FlipColors (ref node);
      return h;
   }

   // Delete the key-value pair with the minimum key rooted at h
   int DeleteMin (int h) {
      ref Node node = ref mA[h];
      if (node.Left == 0) { mFree.Push (h); return 0; }
      if (!IsRed (node.Left) && !IsRed (mA[node.Left].Left)) node = ref mA[h = MoveRedLeft (h)];
      node.Left = DeleteMin (node.Left);
      return Balance (h);
   }

   // Finds the index of the node with the given key (or 0 if not found)
   int Find (TKey key) {
      for (int x = mRoot; x != 0;) {
         ref readonly Node node = ref mA[x];
         switch (key.CompareTo (mKeyer (node.Value))) {
            case < 0: x = node.Left; break;
            case > 0: x = node.Right; break;
            default: return x;
         }
      }
      return 0;
   }

   // Finds the index of the smallest key that is more than or equal to given key,
   // in the given subtree (returns 0 if none such found)
   int FindCeiling (int x, TKey key) {
      if (x == 0) return 0;
      ref readonly Node node = ref mA[x];
      switch (key.CompareTo (mKeyer (node.Value))) {
         case 0: return x;
         case > 0: return FindCeiling (node.Right, key);
         default: int t = FindCeiling (node.Left, key); return t > 0 ? t : x;
      }
   }

   // Finds the index of the largest key that is less than or equal to given key,
   // in the given subtree (returns 0 if none such found)
   int FindFloor (int x, TKey key) {
      if (x == 0) return 0;
      ref readonly Node node = ref mA[x];
      switch (key.CompareTo (mKeyer (node.Value))) {
         case 0: return x;
         case < 0: return FindFloor (node.Left, key);
         default: int t = FindFloor (node.Right, key); return t > 0 ? t : x;
      }
   }

   // Index of the smallest node in the subtree rooted at h, or 0 if there is no
   // such smallest node
   int FindMin (int h) {
      for (; ; ) {
         int left = mA[h].Left;
         if (left == 0) return h; else h = left;
      }
   }

   // Index of the largest node in the subtree rooted at h, or 0 if there is no
   // such largest node (subtree is empty)
   int FindMax (int h) {
      for (; ; ) {
         int right = mA[h].Right;
         if (right == 0) return h; else h = right;
      }
   }

   // Helper used to flip the colors of a node and its two children
   void FlipColors (ref Node node) {
      node.Color = 1 - node.Color;
      if (node.Left > 0) { ref Node n = ref mA[node.Left]; n.Color = 1 - n.Color; }
      if (node.Right > 0) { ref Node n = ref mA[node.Right]; n.Color = 1 - n.Color; }
   }

   // Is a node red? (return false for the NULL node 0)
   bool IsRed (int n) => mA[n].Color == EColor.Red;

   // helper rountine: assuming that h is red and both h.left and h.left.left
   // are black, make h.left or one of its children red.
   int MoveRedLeft (int h) {
      Assert (h != 0);
      ref Node node = ref mA[h];
      Assert (IsRed (h) && !IsRed (node.Left) && !IsRed (mA[node.Left].Left));
      FlipColors (ref node);
      if (IsRed (mA[node.Right].Left)) {
         node.Right = RotateRight (node.Right);
         node = ref mA[h = RotateLeft (h)];
         FlipColors (ref node);
      }
      return h;
   }

   // Helper routine: assuming that h is red and both h.right and h.right.left
   // are black, make h.right or one of its children red.
   int MoveRedRight (int h) {
      Assert (h != 0);
      ref Node node = ref mA[h];
      Assert (IsRed (h) && !IsRed (node.Right) && !IsRed (mA[node.Right].Left));
      FlipColors (ref node);
      if (IsRed (mA[node.Left].Left)) {
         node = ref mA[h = RotateRight (h)];
         FlipColors (ref node);
      }
      return h;
   }

   // Removes a key from the given subtree (throws exception if the key is not found)
   int Remove (int h, TKey key) {
      if (h == 0) throw new Exception ("Remove called on an empty tree");
      ref Node node = ref mA[h];
      if (key.CompareTo (mKeyer (node.Value)) < 0) {
         if (!IsRed (node.Left) && !IsRed (mA[node.Left].Left)) node = ref mA[h = MoveRedLeft (h)];
         node.Left = Remove (node.Left, key);
      } else {
         if (IsRed (node.Left)) node = ref mA[h = RotateRight (h)];
         if (key.CompareTo (mKeyer (node.Value)) == 0 && node.Right == 0) { mFree.Push (h); return 0; }
         if (!IsRed (node.Right) && !IsRed (mA[node.Right].Left)) node = ref mA[h = MoveRedRight (h)];
         if (key.CompareTo (mKeyer (node.Value)) == 0) {
            ref Node x = ref mA[FindMin (node.Right)];
            node.Value = x.Value; node.Right = DeleteMin (node.Right);
         } else
            node.Right = Remove (node.Right, key);
      }
      return Balance (h);
   }

   // Helper method used to return a reference to a value from the tree, or a
   // special null-reference in case the value does not exist
   [MethodImpl (MethodImplOptions.AggressiveInlining)]
   ref readonly TVal Return (int index) {
      if (index == 0) return ref Unsafe.NullRef<TVal> ();
      return ref mA[index].Value;
   }

   // Helper routine: make a right-leaning link lean to the left
   int RotateLeft (int h, bool insert = false) {
      ref Node node = ref mA[h];
      Assert (node.Right > 0 && IsRed (node.Right));
      if (insert) Assert (!IsRed (node.Left));
      int pright = node.Right;
      ref Node x = ref mA[pright];
      node.Right = x.Left; x.Left = h;
      x.Color = node.Color; node.Color = EColor.Red;
      return pright;
   }

   // Helper routine: make a left-leaning link lean to the right
   int RotateRight (int h, bool insert = false) {
      ref Node node = ref mA[h];
      Assert (node.Left > 0 && IsRed (node.Left));
      if (insert) Assert (!IsRed (node.Right));
      int pleft = node.Left;
      ref Node x = ref mA[pleft];
      node.Left = x.Right; x.Right = h;
      x.Color = node.Color; node.Color = EColor.Red;
      return pleft;
   }

   // Performs an in-order traversal of the tree, and returns the indices
   IEnumerable<int> Traverse (int h) {
      if (h != 0) {
         foreach (var v in Traverse (mA[h].Left)) yield return v;
         yield return h;
         foreach (var v in Traverse (mA[h].Right)) yield return v;
      }
   }

   // Implements IEnumerable<T>
   public IEnumerator<TVal> GetEnumerator () { foreach (var n in Traverse (mRoot)) yield return mA[n].Value; }
   IEnumerator IEnumerable.GetEnumerator () { foreach (var n in Traverse (mRoot)) yield return mA[n].Value; }
}
#endregion
