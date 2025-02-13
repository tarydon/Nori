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
public abstract class Scene {
   // Properties ---------------------------------------------------------------
   public Matrix3 NormalXfm { get { _ = Xfm; return mNormalXfm!; } }
   Matrix3? mNormalXfm;

   /// <summary>The pan-vector (0,0) means centered</summary>
   /// This is in OpenGL clip-space coordinates 
   public Vector2 PanVector {
      get => mPanVector;
      set { if (Lib.Set (ref mPanVector, value)) XfmChanged (); } 
   }
   Vector2 mPanVector = Vector2.Zero;

   /// <summary>Used by Lux render to set the viewport for this Scene</summary>
   public Vec2S Viewport {
      get => mViewport;
      set { if (Lib.Set (ref mViewport, value)) XfmChanged (); }  
   }
   Vec2S mViewport;

   /// <summary>Read this property to get the overall Xfm (mapping input coordinates to GL clip space)</summary>
   public Matrix3 Xfm {
      get {
         if (mXfm == null) (mXfm, mNormalXfm) = ComputeXfm ();
         return mXfm;
      }
   }
   Matrix3? mXfm;

   /// <summary>Background color (clear color) for this scene</summary>
   public abstract Color4 BgrdColor { get; }

   // Methods ------------------------------------------------------------------
   /// <summary>Override this to do the actual drawing</summary>
   public abstract void Draw ();

   /// <summary>Override this to zoom in or out about the given position (in pixels)</summary>
   public void Zoom (Vec2S pos, double factor) {
      double oldZoom = mZoomFactor;
      mZoomFactor = (oldZoom * factor).Clamp (0.01, 100);
      factor = mZoomFactor / oldZoom;

      var vp = Lux.Viewport;
      Point3 mid = Midpoint * Xfm;
      Point2 pmid = new (vp.X * (mid.X + 1) / 2, vp.Y * (1 - mid.Y) / 2);
      Point2 pt = new (pos.X, pos.Y), pmouse2 = pmid + (pt - pmid) * factor;
      Vector2 vshift = pt - pmouse2;
      mPanVector += new Vector2 (2 * vshift.X / vp.X, -2 * vshift.Y / vp.Y);

      XfmChanged ();
   }
   protected double mZoomFactor = 1;

   public void ZoomExtents () { 
      mZoomFactor = 1; mPanVector = Vector2.Zero; 
      XfmChanged ();  
   }

   /// <summary>Returns where the 'midpoint' of the drawing / model is</summary>
   protected abstract Point3 Midpoint { get; }

   // Overrides ----------------------------------------------------------------
   // Helper used internally by the Xfm property
   protected abstract (Matrix3 Xfm, Matrix3 NormalXfm) ComputeXfm ();
   protected void XfmChanged () { mXfm = mNormalXfm = null; Lux.Redraw (); }
}
#endregion

#region class Scene2 -------------------------------------------------------------------------------
/// <summary>Represents a 2D scene (override Draw in derived classes)</summary>
/// - The world extent is expressed as a Bound2 (world is defined on XY plane)
public abstract class Scene2 : Scene {
   // Properties ---------------------------------------------------------------
   /// <summary>The bounding rectangle of the drawing</summary>
   public Bound2 Bound {
      get => mBound;
      set { if (Lib.Set (ref mBound, value)) XfmChanged (); }
   }
   Bound2 mBound = new (0, 0, 10, 10);

   // Implementation -----------------------------------------------------------
   // Computes the transformation to map the bounding rectangle to the viewport
   protected override (Matrix3 Xfm, Matrix3 NormalXfm) ComputeXfm () {
      var xfm = Matrix3.Map (mBound.InflatedF (1 / mZoomFactor), Viewport) * Matrix3.Translation (PanVector.X, PanVector.Y, 0);
      return (xfm, xfm);
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
      set { mViewpoint = value; XfmChanged (); }
   }
   (double, double) mViewpoint = (-60, 135);

   // Implementation -----------------------------------------------------------
   // Compute the transform (based on the model Bound and the Viewpoint of rotation)
   protected override (Matrix3 Xfm, Matrix3 NormalXfm) ComputeXfm () {
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
      double aspect = Viewport.X / (double)Viewport.Y, radius = Bound.Diagonal / 2, dx = radius / mZoomFactor, dy = radius / mZoomFactor;
      if (aspect > 1) dx = aspect * dy; else dy = dx / aspect;
      Bound3 frustum = new (-dx, -dy, -radius, dx, dy, radius);
      var projectionXfm = Matrix3.Orthographic (frustum);

      // This, then, is the overall Xfm from model coordinates to OpenGL clip space:
      var xfm = worldXfm * projectionXfm * Matrix3.Translation (PanVector.X, PanVector.Y, 0);
      var normalXfm = worldXfm.ExtractRotation ();
      return (xfm, normalXfm);
   }

   // Returns the 'midpoint' of the model we are displaying
   protected override Point3 Midpoint => mBound.Midpoint;
}
#endregion

#region class BlankScene ---------------------------------------------------------------------------
/// <summary>An empty scene (draws nothing) that is the default scene when Lux starts up</summary>
public class BlankScene : Scene2 {
   public override Color4 BgrdColor => Color4.Gray (96);
   public override void Draw () { }
}
#endregion
