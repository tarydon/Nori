// ────── ╔╗
// ╔═╦╦═╦╦╬╣ VNode.cs
// ║║║║╬║╔╣║ Implements the VNode class (base class for all visual nodes)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class VNode --------------------------------------------------------------------------------
/// <summary>VNode is the base class for all visual nodes - all VNodes in a scene form a single-rooted DAG</summary>
/// A hierarchy of VNodes is used to organize all the drawing that happens in a scene.
/// - A VNode can provide some attributes (like Color, LineType, Xfm) that are used for
///   drawing its contents. Some of these attributes (Color, Xfm etc) are also inherited by
///   the entire sub-tree of VNodes under this one. 
/// - A VNode can control which of the attributes it is setting are actually inherited by
///   its children, by providing a value for the Bequeath property
/// - A VNode implements a Draw() method that is called to draw the actual contents using
///   various Lux.Draw calls (after the attributes are first set using SetAttributes). 
/// - A VNode can have zero or more _child_ VNodes, which are enumerated using GetChild()
/// - It is possible for a VNode to have multiple parents (it can appear multiple times in 
///   the scene graph). 
public class VNode {
   // Constructors -------------------------------------------------------------
   /// <summary>Default VNode constructor</summary>
   public VNode () { }
   /// <summary>Constructor that allows you to specify which 'object' a VNode is rendering</summary>
   /// This should be used where there is a natural domain-space object that this
   /// VNode is rendering. During a 'pick' operation, this object (if one is bound to the VNode)
   /// is returned, so Lux.Pick returns domain-space objects, rather than internal rendering 
   /// objects. 
   public VNode (object obj) => Obj = obj;

   /// <summary>Get the VNode, given an ID</summary>
   public static VNode Get (int id) => mNodes[id]!;

   // Properties ---------------------------------------------------------------
   /// <summary>The set of render-batches for this VNode, along with the corresponding uniforms</summary>
   /// This is a list of tuples - the NBatch value of each tuple is the index of a RBatch
   /// in the global RBatch array, and you can fetch the actual RBatch using RBatch.Get(int). 
   /// 
   /// That RBatch specifies the following:
   /// - Which shader is used
   /// - Which RBuffer the vertices lie in
   /// - The Start and Count of the vertices in that buffer
   /// - If indexed drawing is being used, the Start and Count of the indices in that buffer
   /// 
   /// In addition, that RBatch will also contain a Uniform value that is an index into
   /// the Uniform[] set of the relevant shader. However, we don't use that RBatch.NUniform,
   /// but store the uniforms separately here as the second element of these tuples. 
   /// 
   /// That is because the same RBatch could be used multiple times by different VNodes - that
   /// is different VNodes could be drawn using the same vertices of geometry. However, each will
   /// have a different NUniform slot, and a common use-case would be that these different sets 
   /// of uniforms provide different transformation matrices positioning the same geometry in 
   /// different locations (instancing). 
   internal List<(int NBatch, ushort NUniform)> Batches = [];

   /// <summary>This is the container within our Obj from which we can get child objects</summary>
   /// If a ChildSource collection is specified for a VNode, it completely automates the 
   /// creation, management and deletion of child VNodes for that node. The ChildSource should
   /// implement IList and IObservable(ListChange) so it can be used to fetch items by index,
   /// and also to listen to changes in that list. The AList(T) collection implements both
   /// these interfaces, so you can just pass in an AList(T) and that will work. 
   public IAList? ChildSource { get => mChildSource; init => mChildSource = value; }
   IAList? mChildSource;

   // The unique Id of this VNode within this scene. 
   // If the Id = 0, then the VNode has not yet been 'registered' into this Scene,
   // and has been freshly created.
   internal int Id;

   /// <summary>The domain-space Object which this VNode is rendering</summary>
   public readonly object? Obj;

   // Methods ------------------------------------------------------------------
   /// <summary>Should be called from outside when the parent object has new children</summary>
   public void ChildAdded () { mChildrenAdded = true; Lux.Redraw (); }

