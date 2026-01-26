// ────── ╔╗
// ╔═╦╦═╦╦╬╣ DwgAux.cs
// ║║║║╬║╔╣║ A few helper types used by Dwg2 (such as Block2, Layer2 etc)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Block2 -------------------------------------------------------------------------------
/// <summary>Represents a BLOCK definition as seen in a DXF file</summary>
public class Block2 {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a Block</summary>
   public Block2 (string name, Point2 p, IEnumerable<Ent2> ents)
      => (mName, mBase, mEnts) = (name, p, [.. ents]);
   Block2 () => (mName, mEnts) = ("", []);

   // Properties ---------------------------------------------------------------
   /// <summary>Set of entities in this block</summary>
   public IReadOnlyList<Ent2> Ents => mEnts;
   List<Ent2> mEnts;

   /// <summary>Base point of the block (this maps to the insertion point of the INSERT)</summary>
   public Point2 Base => mBase;
   Point2 mBase;

   /// <summary>Name of the block</summary>
   public string Name => mName;
   string mName;

   /// <summary>The VNode for this Block2 (if one has been created)</summary>
   public object? VNode { get => _vnode; set => _vnode = value; }
   object? _vnode;

   // Methods ------------------------------------------------------------------
   /// <summary>Returns the bound of this Block2</summary>
   public Bound2 GetBound () => new (mEnts.Select (a => a.Bound));

   public override string ToString () => $"Block:{mName}, {mEnts.Count} ents";
}
#endregion

#region enum E2Flags -------------------------------------------------------------------------------
/// <summary>Bitflags for Ent2</summary>
[Flags]
public enum E2Flags {
   Selected = 0x1, InBlock = 0x2, Closed = 0x4, Periodic = 0x8
}
#endregion

#region class Grid2 --------------------------------------------------------------------------------
/// <summary>Represents the Snap Grid settings for a drawing</summary>
public class Grid2 {
   Grid2 () { }
   public Grid2 (double pitch, int subdivs, bool visible, bool snap, Point2 origin, double rotation)
      => (Pitch, Subdivs, Visible, Snap, Origin, Rotation) = (pitch, subdivs, visible, snap, origin, rotation);

   public Grid2 (double pitch, int subdivs, bool visible) 
      : this (pitch, subdivs, visible, false, Point2.Zero, 0) { }

   // Properties ---------------------------------------------------------------
   /// <summary>Is the snap grid visible?</summary>
   public readonly bool Visible;
   /// <summary>Pitch between grid cells (in X and Y)</summary>
   public readonly double Pitch;
   /// <summary>Subdivisions of each grid cell</summary>
   public readonly int Subdivs;
   /// <summary>Are 'GRID' snaps turned on?</summary>
   public readonly bool Snap;

   /// <summary>The origin point of the grid (set by the SHIFT ORIGIN command)</summary>
   public readonly Point2 Origin;
   /// <summary>The rotation angle of the grid (set by the SHIFT ORIGIN command)</summary>
   public readonly double Rotation;

   // Methods ------------------------------------------------------------------
   /// <summary>Make a Grid2 with just the visibility changed</summary>
   public Grid2 WithVisible (bool visibility) => new (Pitch, Subdivs, visibility, Snap, Origin, Rotation);

   public static readonly Grid2 Default = new (10.0, 5, false, false, Point2.Zero, 0);
}
#endregion

#region class Style2 -------------------------------------------------------------------------------
/// <summary>Contains data about a text style</summary>
public class Style2 {
   // Constructors -------------------------------------------------------------
   Style2 () => Name = Font = "";
   public Style2 (string name, string font, double height, double xscale, double oblique) {
      (Name, Font, Height, XScale, Oblique) = (name, font, height, xscale, oblique);
      if (Font.EndsWith (".shx")) Font = Font[..^4];
   }

   public override string ToString () => $"Style2 {Name}({Font})";

   // Properties ---------------------------------------------------------------
   /// <summary>Fixed height of the text (if not set to 0)</summary>
   public readonly double Height;
   /// <summary>The name of the style</summary>
   public readonly string Name;
   /// <summary>The Font used for this</summary>
   public readonly string Font;
   /// <summary>Obliquing angle, in radians</summary>
   public readonly double Oblique;
   /// <summary>The width factor in X</summary>
   public readonly double XScale;

   /// <summary>A default placeholder style to use when a style is missing</summary>
   public static readonly Style2 Default = new ("STANDARD", "SIMPLEX", 0, 1, 0);

   static Style2? ByName (IReadOnlyList<object> stack, string name) {
      for (int i = stack.Count - 1; i >= 0; i--)
         if (stack[i] is Dwg2 dwg) return dwg.GetStyle (name);
      return null;
   }
}
#endregion

#region class Layer2 -------------------------------------------------------------------------------
/// <summary>Represents a layer in a drawing (used to assign color / linetype / visibility etc)</summary>
public class Layer2 {
   Layer2 () => mName = "";
   public Layer2 (string name, Color4 color, ELineType lineType)
      => (mName, mColor, mLinetype) = (name, color, lineType);

   /// <summary>Layer name</summary>
   public string Name => mName;
   readonly string mName;

   /// <summary>Layer color</summary>
   public Color4 Color => mColor;
   readonly Color4 mColor;

   /// <summary>Is this layer visible?</summary>
   public bool IsVisible { get => mVisible; set => mVisible = value; }
   bool mVisible;

   /// <summary>What is the linetype</summary>
   public ELineType Linetype => mLinetype;
   readonly ELineType mLinetype;

   static Layer2? ByName (IReadOnlyList<object> stack, string name) {
      for (int i = stack.Count - 1; i >= 0; i--)
         if (stack[i] is Dwg2 dwg) return dwg.Layers.FirstOrDefault (a => a.Name == name);
      return null;
   }
}
#endregion

#region struct TPolyPick ---------------------------------------------------------------------------
/// <summary>Data returned from Dwg.PickPoly call (the closest poly, closest node, seg etc)</summary>
public readonly struct TPolyPick (E2Poly ent, int seg, int node, Poly.ECornerOpFlags flags) {
   /// <summary>The E2Poly that is closest</summary>
   public readonly E2Poly Ent = ent;
   /// <summary>The Poly contained within that</summary>
   public Poly Poly => Ent.Poly;
   /// <summary>The closest seg on the poly</summary>
   public readonly short Seg = (short)seg;
   /// <summary>Which node (on that closest seg) is closer to the pick point</summary>
   /// Node = Seg or Node = Seg+1 always
   public readonly short Node = (short)node;
   /// <summary>Flags to perform some corner operations like fillet, corner-step etc</summary>
   /// These operations depend on the position of this pick point w.r.t the nearest node
   public readonly Poly.ECornerOpFlags Flags = flags;
}
#endregion
