// ────── ╔╗
// ╔═╦╦═╦╦╬╣ BendPose.cs
// ║║║║╬║╔╣║ Used to 'pose' a sheet-metal model by bending/unbending each flex independently
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Subjects;
namespace Nori;

#region class BendPose -----------------------------------------------------------------------------
/// <summary>BendPose captures the bending state (bent/unbent) of each bend in a sheet-metal model</summary>
/// A sheet metal model consists of E3Flat (flat planar zones that dont deform), 
/// and E3Flex (representing various types of bends that deform). Each Flex is controlled by 
/// a bending-lie that goes from 0.0 (fully flat) to 1.0 (fully bent). 
/// 
/// A BendPose contains one Node for each E3Thick (base class for E3Flat and E3Flex). That node
/// can be used for various purposes, including to power a visualization of the bending. 
/// Each Node returns a Mesh3 (representing the flat/flex to render) and an Xfm positioning it
/// in space.
/// 
/// The Node.Mesh for a flat node (Node.IsFlex == false) never changes since the planes do not
/// deform, while the Node.Mesh for a flex node (Node.IsFlex == true) changes as the flex is bent/
/// unbent and has to be redrawn completely. The Node implements IObservable(EProp) and publishes
/// EProp.Xfm whenever the positioning transform changes (for all nodes) and publishes
/// EProp.Geometry whenever the mesh geometry changes (only for flex nodes).
/// 
/// EnumFlexes is used to enumerate all the flexes of a BendPose (each has an internal Id), and
/// these Flex Ids can be used in later calls to SetFlexLie to bend/unbend the corresponding flex. 
public class BendPose {
   // Constructors -------------------------------------------------------------
   /// <summary>Builds a BendPose for a model (handles all the E3Flat/E3Flex entities of the model)</summary>
   public BendPose (Model3 model) {
      int max = model.Ents.OfType<E3Thick> ().Max (a => a.Id);
      mNodes = new Node[max + 1];

      int rootId = -1;
      foreach (var ent in model.Ents.OfType<E3Thick> ()) {
         if (rootId == -1) rootId = ent.Id;
         Node? pnode = ent.Parent == null ? null : mNodes[ent.Parent.Id];
         mNodes[ent.Id] = new Node (ent.Id, ent, pnode);

         E3Thick? parent = ent.Parent;
         while (parent != null) {
            mNodes[parent.Id].Subtree.Add (ent.Id);
            parent = parent.Parent;
         }
      }
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Returns the set of flexes, along with their current Ids and lies</summary>
   public IEnumerable<(int Id, E3Flex Flex, double Lie)> EnumFlexes
      => mNodes.Where (a => a?.IsFlex ?? false).Select (a => (a.Id, (E3Flex)a.Ent, a.Lie));

   /// <summary>Returns the set of Nodes for this BendPose</summary>
   public IEnumerable<Node> Nodes => mNodes.NonNull ();
   readonly Node[] mNodes;

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the bound of the BendPose in the current state</summary>
   public Bound3 GetBound (double lie = double.NaN) {
      if (!lie.IsNan) SetLie (lie);
      Bound3 bound = new ();
      foreach (var node in mNodes.NonNull ()) {
         if (node.Ent is E3Flex flex)
            bound += flex.BuildMesh (node.Lie).GetBound (node.Xfm);
         else
            bound += node.Ent.Mesh.GetBound (node.Xfm);
      }
      return bound;
   }

   /// <summary>Sets the lie of a particular Id</summary>
   public void SetFlexLie (int id, double lie)
      => mNodes[id].SetLie (mNodes, lie);

   /// <summary>Sets the lies for all the flexes to the given value</summary>
   public void SetLie (double lie) 
      => mNodes.Where (a => a?.IsFlex ?? false).ForEach (a => a.SetLie (mNodes, lie));

   // Nested types -------------------------------------------------------------
   /// <summary>Represents a Node in a BendPose (there is one for each E3Flat/E3Flex)</summary>
   public class Node : IObservable<EProp> {
      // Constructors ------------------------------------------------
      internal Node (int id, E3Thick ent, Node? parent) {
         parent ??= this;
         Id = id; IsFlex = (Ent = ent) is E3Flex;
         mParent = parent;
      }

      // Properties --------------------------------------------------
      /// <summary>The entity corresponding to this node (Flat/Flex)</summary>
      public readonly E3Thick Ent;
      /// <summary>Is this a flex? (if not, it's a flat)</summary>
      public readonly bool IsFlex;
      /// <summary>Internal ID of this node's Flat/Flex</summary>
      public readonly int Id;
      /// <summary>The current bending lie (relevant only if this is a Flex node)</summary>
      public double Lie => mLie;
      double mLie = 1;

      /// <summary>The transform to apply to this entity</summary>
      public Matrix3 Xfm => mXfm ??= ComputeXfm ();
      Matrix3? mXfm;

      // Methods -----------------------------------------------------
      /// <summary>Subscribes the EProp notifications from this node</summary>
      /// If this is a 
      public IDisposable Subscribe (IObserver<EProp> observer) => (mSubject = new ()).Subscribe (observer);
      Subject<EProp>? mSubject;

      // Implementation ----------------------------------------------
      // Computes the incremental transform between this node and its parent.
      // This is relevant only for a Flex node, and the incremental transform depends on 
      // the current lie of this flex. 
      Matrix3 ComputeIncremental () {
         if (!IsFlex) throw new NoriCodeException ("Flat.ComputeIncremental");
         if (Lie.EQ (1)) return Matrix3.Identity;
         E3Flex flex = (E3Flex)Ent;
         mFrom ??= Matrix3.From (flex.GetTailCS (1));
         return mFrom * Matrix3.To (flex.GetTailCS (Lie));
      }
      Matrix3 Incremental => mIncremental ??= ComputeIncremental ();
      Matrix3? mIncremental, mFrom;

      // Called to compute the transform for this node (when dirty)
      Matrix3 ComputeXfm () {
         if (mParent.Id == Id) return Matrix3.Identity;
         if (IsFlex) return mParent.Xfm;
         return mParent.Incremental * mParent.Xfm;
      }

      // Used to raise the IObservable notifications
      void Dirty (EProp prop) {
         if (prop == EProp.Xfm) mXfm = null;
         mSubject?.OnNext (prop);
      }

      // Called from owner BendPose to set the lie of this flex. 
      // When the lie changes, the subtree under this node needs to be redrawn
      internal void SetLie (Node[] nodes, double lie) {
         if (lie.EQ (Lie)) return;
         if (!IsFlex) throw new ArgumentException ($"{Id} is not a valid Flex ID");
         if (lie is < 0 or > 1) throw new ArgumentException ($"Lie is {lie}, should be between 0..1");
         mLie = lie; mXfm = mIncremental = null;
         Dirty (EProp.Geometry);
         Subtree.ForEach (a => nodes[a].Dirty (EProp.Xfm));
      }

      // Private data ------------------------------------------------
      readonly Node mParent;  // Parent node (points to self for the root node)
      // Indices of all descendents (not including this node)
      readonly internal List<int> Subtree = [];
   }
}
#endregion
