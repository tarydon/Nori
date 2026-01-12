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
   public Bound3 Bound => Bound3.Cached (ref _bound, ComputeBound);
   Bound3 _bound = new ();

   /// <summary>These are the children of this mechanism</summary>
   public IReadOnlyList<Mechanism> Children => mChildren ?? [];
   List<Mechanism>? mChildren;

   /// <summary>The color used to draw this mechanism</summary>
   public readonly Color4 Color = Color4.Yellow;

   /// <summary>Which socket (on the parent) is this connected to</summary>
   public readonly int ConnectTo;

   /// <summary>Where do we fetch our geometry from (could be null)</summary>
   public readonly GeometrySource? Geometry;

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
   Mesh3? _mesh;
   static ConcurrentDictionary<string, Mesh3?> sMeshCache = [];

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
}
#endregion

public abstract class GeometrySource {
   public abstract Mesh3 GetMesh (string rootDir);
}

public class FileGeometry : GeometrySource {
   public override Mesh3 GetMesh (string rootDir) {
      string file = Path.Combine (rootDir, File);
      Matrix3 xfm = Rotate.IsIdentity ? Matrix3.Identity : Matrix3.Rotation (Rotate);
      xfm *= Matrix3.Translation (Shift);
      return Mesh3.LoadFluxMesh (file) * xfm;
   }

   public readonly string File = string.Empty;
   public readonly Quaternion Rotate = Quaternion.Identity;
   public readonly Vector3 Shift = Vector3.Zero;
}
