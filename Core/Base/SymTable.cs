using System.Diagnostics.CodeAnalysis;
using System.Text;
namespace Nori;

#region class SymTable -----------------------------------------------------------------------------
/// <summary>Create a symbol-table that uses ReadOnlySpan(byte) as keys</summary>
/// You can add entries into the symbol table using ReadOnlySpan(byte),
/// byte[] or strings as keys. You search for values using ReadOnlySpan(byte)
/// as search keys
public class SymTable<T> {
   // Proprties ----------------------------------------------------------------
   /// <summary>Count of key-value pairs in the SymTable</summary>
   public int Count => mDict0.Count + mDict1.Count;

   // Methods ------------------------------------------------------------------
   /// <summary>Add a key-value pair into the SymTable (key as a ReadOnlySpan(byte))</summary>
   public void Add (ReadOnlySpan<byte> key, T value)
      => Add (key.ToArray (), value);

   /// <summary>Add a key-value pair into the SymTable (key as a byte[])</summary>
   /// Note that we are biased towards optimizing for fast searches, rather than 
   /// fast Adds (though the adds are not very slow, either)
   public void Add (byte[] key, T value) {
      int hash = GetHashCode (key);
      // Create an entry to store as the 'value' in the underlying dictionary
      // mDict0. This contains the original key as a byte-array (needed for equality
      // comparison later), and the T value
      var entry = new Entry (key, value);
      // This handles the rarer case that there has already been another item with the
      // same hash value added (hash collision). In that case, we have stopped using
      // mDict0 and started using mDict1 (which stores a LIST for each hash).
      if (mDict1.TryGetValue (hash, out var list1)) {
         // First, check if the actual key itself has already been added in
         // (that is, this is not a hash collision, but just a duplicate key addition).
         // If so, throw an exception
         if (list1.Any (a => a.Key.SequenceEqual (key))) Fatal ();
         // Otherwise, add this new entry into the list and return
         list1.Add (entry); return;
      }
      // Next, see if we have already added a single item with this hash key 
      // into mDict0 (this is the first hash collision happening for this hash)
      // If not, we are done (the item is stored in mDict0)
      if (mDict0.TryAdd (hash, entry)) return;
      // If there is a hash collision, this is exactly the second item with this 
      // particular hash value, so promote this item from mDict0 (single items)
      // to mDict1 (list of items). Note that it is important to remove this from
      // mDict0!
      List<Entry> list2 = [mDict0[hash], entry];
      // Quick check to assure us this exact key has not already been added before
      // as the single entry in mDict0
      if (key.SequenceEqual (list2[0].Key)) Fatal ();
      mDict1.Add (hash, list2);
      mDict0.Remove (hash);

      void Fatal () {
         string skey = Encoding.UTF8.GetString (key);
         throw new ArgumentException ($"An item with the same key ({skey}) has already been added");
      }
   }

   /// <summary>Add a key-value pair into the SymTable (key as a string)</summary>
   public void Add (string key, T value)
      => Add (Encoding.UTF8.GetBytes (key), value);

   /// <summary>Get the value stored with a particular key (or throws a KeyNotFound exception)</summary>
   public T this[ReadOnlySpan<byte> key] {
      get {
         if (!TryGetValue (key, out var v))
            throw new KeyNotFoundException ($"The given key {Encoding.UTF8.GetString (key)} was not found");
         return v;
      }
   }

   /// <summary>Gets the value stored with a particular key (key passed as string)</summary>
   /// Note: this is not as efficient as passing the key as a ReadOnlySpan(byte)
   public T this[string key] 
      => this[Encoding.UTF8.GetBytes (key)];

   /// <summary>Tries to get a value stored with a particular key (or returns false if the key is not found)</summary>
   public bool TryGetValue (ReadOnlySpan<byte> key, [NotNullWhen (true)] out T? value) {
      int hash = GetHashCode (key);
      if (mDict0.TryGetValue (hash, out var item)) {
         if (key.SequenceEqual (item.Key)) { value = item.Value!; return true; }
      } else if (mDict1.TryGetValue (hash, out var list)) {
         foreach (var elem in list)
            if (key.SequenceEqual (elem.Key)) { value = elem.Value!; return true; }
      }
      value = default; return false;
   }

   /// <summary>Tries to get the value stored with a particular key (or default(T) if the key is not found)</summary>
   public T? GetValueOrDefault (ReadOnlySpan<byte> key) {
      TryGetValue (key, out T? value);
      return value;
   }

   // Impementation ------------------------------------------------------------
   // Compute the hash code of a ReadOnlySpan<byte>
   int GetHashCode (ReadOnlySpan<byte> key) {
      int hash = 17;
      foreach (byte b in key) hash = Combine (hash, b);
      return hash;

      // Combines two hash values by mixing them using bitwise operations
      // Ref: https://github.com/dotnet/roslyn/blob/main/src/Compilers/Test/Resources/Core/NetFX/ValueTuple/ValueTuple.cs
      static int Combine (int h1, int h2) {
         uint rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
         return ((int)rol5 + h1) ^ h2;
      }
   }

   readonly struct Entry (byte[] key, T value) {
      public readonly byte[] Key = key;
      public readonly T Value = value;
      public override string ToString () => $"Key:{Encoding.UTF8.GetString (Key)}, Value:{Value}";
   }

   // This dictionary stores hashcode-value pairs as long as a hash collision does not
   // occur for a given hash value
   Dictionary<int, Entry> mDict0 = [];
   // Once a hash collision occurs, the hashcode and its now multiplicity of values are
   // moved to this dictionary, that maintains a list for each hash code (open-hashing). 
   // This two-tier structure is a bit wasteful of memory, perhaps, but is optimized for
   // the common case happy-path that hash collisions are rare. 
   Dictionary<int, List<Entry>> mDict1 = [];
}
#endregion
