namespace Nori;

public class ModelThickener (Model3 model) {
   public Model3 Process () {
      var planes = mModel.Ents.OfType<E3Plane> ().OrderByDescending (a => a.Area).ToList ();
      foreach (var p in planes)
         Console.Write ($"{p.Area.Round (0)} ");
      Console.WriteLine ($"{planes.Count}");
      return mTModel;
   }

   Model3 mModel = model, mTModel = new ();
}