// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Classes.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace UXDemo;

/// <summary>Base class for different 'node-classes'</summary>
public abstract class NodeClass {
   public abstract EKind Kind { get; }
   public abstract EFlags Flags { get; }

   public virtual void Init (ref Node node) {
      node.Kind = Kind; node.Flags = Flags;
   }

   public virtual void Measure (ref Node node) { 
      node.X.DV = node.X.Min; node.Y.DV = node.Y.Min;
      if (node.X.Max == 0) node.X.Max = short.MaxValue;
      if (node.Y.Max == 0) node.Y.Max = short.MaxValue;
   }

   public virtual void Draw (ref Node node) 
      => throw new NotImplementedException ($"Implement {this.GetType ().Name}.Draw");
   public virtual Vec2S Measure (object data) => throw new NotImplementedException ();
   public virtual void Release (ref Node node) { }
}

/// <summary>Node representing a simple rectangle</summary>"
/// The rectangle can have an optional border, and can also have rounded
/// corners
public class RectClass : NodeClass {
   public override EKind Kind => EKind.Rect;
   public override EFlags Flags => 0;

   public override void Draw (ref Node node) {
      var bgrd = node.BgrdColor; if (bgrd.IsTransparent) return;

      (Lux.Color, Lux.ZLevel) = (node.BgrdColor, node.ZLevel);
      var (radius, border, rect) = (node.CornerRadius, node.BorderWidth, node.Rect);
      if (radius > 0 && border > 0) {
         Lux.BorderColor = node.BorderColor;
         Lux.RRectBorder (rect, radius, border);
      } else if (border > 0) {
         Lux.BorderColor = node.BorderColor;
         Lux.RectBorder (rect, border);
      } else if (radius > 0) {
         Lux.RRect (rect, radius);
      } else
         Lux.Rect (rect);
   }
}

/// <summary>Node representing a basic panel</summary>
/// A panel is just a rectangle (with possibly a border and radius), but it can 
/// house children
public class PanelClass : RectClass {
   public override EKind Kind => EKind.Panel;
   public override EFlags Flags => EFlags.HasChildren;
}

public class RootClass : PanelClass {
   public override EKind Kind => EKind.Root;
}

public class TextClass : NodeClass {
   public override EKind Kind => EKind.Text;
   public override EFlags Flags => 0;

   public override void Measure (ref Node node) {
      TypeFace tf = UXSystem.Typefaces[node.FontId];
      RectS r = tf.Measure (node.Text ?? "", true);
      ref AxisDef x = ref node.X, y = ref node.Y;
      node.TextOffset = new (-r.Left + x.PadStart, -r.Top + x.PadStart);
      x.DV = (short)(r.Width + x.TotalPad);
      y.DV = (short)(r.Height + y.TotalPad);
   }

   public override void Draw (ref Node node) {
      (Lux.Color, Lux.ZLevel) = (node.FgrdColor, node.ZLevel + 1);
      Lux.TypeFace = UXSystem.Typefaces[node.FontId];
      Lux.Text (node.Text, new (node.X.V0 + node.TextOffset.X, node.Y.V0 + node.TextOffset.Y));
   }
}
