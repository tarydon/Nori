namespace Nori;

class WPFKeyboard : IKeyboard {
   public IObservable<KeyInfo> Keys => throw new NotImplementedException ();

   public EKeyModifier Modifiers => throw new NotImplementedException ();

   public IObservable<string> Text => throw new NotImplementedException ();
}