   /// <summary>Called to tell this parent one of its children is removed</summary>
   public void ChildRemoved (VNode child) {
      mFamily.Remove (ref mChildren, child.Id);
      mFamily.Remove (ref child.mParents, Id);
      if (--child.mCRefs <= 0) child.Deregister ();
      mKnownChildren--;
      Lux.Redraw ();
   }

   /// <summary>Holds onto an IDisposable and disposes it when the VNode is detached</summary>
   /// Many VNodes watch various objects (like the owner, the Lux ViewBounds, the
   /// list of Children in the owner etc). All those watches are implemented using
   /// the observable pattern, and so we end up with a number of disposables we need
   /// to clear when we detach. This routine shifts that responsibility to VNode, and
   /// all derived types can simply hand over their IDisposables to this routie which 
   /// will track them and dispose of them when OnDetach is called
   public void DisposeOnDetach (IDisposable disp) => (mDisposer ??= new ()).Add (disp);
   MultiDispose? mDisposer;

   /// <summary>'Virtual constructor' to make a VNode for a given object type</summary>
   /// An assembly that implements various types of VNode (like CircleVN, CubeVN etc) can
   /// be _Registered_ to the VNode system by calling VNode.RegisterAssembly(...). That builds 
   /// up a mBuilder dictionary that maps object types to VNode types (their constructors, 
   /// actually). Then, when that kind of object comes in, we can pick the appropriate constructor
   /// and construct the VNode for that. See RegisterAssembly for more details.
   public static VNode MakeFor (object obj) {
      ArgumentNullException.ThrowIfNull (obj);
      if (!mBuilders.TryGetValue (obj.GetType (), out var ci))
         throw new Exception ($"No VNode found for {obj.GetType ().FullName}");
      return (VNode)ci.Invoke ([obj]);
   }

   /// <summary>Called when geometry has changed and complete redraw of this VNode is needed</summary>
   public void Redraw () { mGeometryDirty = true; Lux.Redraw ();  }

   /// <summary>Register an assembly as containing potential VNode types</summary>
   /// This is used in conjunction with the VNode.Makefor(...) above to construct a VNode
   /// given an object. It basically walks through all VNode-derived types in that assembly, 
   /// sees if they have a construct that takes some type T and if so, registers that VNode
   /// as the corresponding VNode for objects of type T. The type and the constructor should
   /// be public. If you have multiple VNode types for a given type T that will raise an 
   /// exception when attempting to register the assembly (this is ambiguous). To avoid this 
   /// situation, you could 'hide' one of those two VNode types by adding additional dummy 
   /// parameters to the constructor.
   public static void RegisterAssembly (Assembly assy) {
      if (mAssemblies.Add (assy)) {
         // Get all the types in the assembly where 'VModels' are defined. Here, we just
         // use the assembly where VModel is defined, and we are assuming that all VModel types
         // are in this assembly. In the more general case, this will have to be a list of assemblies
         // where VModel derived types are defined. Perhaps all those assemblies will 'register' 
         // themselves with the VModel system so this virtual constructor table can be built
         // correctly.
         var allTypes = assy.GetTypes ();
         var vmTypes = allTypes.Where (a => a.IsAssignableTo (typeof (VNode))).ToList ();
         foreach (var vm in vmTypes) {
            // Skip abstract VModel types - we cannot build instances of 
            // those (for example, skip the actual "VModel" class itself). 
            if (vm.IsAbstract) continue;
            foreach (var ci in vm.GetConstructors ()) {
               // Get constructors that take one parameter and add them to the
               // map. Note that we are using Dictionary.Add, so if we have two VModel types
               // both handling Line entity, this will throw an exception. Presumably, that case
               // should not happen. If there are multiple ways to draw a line based on some 
               // parameters of the line, there should be a single LineVM that handles all those
               // cases in its Draw() method
               var args = ci.GetParameters ();
               if (args.Length == 1) mBuilders.Add (args[0].ParameterType, ci);
            }
         }
      }
   }
   // This dictionary maps particular domain types (like Poly, Text etc) to constructors
   // in appropriate VNode types (like PolyVN, TextVN etc)
   static Dictionary<Type, ConstructorInfo> mBuilders = [];
   // These are the assemblies we have already searched to find VNode-derived types
   static HashSet<Assembly> mAssemblies = [];

