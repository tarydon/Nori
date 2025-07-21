// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Scene.cs
// ║║║║╬║╔╣║ Implements the Scene base class, Scene2 (for 2D) and Scene3 (for 3D) classes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Scene --------------------------------------------------------------------------------
/// <summary>Base type used to represent 2D and 3D scenes</summary>
/// This exposes some basic properties like BgrdColor and Xfm which are common across all
/// types of scenes, and the important Draw() method that will be overridden in derived types
/// to draw the content
public abstract partial class Scene {
   // Properties ---------------------------------------------------------------
   /// <summary>Background color (clear color) for this scene</summary>
   public Color4 BgrdColor { get => mBgrdColor; set { mBgrdColor = value; Lux.Redraw (); } }
   Color4 mBgrdColor = Color4.Gray (128); 

   /// <summary>The pan-vector (0,0) means centered</summary>
   /// This is in OpenGL clip-space coordinates 
   public Vector2 PanVector {
      get => mPanVector;
      set { if (Lib.Set (ref mPanVector, value)) XfmChanged (); } 
   }
   Vector2 mPanVector = Vector2.Zero;

   /// <summary>The Projection transform (transforms world coordinates to OpenGL clip space)</summary>
   internal Matrix3 ProjectionXfm { get { _ = WorldXfm; return mProjectionXfm; } }
   Matrix3 mProjectionXfm = Matrix3.Identity;

   /// <summary>The root VNode of this Scene</summary>
   public VNode? Root { 
      get => mRoot;
      set {
         Debug.Assert (mRoot == null);
         (mRoot = value)?.Register (); 
      }
   }
   VNode? mRoot;

   /// <summary>The World transform (transforms model coordinates to world at (0,0,0)</summary>
   internal Matrix3 WorldXfm {
      get {
         if (mWorldXfm == null) (mWorldXfm, mProjectionXfm) = ComputeXfms ();
         return mWorldXfm;
      }
   }
   Matrix3? mWorldXfm;

   /// <summary>The stack of Xfms for this Scene (referenced by various VNode batches)</summary>
   internal List<XfmEntry> Xfms {
      get {
         if (mXfms.Count == 0) mXfms.Add (new XfmEntry (this));
         return mXfms;
      }
   }
   List<XfmEntry> mXfms = [];

   // Methods ------------------------------------------------------------------
   public void Render (Vec2S viewport) {
      Lux.Scene = this;
      if (Lib.Set (ref mViewport, viewport)) XfmChanged ();
      Xfms.RemoveRange (1, Xfms.Count - 1);
      mRoot?.Render ();
      RBatch.IssueAll ();
      Lux.Scene = null;
   }
   protected Vec2S mViewport;

   /// <summary>Called when the scene is detached from the Lux renderer</summary>
   public void Detach () => mRoot?.Deregister ();

   /// <summary>Override this to zoom in or out about the given position (in pixels)</summary>
   public void Zoom (Vec2S pos, double factor) {
      double oldZoom = mZoomFactor;
      mZoomFactor = (oldZoom * factor).Clamp (0.01, 100);
      factor = mZoomFactor / oldZoom;

      var vp = Lux.Viewport;
      Point3 mid = Midpoint * (WorldXfm * ProjectionXfm);
      Point2 pmid = new (vp.X * (mid.X + 1) / 2, vp.Y * (1 - mid.Y) / 2);
      Point2 pt = new (pos.X, pos.Y), pmouse2 = pmid + (pt - pmid) * factor;
      Vector2 vshift = pt - pmouse2;
      mPanVector += new Vector2 (2 * vshift.X / vp.X, -2 * vshift.Y / vp.Y);

      XfmChanged ();
   }
   protected double mZoomFactor = 1;

   // Called when the root transform is changed
   protected void XfmChanged () { 
      mXfms.Clear (); mWorldXfm = null;
      Lux.mViewBound.OnNext (0); Lux.Redraw (); 
   }

   public virtual void ZoomExtents () { 
      mZoomFactor = 1; mPanVector = Vector2.Zero;
      XfmChanged ();  
   }

   // Overrides ----------------------------------------------------------------
   // Helper used internally by the Xfm property
   protected abstract (Matrix3 World, Matrix3 Projection) ComputeXfms ();
   /// <summary>Returns where the 'midpoint' of the drawing / model is</summary>
   protected abstract Point3 Midpoint { get; }
}
#endregion

#region class Scene2 -------------------------------------------------------------------------------
/// <summary>Represents a 2D scene (override Draw in derived classes)</summary>
/// - The world extent is expressed as a Bound2 (world is defined on XY plane)
public class Scene2 : Scene {
   public Scene2 () { }
   public Scene2 (Color4 bgrd, Bound2 bound, VNode? root)
      => (BgrdColor, Bound, Root) = (bgrd, bound, root);

   // Properties ---------------------------------------------------------------
   /// <summary>The bounding rectangle of the drawing</summary>
   public Bound2 Bound {
      get => mBound;
      set { if (Lib.Set (ref mBound, value)) XfmChanged (); }
   }
   Bound2 mBound = new (0, 0, 10, 10);

   // Implementation -----------------------------------------------------------
   protected override (Matrix3 World, Matrix3 Projection) ComputeXfms () {
      var xfm = Matrix3.Map (mBound.InflatedF (1 / mZoomFactor), mViewport) * Matrix3.Translation (PanVector.X, PanVector.Y, 0);
      return (Matrix3.Identity, xfm);
   }

