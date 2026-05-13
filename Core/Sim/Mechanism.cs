// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Mechanism.cs
// ║║║║╬║╔╣║ Implements the Mechanism type (used to represent different types of kinematic chains)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections.Concurrent;
namespace Nori;

#region class Mechanism ----------------------------------------------------------------------------
/// <summary>Mechanism is used for various kinematic simulations</summary>
[EPropClass]
public partial class Mechanism {
   // Constructors -------------------------------------------------------------
   /// <summary>Loads a Mechanism from a curl file</summary>
   public static Mechanism Load (string curlFile) {
      var mech = (Mechanism)CurlReader.Load (curlFile);
      mech._rootDir = Path.GetDirectoryName (curlFile)!;
      return mech;
   }

   // Properties --------------------------------------------------------------
   /// <summary>Returns the bound of this mechanism</summary>
   public Bound3 Bound { get => Bound3.Cached (ref field, ComputeBound); } = new ();

   /// <summary>These are the children of this mechanism</summary>
   public IReadOnlyList<Mechanism> Children => mChildren ?? [];
   List<Mechanism>? mChildren;

   /// <summary>The mesh to use for collision testing. If a separate collision mesh is not defined,
   /// it returns the rendering mesh (if the rendering mesh exists)</summary>
   public TopoMesh? CMesh {
      get {
         if (field == null) {
            string name = FullName;
            if (!sCrashMeshCache.TryGetValue (name, out field)) {
               field = Geometry?.GetCMesh (RootDir) ?? Mesh?.ToTopoMesh ();
               sCrashMeshCache.TryAdd (name, field);
            }
         }
         return field;
      }
   }
   static readonly ConcurrentDictionary<string, TopoMesh?> sCrashMeshCache = [];

   /// <summary>The color used to draw this mechanism</summary>
   public readonly Color4 Color = Color4.Yellow;

   /// <summary>Which socket (on the parent) is this connected to</summary>
   public readonly int ConnectTo;

   /// <summary>The list of decals painted on this Mechanism</summary>
   public IReadOnlyList<Decal> Decals => mDecals ?? [];
   List<Decal>? mDecals = null;

   /// <summary>Where do we fetch our geometry from (could be null)</summary>
   public readonly GeomSrc? Geometry;

   /// <summary>Is this mechanism colliding?</summary>
   public bool IsColliding {
      get => _isColliding;
      set { if (Lib.Set (ref _isColliding, value)) Notify (EProp.Colliding); }
   }
   bool _isColliding;

   /// <summary>Is this mechanism visible?</summary>
   public bool IsVisible {
      get => _isVisible;
      set { if (Lib.Set (ref _isVisible, value)) Notify (EProp.Visibility); }
   }
   bool _isVisible = true;

   /// <summary>What kind of joint does this mechanism have?</summary>
   public readonly EJoint Joint;

   /// <summary>Range of movement for the axis (for rotary axes, these are in degrees)</summary>
   public readonly double JMin, JMax;

   /// <summary>The mechanism is modeled with the J value at this position:</summary>
   public readonly double JAsDrawn;

   /// <summary>The current value for the joint</summary>
   public double JValue {
      get => mJValue;
      set {
         if (mJValue.EQ (value) || value.IsNan) return;
         mJValue = value; EnumTree ().ForEach (a => a._xfm = null);
         Notify (EProp.JValue);
      }
   }
   double mJValue;

   /// <summary>The definition vector for the joint</summary>
   /// - For a translation joint, this is the direction of translation
   /// - For a rotation joint, this is the direction of the rotation axis. The
   ///   axis passes through the connection point on the parent
   public readonly Vector3 JVector;

   /// <summary>The mesh used to render this Mechanism (could be null)</summary>
   public Mesh3? Mesh {
      get {
         if (field == null) {
            string name = FullName;
            if (!sMeshCache.TryGetValue (name, out field)) {
               field = Geometry?.GetMesh (RootDir);
               sMeshCache.TryAdd (name, field);
            }
         }
         return field;
      }
   }
   static readonly ConcurrentDictionary<string, Mesh3?> sMeshCache = [];

   /// <summary>What's the name of this mechanism (or sub-mechanism)</summary>
   public readonly string Name = string.Empty;