   // Overrides ----------------------------------------------------------------
   /// <summary>Specifies which attributes are inherited by children of this VNode</summary>
   public virtual ELuxAttr Bequeath => ELuxAttr.Color | ELuxAttr.Xfm | ELuxAttr.TypeFace;

   /// <summary>Override this to draw the contents of this VNode</summary>
   /// It is possible that some VNodes will not override this - they will exist purely
   /// to provide some attributes to the subtree beneath. For example, XfmVNode exists only
   /// to provide a Xfm using which the entire subtree underneath it is drawn. 
   public virtual void Draw () { }

   /// <summary>Override this to return the children of this VNode</summary>
   /// The Lux system will call GetChild, starting with an index of 0 and
   /// going to successive indices, as long as this returns a non-null child. So,
   /// returning null for this call is the way to indicate 'no-more-children'. 
   /// 
   /// We provide a default implementation of this if the ChildSource property is set.
   /// Then, the mAutoChildren list is maintained here having a one-to-one correspondence
   /// between the child objects in ChildSource and the child VNodes. Given a particular
   /// child object, the MakeFor 'virtual constructor' is used to create the corresponding
   /// VNode. 
   public virtual VNode? GetChild (int n) {
      if (mAutoChildren != null) {
         while (mAutoChildren.Count < mChildSource!.Count)
            mAutoChildren.Add (MakeFor (mChildSource[mAutoChildren.Count]!));
         return mAutoChildren.SafeGet (n);
      }
      return null;
   }
   List<VNode>? mAutoChildren;

   /// <summary>Override this to set up the attributes for the VNode</summary>
   /// By default, Color, Xfm and TypeFace are inherited by the entire sub-tree under
   /// this VNode, unless overridden by one of the children. (See Bequeath above)
   public virtual void SetAttributes () { }

   /// <summary>Override this to do something specific after this VNode is 'attached'</summary>
   /// This means the VNode is now part of the scene-graph of a Scene that is being
   /// rendered in Lux (this is called after the VNode is already attached)
   public virtual void OnAttach () { }

   /// <summary>Override this to do something specific when this VNode is getting 'detached'</summary>
   /// When the VNode is retiring from the scene-graph of a Scene, this is called before
   /// the actual detaching takes place
   public virtual void OnDetach () { } 

   /// <summary>Observer implementation that watches the owner Obj for changes</summary>
   /// If the Obj this VNode is displaying implements IObservable(EProp), then
   /// this handler is set up to observe it automatically. It responds to some
   /// known properties like Geometry, Attributes, Xfm etc to dirty the appropriate
   /// state and issue a redraw. You can override this in child VNodes, but remember
   /// to call the base OnChanged for this core functionality to continue working
   /// TODO: Check we are disconnecting correctly
   protected virtual void OnChanged (EProp prop) {
      switch (prop) {
         case EProp.Geometry: Redraw (); break;
         case EProp.Attributes: case EProp.Xfm: Lux.Redraw (); break;
      }
   }

   public override string ToString ()
      => $"{GetType ().Name} ({Obj})";

   // Implementation -----------------------------------------------------------
   // Release all the geometry batches owned by this VNode.
   // This is called when the VNode is finally detached from the renderer, and
   // also whenever the geometry is dirty and new batches have to be gathered
   void ReleaseBatches () {
      foreach (var (n, _) in Batches) {
         ref RBatch rb = ref RBatch.Get (n);
         rb.Release ();
      }
      Batches.Clear ();
   }

