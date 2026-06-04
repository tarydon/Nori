using System.Reactive;
namespace Nori;

class WPFMouse : IMouse {
   public IObservable<MouseClickInfo> Clicks => throw new NotImplementedException ();

   public IObservable<Unit> Leave => throw new NotImplementedException ();

   public IObservable<Unit> Lost => throw new NotImplementedException ();

   public IObservable<Vec2S> Moves => throw new NotImplementedException ();

   public IObservable<MouseWheelInfo> Wheel => throw new NotImplementedException ();

   public Vec2S Pos => throw new NotImplementedException ();

   public bool TryCapture () => throw new NotImplementedException ();
}