   /// <summary>Sockets are used (by index) to connect children to this</summary>
   public readonly Point3[] Sockets = [];

   /// <summary>The parent mechanism of this</summary>
   public readonly Mechanism? Parent;

   /// <summary>Returns the 'incremental transformation matrix' of this piece (relative to its parent)</summary>
   public Matrix3 RelativeXfm {
      get {
         if (Parent == null) return Matrix3.Identity;
         Vector3 vec = Vector3.Zero;
         if (Parent.Sockets.Length > ConnectTo) vec = (Vector3)Parent.Sockets[ConnectTo];
         switch (Joint) {
            case EJoint.Translate:
               vec += JVector * (mJValue - JAsDrawn);
               return Matrix3.Translation (vec);
            case EJoint.Rotate:
               double ang = (mJValue - JAsDrawn).D2R ();
               return Matrix3.Rotation (JVector, ang) * Matrix3.Translation (vec);
            default:
               return Matrix3.Translation (vec);
         }
      }
   }

   /// <summary>The RootDir from which this was loaded (used to load the models)</summary>
   public string RootDir => _rootDir ??= Parent?.RootDir ?? "";
   string? _rootDir;

   /// <summary>Returns the transformation matrix for this piece of the mechanism</summary>
   public Matrix3 Xfm => _xfm ??= Parent == null ? Matrix3.Identity : RelativeXfm * Parent.Xfm;
   Matrix3? _xfm;

   // Methods ------------------------------------------------------------------
   /// <summary>Adds a child mechanism to this one</summary>
   public void AddChild (Mechanism child) => (mChildren ??= []).Add (child);

   /// <summary>Makes a clone of this mechanism</summary>
   /// Normally, mechanisms attached to machines, robots etc should not be directly updated,
   /// since we don't want the same mechanism to be used for multiple parts / jobs. We always 
   /// make a clone of the mechanism and use it
   public Mechanism Clone () => new (this, null);

   /// <summary>Enumerates the subtree of mechanisms under this one</summary>
   public IEnumerable<Mechanism> EnumTree () {
      yield return this;
      foreach (var child in Children)
         foreach (var grandchild in child.EnumTree ()) yield return grandchild;
   }

   public Mechanism? FindChild (string name)
      => EnumTree ().FirstOrDefault (a => a.Name == name);

   // Implementation -----------------------------------------------------------
   string FullName => Parent == null ? Name : $"{Parent.FullName}.{Name}";
   public override string ToString () => $"Mechanism:{Name}";

   // Constructor used for cloning
   Mechanism (Mechanism b, Mechanism? parent) {
      Color = b.Color; ConnectTo = b.ConnectTo; Geometry = b.Geometry; Joint = b.Joint; JMin = b.JMin; 
      JMax = b.JMax; JAsDrawn = b.JAsDrawn; JVector = b.JVector; JValue = b.JValue; 
      Name = b.Name; Sockets = b.Sockets; Parent = parent; _rootDir = b._rootDir;
      if (b.mChildren != null) 
         mChildren = [.. b.mChildren.Select (a => new Mechanism (a, this))];
   }
   Mechanism () { }

   // Computes the bound of this mechanism (subtree starting from here)
   Bound3 ComputeBound () {
      Bound3 b = new ();
      if (Mesh != null) b += Mesh.GetBound (Xfm);
      EnumTree ().Skip (1).ForEach (c => b += c.Bound);
      return b;
   }

   void PostLoad () {
      if ((mFlags & EFlags.Invisible) != 0) _isVisible = false;
   }

   // Nested types -------------------------------------------------------------
   [Flags]
   public enum EFlags { 
      /// <summary>The mechanism is initially invisible when loaded from the file</summary>
      Invisible = 1,

   }
   public EFlags Flags => mFlags;
   EFlags mFlags;
}
#endregion

#region struct Decal -------------------------------------------------------------------------------
/// <summary>Represents a decal that is pasted on a Mechanism</summary>
public readonly struct Decal {
   public Decal () { }
   public Decal (string file, CoordSystem cs, float scale)
      => (File, CS, Scale) = (file, cs, scale);

   public readonly string File = "";
   public readonly CoordSystem CS;
   public readonly float Scale;
}
#endregion

