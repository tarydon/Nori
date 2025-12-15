using Nori;
namespace WPFDemo;

class T3XDemoScene : Scene3 {
   public T3XDemoScene () {
      var model = new T3XReader ("N:/Demos/Data/5x-043-blank.t3x").Load ();
      var model2 = new T3XReader ("N:/Demos/Data/5x-043.t3x").Load ();
      foreach (var ent in model.Ents) ent.IsTranslucent = true; 
      BgrdColor = new Color4 (80, 84, 88);
      Bound = model.Bound;
      Root = new GroupVN ([new Model3VN (model), new Model3VN (model2)]);
   }
}
