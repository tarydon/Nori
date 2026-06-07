// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Classes.cs
// ║║║║╬║╔╣║ Various utility classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class DIBitmap -----------------------------------------------------------------------------
/// <summary>This represents a simple 'device-independent' bitmap in one of various formats</summary>
/// This is just a thin wrapper around an array of bytes holding pixel data. It
/// adds some key metadata like the width and height of the bitmap, and the 'format' by
/// which to interpret the bit values
public class DIBitmap {
   /// <summary>Construct a bitmap given the width, height, format and actual raw data</summary>
   /// Note that this format has no padding at the end of each line to align it
   /// on any 4-byte or 8-byte boundary. The data for successive lines is tightly
   /// packed.
   public DIBitmap (int width, int height, EFormat fmt, byte[] data) {
      (Width, Height, Fmt, Data) = (width, height, fmt, data);
      Stride = Width * fmt.BytesPerPixel ();
   }
   public override string ToString () => $"DIBitmap: {Width}x{Height}, {Fmt}";

   /// <summary>Get the number of bytes per pixel</summary>
   public int BytesPerPixel => Fmt switch {
      EFormat.Gray8 => 1, EFormat.RGB8 => 3, EFormat.RGBA8 => 4,
      _ => throw new BadCaseException (Fmt)
   };

   /// <summary>Width of the bitmap in pixels</summary>
   public readonly int Width;
   /// <summary>Height of the bitmap in pixels</summary>
   public readonly int Height;
   /// <summary>Format of the bitmap</summary>
   public readonly EFormat Fmt;
   /// <summary>Stride between succsesive lines in the Data array</summary>
   public readonly int Stride;
   /// <summary>Raw data of the bitmap</summary>
   public readonly byte[] Data;

   /// <summary>Checks if this bitmap is identical to another, within a given threshold</summary>
   public bool Identical (DIBitmap other, byte threshold = 0) {
      if (Width != other.Width || Height != other.Height || Fmt != other.Fmt || Data.Length != other.Data.Length)
         return false;
      for (int i = 0; i < Data.Length; i++) {
         byte a = Data[i], b = other.Data[i];
         if (Math.Abs (a - b) <= threshold) continue;
         return false;
      }
      return true;
   }

   /// <summary>Format of the bitmap</summary>
   public enum EFormat {
      Unknown,
      /// <summary>8-bit Red, Green, Blue components (24 bits per pixel)</summary>
      RGB8,
      /// <summary>8-bit Red, Green, Blue, Alpha components (32 bits per pixel)</summary>
      RGBA8,
      /// <summary>8-bit Grayscale values (8-bits per pixel)</summary>
      Gray8
   }
}
#endregion

#region class MultiDispose -------------------------------------------------------------------------
/// <summary>Helper to hold on to, and dispose, multiple IDisposables</summary>
public class MultiDispose : IDisposable {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a MultiDispose with zero or more disposables to hold on to</summary>
   public MultiDispose (params IDisposable?[] disps) => mDisposables.AddRange (disps);
   readonly List<IDisposable?> mDisposables = [];

   // Methods ------------------------------------------------------------------
   /// <summary>Add an additional disposable</summary>
   public void Add (IDisposable? disp) => mDisposables.Add (disp);

   // Implement IDisposable ----------------------------------------------------
   public void Dispose () { mDisposables.ForEach (a => a?.Dispose ()); mDisposables.Clear (); }
}
#endregion

#region class EventWrapper<T> ----------------------------------------------------------------------
/// <summary>Helper class to wrap events / callbacks into IObservables</summary>
/// Derive a class from this, implement Connect() to sign up/disconnect from the event
/// or callback. Then, use Push() to push events. This class manages any number of observers,
/// calls Connect lazily (only when first subscriber signs up) and manages the disposal of
/// observers etc. 
public abstract class EventWrapper<T> : IObservable<T> {
   // Methods ------------------------------------------------------------------
   /// <summary>Implements the IObservable contract</summary>
   /// When the first subscriber connects, this calls Connect(true) on its derived
   /// class, which in turn will actually connect an event handler to the underlying
   /// event. This returns an instance of the Disposer (see below) that when disposed
   /// disconnects the observer from our list of observers.
   public IDisposable Subscribe (IObserver<T> observer) {
      mObservers.Add (observer);
      if (mObservers.Count == 1) Connect (true);
      return new Disposer (this, observer);
   }
   List<IObserver<T>> mObservers = [];

   // Implementation -----------------------------------------------------------
   // Must be implemented by derived class to actually connect / disconnect from the event
   abstract protected void Connect (bool connect);

   // Used internally by derived clases to push an item (KeyInfo / MouseInfo etc)
   // to all observers. Note that even when we have multiple observers connected, there is
   // only event handler that is signed up (since we call Connect only when the first observer
   // signs up). This push method will then distribute the event to all observers that have
   // signed up.
   // NOTE: This is done in a last-come, first-served method. The most recent observer to
   // sign up will get the first look at the event.
   protected void Push (T item) {
      for (int i = mObservers.Count - 1; i >= 0; i--)
         mObservers[i].OnNext (item);
   }

   // Called by the Disposer type (see below) to remove this particular observer from
   // the list of observers this class maintains. Once the last observer is gone, it
   // calls Connect(false) to disconnect the event handler
   void Remove (IObserver<T> observer) {
      if (mObservers.Remove (observer) && mObservers.Count == 0)
         Connect (false);
   }

   // Nested types -------------------------------------------------------------
   // An implementation of IDisposable that removes this observer from its owner
   class Disposer (EventWrapper<T> owner, IObserver<T> observer) : IDisposable {
      public void Dispose () => owner.Remove (observer);
   }
}
#endregion
