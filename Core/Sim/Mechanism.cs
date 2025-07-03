// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Mechanism.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Concurrent;
namespace Nori;

#region class Mechanism ----------------------------------------------------------------------------
/// <summary>Mechanism is used for various kinematic simulations</summary>
public class Mechanism {
   // Constructors -------------------------------------------------------------
   /// <summary>Loads a Mechanism from a curl file</summary>
   public static Mechanism Load (string curlFile) {
      var mech = (Mechanism)CurlReader.FromFile (curlFile);
      mech._rootDir = Path.GetDirectoryName (curlFile)!;
      return mech;
   }

   // Properties --------------------------------------------------------------
   /// <summary>Returns the bound of this mechanism</summary>
   public Bound3 Bound {
      get {
         if (_bound.IsEmpty) {
            if (Mesh != null) _bound += Mesh.Bound;
            EnumTree ().Skip (1).ForEach (c => _bound += c.Bound);
         }
         return _bound;
      }
   }
   Bound3 _bound = new ();

   /// <summary>These are the children of this mechanism</summary>
   public IReadOnlyList<Mechanism> Children => mChildren ?? [];
   List<Mechanism>? mChildren;

   /// <summary>The color used to draw this mechanism</summary>
   public readonly Color4 Color = Color4.Yellow;

   /// <summary>Which socket (on the parent) is this connected to</summary>
   public readonly int ConnectTo;

   /// <summary>
   /// Where do we fetch our geometry from (could be null)
   /// </summary>
   public readonly GeometrySource? Geometry;

   /// <summary>What kind of joint does this mechanism have?</summary>
   public readonly EJoint Joint;

   /// <summary>Range of movement for the axis (for rotary axes, these are in degrees)</summary>
   public readonly double JMin, JMax;

   /// <summary>
   /// The mechanism is modeled with the J value at this position:
   /// </summary>
   public readonly double JAsDrawn;

   /// <summary>The current value for the joint</summary>
   public double JValue { get => mJValue; set => mJValue = value; }
   double mJValue;

   /// <summary>The definition vector for the joint</summary>
   /// - For a translation joint, this is the direction of translation
   /// - For a rotation joint, this is the direction of the rotation axis. The
   ///   axis passes through the connection point on the parent
   public readonly Vector3 JVector;

   /// <summary>
   /// The mesh used to render this Mechanism (could be null)
   /// </summary>
   public CMesh? Mesh {
      get {
         if (_mesh == null) {
            string name = FullName;
            if (!sMeshCache.TryGetValue (name, out _mesh)) {
               _mesh = Geometry?.GetMesh (RootDir);
               sMeshCache.TryAdd (name, _mesh);
            }
         }
         return _mesh;
      }
   }
   CMesh? _mesh;
   static ConcurrentDictionary<string, CMesh?> sMeshCache = [];

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

   // Methods ------------------------------------------------------------------
   /// <summary>
   /// Adds a child mechanism to this one
   /// </summary>
   public void AddChild (Mechanism child) => (mChildren ??= []).Add (child);

   /// <summary>Enumerates the subtree of mechanisms under this one</summary>
   public IEnumerable<Mechanism> EnumTree () {
      yield return this;
      foreach (var child in Children)
         foreach (var grandchild in child.EnumTree ()) yield return grandchild;
   }

   // Implementation -----------------------------------------------------------
   string FullName => Parent == null ? Name : $"{Parent.FullName}.{Name}";
   Point3 _connectToPt;
}
#endregion

public abstract class GeometrySource {
   public abstract CMesh GetMesh (string rootDir);
}

public class FileGeometry : GeometrySource {
   public override CMesh GetMesh (string rootDir) {
      string file = Path.Combine (rootDir, File);
      Matrix3 xfm = Rotate.IsIdentity ? Matrix3.Identity : Matrix3.Rotation (Rotate);
      xfm *= Matrix3.Translation (Shift);
      return CMesh.Load (file) * xfm;
   }

   public readonly string File = string.Empty;
   public readonly Quaternion Rotate = Quaternion.Identity;
   public readonly Vector3 Shift = Vector3.Zero;
}
