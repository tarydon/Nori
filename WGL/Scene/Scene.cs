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
   /// <summary>Used by Lux render to set the viewport for this Scene</summary>
   public Vec2S Viewport {
      get => mViewport;
      set {
         // Any time the viewport changes, reset the mXfm so it gets
         // computed again
         if (Lib.Set (ref mViewport, value))
            mXfm = null; 
      }
   }
   Vec2S mViewport;

   /// <summary>Read this property to get the overall Xfm (mapping input coordinates to GL clip space)</summary>
   public Matrix3 Xfm => mXfm ??= ComputeXfm ();
   Matrix3? mXfm;

   /// <summary>Background color (clear color) for this scene</summary>
   public abstract Color4 BgrdColor { get; }

   // Methods ------------------------------------------------------------------
   /// <summary>Override this to do the actual drawing</summary>
   public abstract void Draw ();

   // Overrides ----------------------------------------------------------------
   // Helper used internally by the Xfm property
   protected abstract Matrix3 ComputeXfm ();

   protected void XfmChanged () { mXfm = null; Lux.Redraw (); }
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
   protected override Matrix3 ComputeXfm () 
      => Matrix3.Map (mBound, Viewport);
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
   public (int XRot, int ZRot) Viewpoint {
      get => mViewpoint;
      set { mViewpoint = value; XfmChanged (); }
   }
   (int, int) mViewpoint = (-60, 135);

   // Implementation -----------------------------------------------------------
   // Compute the transform (based on the model Bound and the Viewpoint of rotation)
   protected override Matrix3 ComputeXfm () {
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
      double aspect = Viewport.X / (double)Viewport.Y, radius = Bound.Diagonal / 2, dx = radius, dy = radius;
      if (aspect > 1) dx = aspect * dy; else dy = dx / aspect;
      Bound3 frustum = new (-dx, -dy, -radius, dx, dy, radius);
      var projectionXfm = Matrix3.Orthographic (frustum);

      // This, then, is the overall Xfm from model coordinates to OpenGL clip space:
      return worldXfm * projectionXfm;
   }
}
#endregion
