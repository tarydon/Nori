using Nori;
namespace WPFDemo;

class T3XDemoScene : Scene3 {
   public T3XDemoScene () {
      var blank = new T3XReader ("N:/Demos/Data/5x-043-blank.t3x").Load ();
      var part = new T3XReader ("N:/Demos/Data/5x-043.t3x").Load ();
      foreach (var ent in blank.Ents) ent.IsTranslucent = true; 

      BgrdColor = new Color4 (80, 84, 88);
      Bound = part.Bound;
      Root = new GroupVN ([new Model3VN (blank), new Model3VN (part)]);
   }
}