   // This is the core routine used to draw a VNode and the entire subtree of nodes
   // under it. So, call Scene.Root.Render() will draw the entire scene. 
   internal void Render () {
      Lux.BeginNode (this);
      try {
         // If the GeometryDirty flag is set, it means the geometry of this VNode
         // has changed - the batches it might be holding on to are all useless now, and we
         // release those and gather fresh batches (encapsulating the new geometry)
         if (mGeometryDirty) ReleaseBatches ();

         // Before we draw, we call SetAttributes - this just makes a bunch of calls to
         // set various Lux properties (like Color, Xfm, LineType etc) and those are then 
         // latched into the global Lux properties state (ready to be captured into RBatches by
         // the upcoming call to Draw). 
         SetAttributes ();

         // Now, if the GeometryDirty flag is set (which is also our state when the VNode is
         // first created), we call Draw(). In response, the VNode will issue various Lux draw calls
         // (like Lines, Beziers, Points, Mesh etc) to create new RBatches. 
         // These Draw calls that we are using will create new batches, and will place those
         // (batch-index, uniform-index) tuples into the Batches[] list of the VNode that is currently
         // being drawn (that's why we call BeginNode at the start of this function). 
         if (mGeometryDirty) {
            Draw ();
            mGeometryDirty = false;
         } else {
            // But if the geometry is not dirty, we don't need to create new batches at all,
            // but instead can just update the existing batches we have with freshly captured
            // batches (that the Shader will snap for us from the Lux global state). ☼IDEA001
            for (int i = 0; i < Batches.Count; i++) {
               var (b, u) = Batches[i];
               var shader = Shader.Get (RBatch.Get (b).NShader);
               Batches[i] = (b, shader.SnapUniforms ());
            }
         }

         // Now all the geometry is drawn (or the batch attributes have changed), we can take all
         // the batches we have and add them to the global staging area - this is collecting all
         // the batches we want to render in this frame
         RBatch.Staging.AddRange (Batches);

         // From the BeginNode call above to this point, this node might have changed some attributes.
         // But not all of these are changes we want to pass on to our children (which we are going to 
         // draw shortly below). So reset some of these attributes (all that are not 'bequeathed' to
         // children) using a PopAttr call. 
         Lux.PopAttr (~Bequeath);

         // If we've indicated that the set of children has grown (which is also our state when
         // the VNode is first created), we go through a loop that enumerates children using
         // GetChild(n) until there are no more children left. Since this is called not only at
         // the start of this VNode's life, but also subsequently when additional children are added,
         // we can start this enumeration from mKnownChildren (the children this node is already
         // known to have, and which are registered as part of the scene already)
         if (mChildrenAdded) {
            for (; ; mKnownChildren++) {
               var child = GetChild (mKnownChildren);
               if (child == null) break;
               // Note that a child returned here might have an Id > 0 (already registered), since
               // the same child can occur multiple times in the scene's DAG of nodes. 
               if (child.Id == 0) child.Register ();
               // This sets up the bi-directional links connecting a parent to all of its children,
               // and a child to all of its parents (there may be more than 1).
               mFamily.Add (ref mChildren, child.Id);
               mFamily.Add (ref child.mParents, Id);
               child.mCRefs++;
            }
            mChildrenAdded = false;
         }

         // The code above has refreshed the linked-list of children we have, and we can now
         // recursively call Render() on each of them
         foreach (var n in mFamily.Enum (mChildren))
            Get (n).Render ();
      } finally {
         // Now we've drawn this VNode and the entire subtree of VNode under it. It's time 
         // to reset any attributes we might have set back to their previous value before 
         // returning, and EndNode does that. We do this by maintaining a Stack for each 
         // individual attribute, so this is just a series of pops off the various stacks
         // (like the color stack, idxfm stack etc).
         Lux.EndNode ();
      }
   }

