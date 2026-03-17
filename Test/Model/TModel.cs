// ────── ╔╗
// ╔═╦╦═╦╦╬╣ TModel.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Testing;

[Fixture (40, "Basic model tests", "Model")]
class TModel {
   [Test (224, "Basic test of SurfaceMesher")]
   void Test1 () {
      var reader = new T3XReader (NT.File ("IO/T3X/NURB.t3x")) { NoMeshes = true };
      var model = reader.Load ();
      var nurb = model.Ents.OfType<E3NurbsSurface> ().Single ();
      var mesh = nurb.Mesh;
      mesh.Vertex.Length.Is (926);
      mesh.Wire.Length.Is (364);
      mesh.Triangle.Length.Is (10692);
   }

   [Test (225, "Testing surface connectivity")]
   void Test2 () {
      var model = new T3XReader (NT.File ("IO/T3X/5X-022.t3x")).Load ();
      Test (82, "71,72,79,81,91,182"); Test (78, "77"); Test (77, "75,76,78");
      Test (182, "15,22,23,26,43,45,52,56,82,91,183");
      Test (181, "1,6,22,199");

      var ent = (E3Surface)model.Ents.Single (a => a.Id == 78);
      var c1 = ent.Contours[0].Curves[0];
      model.GetCoedge (c1, out E3Surface? ent2, out Curve3? curve2).IsTrue ();
      if (ent2 == null || curve2 == null) throw new InvalidOperationException ();
      ent2.Id.Is (77); (curve2 is Arc3).IsTrue ();

      var bits = model.PartitionByConnectivity ();
      bits.Count.Is (1);
      var connected = model.GetConnected (ent);
      connected.Count.Is (201);

      void Test (int id, string next) {
         var ent = (E3Surface)model.Ents.Single (a => a.Id == id);
         var neighbors = model.GetNeighbors (ent).Select (a => a.Id).Order ().ToCSV ();
         neighbors.Is (next);
      }
   }

   [Test (226, "Testing Model.Ents firing")]
   void Test3 () {
      string observe = "";
      Model3 model = new T3XReader (NT.File ("IO/T3X/5X-022.t3x")).Load (), model2 = new ();
      model2.Ents.Subscribe (Changed);

      model2.Ents.Add (model.Ents.Single (a => a.Id == 78));
      var b1 = model2.Bound; b1.DiagVector.Is ("<47.996029,47.856552,3.365728>");
      model2.Ents.Add (model.Ents.Single (a => a.Id == 77));
      var b2 = model2.Bound; b2.DiagVector.Is ("<58.880905,58.734215,4.129915>");
      model2.Ents.RemoveAt (1);
      var b3 = model2.Bound; b3.DiagVector.Is ("<47.996029,47.856552,3.365728>");
      model2.Ents.Clear (); 
      observe.Is ("Added.0 Added.1 Removing.1 Clearing.0 ");

      void Changed (ListChange a) => observe += $"{a.Action}.{a.Index} ";
   }

   [Test (227, "Transform constructor")]
   void Test4 () {
      var model = new T3XReader (NT.File ("IO/T3X/PLANE.t3x")).Load ();
      model.Bound.DiagVector.Is ("<50.202042,38.157703,0.563709>");
      var model2 = model * Matrix3.Rotation (EAxis.X, Lib.HalfPI);
      model2.Bound.DiagVector.Is ("<50.202042,0.563709,38.157703>");
   }
}