   // Returns the 'midpoint' of the drawing we are displaying
   protected override Point3 Midpoint { get { var mid = mBound.Midpoint; return new (mid.X, mid.Y, 0); }  }
}
#endregion

#region class Scene3 -------------------------------------------------------------------------------
/// <summary>Represents a 3D scene (override Draw in derived classes)</summary>
/// - The world extent is expressed as a Bound3
/// - The 'viewpoint' is expressed using an X + Z turntable convention
public abstract class Scene3 : Scene {
   // Properties ---------------------------------------------------------------
   /// <summary>The bounding cuboid of the model</summary>
   public Bound3 Bound {
      get => mBound;
      set { if (Lib.Set (ref mBound, value)) XfmChanged (); }
   }
   Bound3 mBound = new (0, 0, 0, 10, 10, 10);

   /// <summary>The rotation angle viewpoint</summary>
   public (double XRot, double ZRot) Viewpoint {
      get => mViewpoint;
      set {
         mViewpoint = (Lib.NormalizeAngle (value.XRot.D2R ()).R2D (), Lib.NormalizeAngle (value.ZRot.D2R ()).R2D ());
         XfmChanged ();
      }
   }
   (double, double) mViewpoint = (-60, 135);

   // Implementation -----------------------------------------------------------
   // Compute the transform (based on the model Bound and the Viewpoint of rotation)
   protected override (Matrix3 World, Matrix3 Projection) ComputeXfms () {
      // The worldXfm maps the bounding cuboid of the model to the origin, and 
      // then rotates the model by applying the rotation viewpoint (a quaternion with X and
      // Z rotations, implementing a simple turntable rotation model)
      var (x, z) = Viewpoint;
      var viewpoint = Quaternion.FromAxisRotations (x.D2R (), 0, z.D2R ());
      var mid = Bound.Midpoint;
      var worldXfm = Matrix3.Translation (-mid.X, -mid.Y, -mid.Z) * Matrix3.Rotation (viewpoint);

      // The projectionXfm maps the sphere that contains the model (now centered at the origin
      // thanks to worldXfm) to the OpenGL clip space. The computation is complicated a bit
      // by the fact that we have to account for the aspect ratio of the window
      double aspect = mViewport.X / (double)mViewport.Y, radius = Bound.Diagonal / 2, dx = radius / mZoomFactor, dy = radius / mZoomFactor;
      if (aspect > 1) dx = aspect * dy; else dy = dx / aspect;
      Bound3 frustum = new (-dx, -dy, -radius, dx, dy, radius);
      var projectionXfm = Matrix3.Orthographic (frustum) * Matrix3.Translation (PanVector.X, PanVector.Y, 0);
      return (worldXfm, projectionXfm);
   }

   // Returns the 'midpoint' of the model we are displaying
   protected override Point3 Midpoint => mBound.Midpoint;
}
#endregion

#region class XfmEntry -----------------------------------------------------------------------------
/// <summary>XfmEntry is used to build the stack of transforms</summary>
/// Each XfmEntry points to a parent, and has an incremental transform from that. 
/// The XfmEntry then computes the overall transform (Xfm) and the associated
/// NormalXfm for lighting computations (for 3D scenes)
class XfmEntry {
   // Constructors -------------------------------------------------------------
   /// <summary>Root XfmEntry of a Scene</summary>
   public XfmEntry (Scene scene) {
      mScene = scene; mIncremental = Matrix3.Identity;
      mIs3D = scene is Scene3;
   }
   /// <summary>Incremental XfmEntry (based off a parent)</summary>
   public XfmEntry (XfmEntry parent, Matrix3 incremental) {
      mIs3D = (mScene = (mParent = parent).mScene) is Scene3;
      mIncremental = incremental;
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The NormalXfm to use for 3D scenes</summary>
   public ref Mat4F NormalXfm { get { _ = ObjToWorld; return ref mNormalXfm; } }

   /// <summary>The overall transform from the object to world coordinates</summary>
   /// If we multiply this with the scene's projectXfm, we will get the final
   /// overally Xfm for this object
   Matrix3 ObjToWorld {
      get {
         if (mObjToWorld == null) {
            if (mParent == null) mObjToWorld = mScene.WorldXfm;
            else mObjToWorld = mIncremental * mParent.ObjToWorld;
            mXfm = (Mat4F)(ObjToWorld * mScene.ProjectionXfm);
            if (mIs3D) mNormalXfm = (Mat4F)ObjToWorld.ExtractRotation ();
         }
         return mObjToWorld;
      }
   }
   Matrix3? mObjToWorld;

   /// <summary>The overall transform from the world to the OpenGL clip coordinates</summary>
   public ref Mat4F Xfm { get { _ = ObjToWorld; return ref mXfm; } }
   Mat4F mXfm, mNormalXfm;

   /// <summary>Inverse transform that transforms OpenGL clip spaces to world (inverse of Xfm)</summary>
   public Matrix3 InvXfm => mInvXfm ??= (mScene.WorldXfm * mScene.ProjectionXfm).GetInverse ();
   Matrix3? mInvXfm;

   // Private data -------------------------------------------------------------
   readonly Scene mScene;           // The scene we're working with
   readonly bool mIs3D;             // Is this a 3D scene
   readonly XfmEntry? mParent;      // The parent XfmEntry this is derived from
   readonly Matrix3 mIncremental;   // The incremental transform from that parent
}
#endregion
