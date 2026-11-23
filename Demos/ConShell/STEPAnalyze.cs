using Nori.STEP;
using static Nori.STEP.EKind;
namespace Nori;

public partial class STEPReader {
   void AnalyzeAssembly () {
      Console.WriteLine ();
      foreach (var sd in ShapeDefn) {
         switch (sd.Kind) {
            case SHAPE_DEFINITION_REPRESENTATION:
               LoadPartShape (sd.Target, sd.Shape);
               break;
            case CONTEXT_DEPENDENT_SHAPE_REPRESENTATION:
               LoadInsert (sd.Target, sd.Shape);
               break;
         }
      }
      foreach (var p in Links) {
         Node parent = Nodes[p.Parent], child = Nodes[p.Child];
         parent.Children.Add (p); child.Root = false;
      }
   }

   public bool IsSimplePart () {
      return Nodes.Count == 1 && Links.Count == 0;
   }

   void BuildAssembly () {
      Lib.Trace (mFile);      
      foreach (var node in Nodes.Values.Where (a => a.Root))
         Dump (node, 0);

      void Dump (Node node, int level) {
         Lib.Trace (new string (' ', level * 2) + node);
         foreach (var link in node.Children) {
            Lib.Trace (new string (' ', level * 2 + 1) + $"{link.ParentXfm} x {link.ChildXfm}");
            var child = Nodes[link.Child];
            Dump (child, level + 1);
         }
      }
   }

   void LoadInsert (int idXfm, int idLinks) {
      // First, get the child and parent transforms used in defining this insert
      var span = GetCompoundBytes (idXfm);
      Check (Seek (span, "REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION"u8), 26); 
      mR.Match ('('); int itemXfm = RRef ();
      TwoRef xfmRefs = TwoRef[GetIndex (itemXfm, ITEM_DEFINED_TRANSFORMATION)];

      Check (Seek (span, "REPRESENTATION_RELATIONSHIP"u8), 26);
      mR.Match ('('); RString (); RString ();
      int idChild = RRef (), idParent = RRef ();
      Links.Add (new Link (idParent, idChild, xfmRefs.Item2, xfmRefs.Item1));
   }

   void LoadPartShape (int idPart, int idShape) {
      if (Data[idPart].Kind == PROPERTY_DEFINITION) { idPart = Int[Data[idPart].Index]; return; }
      if (Data[idPart].Kind == SHAPE_ASPECT) idPart = Int[Data[idPart].Index];
      int idProdDef = Int[GetIndex (idPart, PRODUCT_DEFINITION_SHAPE)];
      int idProdDefFormation = Int[GetIndex (idProdDef, PRODUCT_DEFINITION, PRODUCT_DEFINITION_WITH_ASSOCIATED_DOCUMENTS)];
      int idProduct = Int[GetIndex (idProdDefFormation, PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE, PRODUCT_DEFINITION_FORMATION)];
      ref readonly Product prod = ref Product.AsSpan ()[GetIndex (idProduct, PRODUCT)];

      var tup = Data[idShape];
      ref readonly ShapeRepr shape = ref ShapeRepr.AsSpan ()[Data[idShape].Index];
      switch (tup.Kind) {
         case ADVANCED_BREP_SHAPE_REPRESENTATION or MANIFOLD_SURFACE_SHAPE_REPRESENTATION
         or GEOMETRICALLY_BOUNDED_WIREFRAME_SHAPE_REPRESENTATION or GEOMETRICALLY_BOUNDED_SURFACE_SHAPE_REPRESENTATION:
            int xfmZero = -1;
            foreach (var n in shape.Items) if (Data[n].Kind == AXIS2_PLACEMENT_3D) xfmZero = n;
            var ps = new Node (prod.Name, idProdDef, idShape, xfmZero, []); Console.WriteLine ($"  {ps}");
            Nodes.Add (idShape, ps);
            break;
         case SHAPE_REPRESENTATION:
            if (shape.Items.Length == 1 && Data[shape.Items[0]].Kind == MANIFOLD_SOLID_BREP) {
               var ps2 = new Node (prod.Name, idProdDef, idShape, -1, []); Console.WriteLine ($"  {ps2}");
               Nodes.Add (idShape, ps2);
            } else {
               var ash = new Node (prod.Name, idProdDef, idShape, -1, shape.Items); Console.WriteLine ($"  {ash}");
               Nodes.Add (idShape, ash);
            }
            break;
         default:
            throw new BadCaseException (tup.Kind);
      }
   }

   class Node (string name, int defn, int shapeRep, int xfmZero, int[] items) {
      public readonly string Name = name;
      public readonly int ShapeRep = shapeRep;
      public readonly int Definition = defn;
      public readonly int XfmZero = xfmZero;
      public readonly int[] Items = items;
      public readonly List<Link> Children = [];
      public bool Root = true;

      public override string ToString () {
         if (Items.Length > 0) {
            return $"ASSEMBLY '{Name}' ShapeRep={ShapeRep} Defn={Definition} Items={Items.ToCSV ()}";
         } else {
            return $"PART '{Name}' ShapeRep={ShapeRep} Defn={Definition} XfmZero={XfmZero}";
         }
      }
   }
   Dictionary<int, Node> Nodes = [];  // Indexes PartShapes by definition

   readonly struct Link (int idParent, int idChild, int idParentXfm, int idChildXfm) {
      public readonly int Parent = idParent, Child = idChild;
      public readonly int ParentXfm = idParentXfm, ChildXfm = idChildXfm;
      public override string ToString () => $"PLACEMENT, {Parent}->{Child}, {ParentXfm}->{ChildXfm}";
   }
   List<Link> Links = [];
}