#region class GeomSrc ------------------------------------------------------------------------------
/// <summary>GeomSrc represents a 'geometry source' for a Mechanism node</summary>
/// The geometry source can provide geometry for the rendering (Mesh) and geometry for
/// the collision checking (CMesh)
public abstract class GeomSrc {
   /// <summary>Get the mesh used for rendering</summary>
   /// This always exists (for any mesh that includes a GeomSrc)
   public abstract Mesh3 GetMesh (string rootDir);
   /// <summary>Gets the special mesh to use for collision testing</summary>
   public abstract TopoMesh? GetCMesh (string rootDir);
}
#endregion

#region class MeshGeomSrc --------------------------------------------------------------------------
/// <summary>
/// MeshGeomSrc is a geometry source using a Mesh3 for the geometry data
/// </summary>
/// The input is a .msh file for geometry data, and the collision TopoMesh is derived from the
/// Mesh3 internally. Alternatively, the input could be specified using a .msh2 file, which contains
/// a .msh (Mesh3 for rendering) and a .msht (TopoMesh for collision) packed into a ZIP file. 
/// The mesh can optionally be positioned in space by the given coordinate system 
public class MeshGeomSrc : GeomSrc {
   // Constructors -------------------------------------------------------------
   /// <summary>
   /// Get the mesh for rendering
   /// </summary>
   public override Mesh3 GetMesh (string rootDir) { LoadMeshes (rootDir); return _mesh!; }
   public override TopoMesh? GetCMesh (string rootDir) { LoadMeshes (rootDir); return _crashMesh; }

   // Properties ---------------------------------------------------------------
   /// <summary>
   /// File from which the data is loaded
   /// </summary>
   public readonly string File = string.Empty;
   /// <summary>
   /// The coordinate system used to position the model 
   /// </summary>
   public CoordSystem CS => mCS;
   CoordSystem mCS;

   // Implementation -----------------------------------------------------------
   void LoadMeshes (string rootDir) {
      if (_mesh == null) {
         Matrix3 xfm = Matrix3.To (CS);
         var file = Path.Combine (rootDir, File);
         if (File.EndsWith (".mesh")) {
            _mesh = Mesh3.LoadFluxMesh (file) * xfm;
            _crashMesh = _mesh.ToTopoMesh ();
         } else {
            (_mesh, _crashMesh) = Mesh3.LoadMSH2 (file);
            _mesh *= xfm; _crashMesh *= xfm;
         }
      }
   }
   TopoMesh? _crashMesh;
   Mesh3? _mesh;
}
#endregion

public class SweptGeomSrc : GeomSrc {
   public readonly string File = null!;
   public readonly CoordSystem CS;
   public readonly Bound1[] Spans = null!;

   public override TopoMesh? GetCMesh (string rootDir) { LoadMeshes (rootDir); return _crashMesh!; }
   public override Mesh3 GetMesh (string rootDir) { LoadMeshes (rootDir); return _mesh!; }

   void LoadMeshes (string rootDir) {
      if (_mesh == null) {
         var file = Path.Combine (rootDir, File);
         var (mset, cset) = DXFReader.LoadDXF2 (file);
         if (Spans.Length != 1) throw new NoriCodeException ("Unsupported");
         _mesh = Extrude (mset, Spans[0], ETess.Medium);
         _crashMesh = Extrude (cset, Spans[0], ETess.Coarse).ToTopoMesh ();

         // Helpers ........................................
         Mesh3 Extrude (Poly[] set, Bound1 span, ETess tess) {
            var csFrom = new CoordSystem (new (0, 0, span.Length / 2));
            var xfm = Matrix3.Between (in csFrom, in CS);
            return Mesh3.Extrude (set, span.Length, xfm, ETess.Medium);
         }
      }
   }
   TopoMesh? _crashMesh;
   Mesh3? _mesh;
}

public class MultiGeomSrc : GeomSrc {
   public override TopoMesh? GetCMesh (string rootDir) 
      => new (Items.Select (a => a.GetCMesh (rootDir)).NonNull ());

   public override Mesh3 GetMesh (string rootDir) 
      => new (Items.Select (a => a.GetMesh (rootDir)));

   public readonly GeomSrc[] Items = [];
}
