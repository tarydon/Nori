// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Classes.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.CodeDom.Compiler;
using System.Reflection.Metadata.Ecma335;
using JetBrains.Annotations;
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
   }

   public virtual void Draw (ref Node node) 
      => throw new NotImplementedException ($"Implement {this.GetType ().Name}.Draw");
   public virtual Vec2S Measure (object data) => throw new NotImplementedException ();
   public virtual void Release (ref Node node) { }
   public virtual void Wrap (ref Node node) => throw new NotImplementedException (); 
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

public class BlockClass : RectClass {
   public override EKind Kind => EKind.Block;
}

public class RootClass : PanelClass {
   public override EKind Kind => EKind.Root;
}

public class VScrollClass : NodeClass {
   public override EKind Kind => EKind.VScroll;
   public override EFlags Flags => EFlags.HasChildren | EFlags.Scrollable;
   public const int WIDTH = 20, MARGIN = 2;

   public override void Draw (ref Node node) {
      ref NodeMemo memo = ref node.GetMemo ();
      double ratio = (double)node.Y.DV / Math.Max (1, (int)memo.ChildSize);
      int availHeight = node.Y.DV - 2 * MARGIN, thumbWidth = WIDTH - 2 * MARGIN;
      int thumbHeight = (int)Math.Max (ratio * availHeight, thumbWidth);
      double position = (double)memo.ScrollPos / memo.MaxScrollPos;
      int left = node.X.V0 + node.X.DV - WIDTH + MARGIN, top = (int)(position * (availHeight - thumbHeight) + 0.5) + node.Y.V0;
      int right = left + thumbWidth, bottom = top + thumbHeight;
      Lux.Color = node.FgrdColor;
      Lux.RRect (new RectS (left, top, right, bottom), 6);
   }
}

public class TextClass : NodeClass {
   public override EKind Kind => EKind.Text;
   public override EFlags Flags => 0;

   public override void Measure (ref Node node) {
      TypeFace tf = UXSystem.Typefaces[node.FontId];
      RectS r = tf.Measure (node.Text ?? "");
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

public class MTextClass : NodeClass {
   public override EKind Kind => EKind.MText;
   public override EFlags Flags => EFlags.Wrap;

   public override void Measure (ref Node node) {
      ref NodeMemo memo = ref node.GetMemo ();
      if (memo.Data is not Data data) memo.Data = data = new Data (ref node);
      data.Measure (ref node);
   }

   public override void Draw (ref Node node) 
      => ((Data)node.GetMemo ().Data).Draw (ref node);

   public override void Wrap (ref Node node)
      => ((Data)node.GetMemo ().Data).Wrap (ref node);

   // Maintains data needed to wrap and render an MText
   class Data {
      public Data (ref Node node) 
         => (mText, mFace) = (node.Text ?? "", UXSystem.Typefaces[node.FontId]);
      readonly string mText;
      readonly TypeFace mFace;
      readonly List<(int Start, int End)> mSpans = [];
      short mDV;

      public void Measure (ref Node node) {
         var (start, text, max) = (-1, node.Text ?? "", 0);
         ref AxisDef x = ref node.X, y = ref node.Y;
         x.DV = (short)(mFace.MeasureWidth (text, Lux.PanelSize.X) + x.TotalPad);
         RectS r = mFace.Measure ("M");
         node.TextOffset = new (-r.Left + x.PadStart, -r.Top + x.PadStart);
         y.DV = (short)(r.Height + y.TotalPad);
         int words = text.Count (' ') + 1;
         x.Min = (short)(r.Width / words + x.TotalPad);
      }

      public void Wrap (ref Node node) {
         if (node.X.DV != mDV) { mDV = node.X.DV; mSpans.Clear (); }
         // The spans at which we are splitting the text are not yet computed, 
         // so compute them here.
         if (mSpans.Count == 0) 
            mFace.SplitSpans (mText, node.X.DV - node.X.TotalPad, mSpans);
         node.Y.DV = node.Y.Min = (short)(mSpans.Count * mFace.LineHeight + node.Y.TotalPad);
      }

      public void Draw (ref Node node) {
         (Lux.Color, Lux.ZLevel) = (node.FgrdColor, node.ZLevel + 1);
         Lux.TypeFace = mFace;
         int x = node.X.V0 + node.TextOffset.X + node.X.PadStart;
         int y = node.Y.V0 + node.TextOffset.Y + node.Y.PadStart;
         foreach (var (start, end) in mSpans) {
            Lux.Text (mText.AsSpan (start, end - start + 1), new (x, y));
            y += mFace.LineHeight;
         }
      }
   }
}
