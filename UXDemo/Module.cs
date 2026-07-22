using Nori;
namespace UXDemo;

public abstract class INodeClass {
   public abstract EKind Kind { get; }
   public abstract EFlags Flags { get; }

   public virtual void Init (ref Node node) {
      node.Kind = Kind; node.Flags = Flags;
   }

   public virtual void Draw (ref Node node) => throw new NotImplementedException ();
   public virtual void Release (ref Node node) { }
   public virtual Vec2S Measure (ref Node node) => throw new NotImplementedException ();
   public virtual Vec2S Measure (object data) => throw new NotImplementedException ();
}

/// <summary>
/// Node representing a simple rectangle
/// </summary>
/// The rectangle can have an optional border, and can also have rounded
/// corners
[Singleton]
partial class RectClass : INodeClass {
   public override EKind Kind => EKind.Rect;
   public override EFlags Flags => 0;

   public override void Draw (ref Node node) {
      var bgrd = node.BgrdColor; if (bgrd.IsTransparent) return;

      Lux.Color = node.BgrdColor;
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

   public void Init (ref Node node) {
      node.Kind = Kind; node.
   }

   public Vec2S Measure (ref Node node) {

   }

   public Vec2S Measure (object data) => Vec2S.Zero;
   public void Release (ref Node node) { }
}

/// <summary>
/// Represents a panel (like a Rect, but contains children)
/// </summary>
class PanelClass : INodeClass {
   public EKind Kind => EKind.Panel;

   public void Draw (ref Node node) => 
   public void Init (ref Node node) => throw new NotImplementedException ();
   public Vec2S Measure (ref Node node) => throw new NotImplementedException ();
   public Vec2S Measure (object data) => throw new NotImplementedException ();
   public void Release (ref Node node) => throw new NotImplementedException ();
}