   // Helper to handle the case where we have a 'child-source' collection that is 
   // observable. In this case, we handle adding and removing of children from that collection
   // by doing corresponding adds and remove of child VNodes
   void OnChildrenChanged (ListChange ch) {
      IAList source = mChildSource!;
      switch (ch.Action) {
         case ListChange.E.Added:
            mAutoChildren!.Add (MakeFor (source[ch.Index]!));
            ChildAdded ();
            break;
         case ListChange.E.Removing: 
            Remove (ch.Index); 
            break;
         case ListChange.E.Clearing:
            for (int i = mAutoChildren!.Count - 1; i >= 0; i--) Remove (i);
            break;
      }

      // Helper ............................................
      void Remove (int n) {
         ChildRemoved (mAutoChildren![ch.Index]);
         mAutoChildren.RemoveAt (ch.Index);
      }
   }

   // This is called to allot an ID for this VNode and to add it to the list of nodes.
   // This is called exactly once in the lifetime of the node, and is balanced eventually
   // by one call to Deregister (this is true even if the node is participating in multiple
   // scenes - Register will be called only once).
   internal void Register () {
      Debug.Assert (Id == 0);
      if (mFreeIDs.Count == 0) {
         int size = Math.Max (mNodes.Length * 2, 8);
         for (int i = size - 1; i >= mNodes.Length; i--) mFreeIDs.Push (i);
         Array.Resize (ref mNodes, size);
      }
      Id = mFreeIDs.Pop (); mNodes[Id] = this; 
      if (Obj is IObservable<EProp> observable)
         DisposeOnDetach (observable.Subscribe (OnChanged));
      if (mChildSource != null) {
         mAutoChildren = new List<VNode> (mChildSource.Count);
         DisposeOnDetach(mChildSource.Subscribe (OnChildrenChanged));
      }
      if (GetType ().HasAttribute<RedrawOnZoomAttribute> ())
         DisposeOnDetach (Lux.ViewBound.Subscribe (_ => Redraw ()));
      OnAttach ();
   }
   static VNode?[] mNodes = [null];
   static Stack<int> mFreeIDs = [];

   // Deregister is called when this VNode is no longer ever required.
   // This means it is not part of any scenes and its 'parent refs' counter has
   // run down to zero.
   internal void Deregister () {
      Debug.Assert (Id > 0);
      Lib.Trace ($"Detach {this}\n");
      OnDetach ();
      mDisposer?.Dispose ();
      ReleaseBatches ();
      foreach (var c in mFamily.Enum (mChildren)) {
         // Walk through the children, and disconnect them from _this_ parent. 
         // The child may continue living, since it may have other parents. 
         // Otherwise, it will end up Deregistering itself
         var child = Get (c);
         mFamily.Remove (ref child.mParents, Id);
         if (--child.mCRefs <= 0) child.Deregister ();
      }
      mFamily.ReleaseChain (ref mChildren);
      mFreeIDs.Push (Id); mNodes[Id] = null;
      mGeometryDirty = mChildrenAdded = true;
      Id = 0; 
   }

   // Private data -------------------------------------------------------------
   // The number of children this Node is already known to have
   // If additional children are added, we need to start enumerating only
   // the children beyond this number
   int mKnownChildren;
   // Handles of the 'parents' and 'children' lists for this node. Passing this to
   // the mFamily chains structure enumerates the actual parents and children of 
   // this node
   int mParents, mChildren;
   // This tracks parent-child relationships within all VNodes. 
   // The mParents and mChildren fields above are handles to linked-lists within
   // this Chains structure
   static Chains<int> mFamily = new ();
   // Number of parents this VNode has. When this runs down to zero, we can 
   // release the VNode
   int mCRefs;
   
   // If set, this means we might have new children, and a fresh GetChild()
   // enumeration is required
   bool mChildrenAdded = true;
   // If set, this means the geometry has changed and fresh RBatches have to 
   // be gathered for our geometry
   protected bool mGeometryDirty = true;
}
#endregion
