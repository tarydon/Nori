// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Class.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using static UXNode;
using static UXClass.EContainer;

/// <summary>There is one of these for each class of node we are going to create</summary>
public class UXClass (EKind kind, EFlags flags, UXClass.EContainer con, string argTypes, string? varType, int boundArg) {
   // Properties ---------------------------------------------------------------
   /// <summary>Argument types</summary>
   public readonly string ArgTypes = argTypes;
   /// <summary>The default flags value for nodes of this class</summary>
   public readonly EFlags Flags = flags;
   /// <summary>The kind of node this class represents</summary>
   public readonly EKind Kind = kind;

   /// <summary>Number of needed parameters</summary>
   public readonly int NeedParams = argTypes.TakeWhile (char.IsUpper).Count ();
   /// <summary>Number of optional parameters</summary>
   public int OptParams => ArgTypes.Length - NeedParams;
   /// <summary>Type of bound variable (for data controls like Checkbox / TextBox / Slider etc)</summary>
   public readonly string? VarType = varType;
   /// <summary>Is this a container (Maybe means it might be a container sometimes)</summary>
   public readonly EContainer CCode = con;
   /// <summary>Which of the arguments is 'bound' (for data controls</summary>
   public readonly int BoundArg = boundArg;

   /// <summary>Number of nodes used by this element</summary>
   public int CFragments { get; protected set; } = 1;

   /// <summary>Is this an inert element?</summary>
   public bool Inert => (Flags & EFlags.Inert) != 0;
   /// <summary>
   /// Does this need a 'key' to index into a memo?
   /// </summary>
   public bool NeedsKey => (Flags & EFlags.HasMemo) != 0;

   // Methods ------------------------------------------------------------------
   /// <summary>Called to draw a node</summary>
   /// The generic draw method here draws the bgrd/border of the node. It handles
   /// attributes like the border color, bgrd color, border with, corner radius etc.
   /// It can be used as a starting point for a number of different controls by chaining to 
   /// this implementatoin before doing custom draw, or it can be overridden completely
   public virtual void Draw (ref UXNode node) {
      var bgrd = node.BgrdColor; if (bgrd.IsTransparent) return;

      (Lux.Color, Lux.ZLevel) = (node.BgrdColor, node.ZLevel);
      var (radius, border, rect) = (node.CornerRadius, node.BorderWidth, node.Rect);
      if (node.HasShadow) {
         Lux.UIRect (rect.Center, rect.Size, radius, border, node.BgrdColor, node.BorderColor);
      } else if (radius > 0 && border > 0) {
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

   /// <summary>Iniitialize a node of this type when it is created</summary>
   public virtual void Init (ref UXNode node) {
      node.Kind = Kind; node.Flags = Flags;
   }

   /// <summary>Called to measure this node - sets X.DV and Y.DV only</summary>
   /// The default implementation just sets these to the Min value of each axis.
   /// Note that this does not set the position - just the width
   public virtual void Measure (ref UXNode node) {
      node.X.DV = node.X.Min; node.Y.DV = node.Y.Min;
   }

   /// <summary>Called to 'wrap' the contents of this node</summary>
   /// This is called only for nodes with the WRAP bit set in the flags (for example, 
   /// multi-line text, wrap-list-boxes etc)
   public virtual void Wrap (ref UXNode node) => throw new NotImplementedException ();

   // Nested types -------------------------------------------------------------
   /// <summary>Is this a container</summary>
   public enum EContainer { No, Yes, Maybe };

   // Implementation -----------------------------------------------------------
   internal static void RegisterAll () {
      UXEngine.RegisterClass (new RootClass ());
      UXEngine.RegisterClass (new PanelClass ());
      UXEngine.RegisterClass (new PopupClass ());
      UXEngine.RegisterClass (new VScrollClass ());
      UXEngine.RegisterClass (new TopMenuClass ());
      UXEngine.RegisterClass (new MenuClass ());
      UXEngine.RegisterClass (new SeparatorClass ());
      UXEngine.RegisterClass (new RadioButtonClass ());
      UXEngine.RegisterClass (new CheckboxClass ());
      UXEngine.RegisterClass (new LabelClass ());
      UXEngine.RegisterClass (new TextClass ());
      UXEngine.RegisterClass (new FillerClass ());
   }
}

public class CheckboxClass : UXClass {
   public CheckboxClass () : base (EKind.Checkbox, 0, No, "SB", "bool", 1) { }
}

public class FillerClass : UXClass {
   public FillerClass () : base (EKind.Filler, 0, No, "", null, -1) { }
}

public class LabelClass : UXClass {
   public LabelClass () : base (EKind.Label, 0, No, "", null, -1) { }
}

public class MenuClass : UXClass {
   public MenuClass () : base (EKind.Menu, EFlags.Horizontal | EFlags.HasMemo | EFlags.MayHavePopups, Maybe, "Sse", null, -1) => CFragments = 4;
}

public class PanelClass : UXClass {
   public PanelClass () : base (EKind.Panel, 0, Yes, "", null, -1) { }
}

public class PopupClass : UXClass {
   public PopupClass () : base (EKind.Popup, EFlags.Popup | EFlags.Shadow, Yes, "", null, -1) { }
}

public class RadioButtonClass : UXClass {
   public RadioButtonClass () : base (EKind.RadioButton, 0, No, "SB", "bool", 1) { }
}

public class RootClass : UXClass {
   public RootClass () : base (EKind.Root, 0, Yes, "", null, -1) { }
}

public class SeparatorClass : UXClass {
   public SeparatorClass () : base (EKind.Separator, EFlags.Inert, No, "", null, -1) { }
}

public class TopMenuClass : UXClass {
   public TopMenuClass () : base (EKind.TopMenu, EFlags.Horizontal, Yes, "", null, -1) { }
}

public class TextClass : UXClass {
   public TextClass () : base (EKind.Text, 0, No, "S", null, -1) { }

   public override void Measure (ref UXNode node) {
      TypeFace tf = UXEngine.Typefaces[node.FontId];
      RectS r = tf.Measure (node.Text ?? "");
      ref Axis x = ref node.X, y = ref node.Y;
      node.TextOffset = new (-r.Left + x.PadStart, -r.Top + x.PadStart);
      x.DV = (short)(r.Width + x.TotalPad);
      y.DV = (short)(r.Height + y.TotalPad);
   }

   public override void Draw (ref UXNode node) {
      (Lux.Color, Lux.ZLevel) = (node.FgrdColor, node.ZLevel + 1);
      Lux.TypeFace = UXEngine.Typefaces[node.FontId];
      Lux.Text (node.Text, new (node.X.V0 + node.TextOffset.X, node.Y.V0 + node.TextOffset.Y));
   }
}

public class VScrollClass : UXClass {
   public VScrollClass () : base (EKind.VScroll, EFlags.Scrollable, Yes, "", null, -1) { }
   const int WIDTH = 20, MARGIN = 2;

   public override void Draw (ref UXNode node) {
      ref Memo memo = ref node.GetMemo ();
      double ratio = (double)node.Y.DV / Math.Max (1, memo.ChildSize);
      int availHeight = node.Y.DV - 2 * MARGIN, thumbWidth = WIDTH - 2 * MARGIN;
      int thumbHeight = (int)Math.Max (ratio * availHeight, thumbWidth);
      double position = (double)memo.ScrollPos / memo.MaxScrollPos;

      int left = node.X.V0 + node.X.DV - WIDTH + MARGIN, top = (int)(position * (availHeight - thumbHeight) + 0.5) + node.Y.V0;
      int right = left + thumbWidth, bottom = top + thumbHeight;
      Lux.Color = node.FgrdColor;
      Lux.RRect (new RectS (left, top, right, bottom), 6);
   }
}

/*

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
         ref AxisDef x = ref node.X, y = ref node.Y;
         x.DV = (short)(mFace.MeasureWidth (node.Text ?? "", Lux.PanelSize.X) + x.TotalPad);
         RectS r = mFace.Measure ("M");
         node.TextOffset = new (-r.Left + x.PadStart, -r.Top + x.PadStart);
         y.DV = (short)(r.Height + y.TotalPad);
         x.Min = 50;
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

public class ListboxClass : NodeClass {
   public override EKind Kind => EKind.Listbox;
   public override EFlags Flags => 0;

   public override void Measure (ref Node node) {
      var data = (Data)node.GetMemo ().Data;
      var face = UXSystem.Typefaces[node.FontId];
      data.DYLine = face.LineHeight; 
      node.Y.DV = node.Y.Min = (short)(data.DYLine * data.Items.Count);
      RectS r = face.Measure ("M");
      node.TextOffset = new (-r.Left, -r.Top + face.Leading / 2);
   }

   public override void Draw (ref Node node) {
      var data = (Data)node.GetMemo ().Data;
      var items = data.Items;

      ref AxisDef x = ref node.X, y = ref node.Y;
      RectS rList = node.GetGrandparent ().Rect;
      ushort nClip = Lux.NClipRect; Lux.ClipRect = rList;
      Lux.TypeFace = UXSystem.Typefaces[node.FontId];
      for (int i = 0; i < items.Count; i++) {
         int yTop = y.V0 + i * data.DYLine; if (yTop > rList.Bottom) continue;
         int yBot = yTop + data.DYLine; if (yBot < rList.Top) continue;
         RectS rItem = new (x.V0, yTop, x.V0 + x.DV, yBot);
         bool iHover = rItem.Contains (UXSystem.MousePos), iSel = i == data.Selected;
         if (iHover || iSel) {
            Lux.ZLevel = node.ZLevel + 1;
            Lux.Color = iSel ? LISTBOX_Bgrd_S : LISTBOX_Bgrd_H; Lux.Rect (rItem);
         }
         Lux.ZLevel = node.ZLevel + 2;
         Lux.Color = iSel ? LISTBOX_Text_S : (iHover ? LISTBOX_Text_H : LISTBOX_Text);
         string s = items[i].ToString () ?? "";
         Lux.Text (s, new (rItem.Left + node.TextOffset.X + 5, rItem.Top + node.TextOffset.Y));
      }
      Lux.NClipRect = nClip;
   }

   internal class Data (IReadOnlyList<object> items, int selected) {
      public IReadOnlyList<object> Items = items;
      public int Selected = selected;
      public int DYLine; 
   }
}

public class CListBoxClass : NodeClass {
   public override EKind Kind => EKind.CListBox;
   public override EFlags Flags => EFlags.Wrap;

   public override void Wrap (ref Node node) {
      var data = (Data)node.GetMemo ().Data;
      var clist = data.CList;
      if (data.YTotal == 0 || clist.NeedsRemeasure) {
         data.Rects.Clear ();
         int xAvailable = node.X.DV, yTop = 0; 
         for (int i = 0, max = clist.Count; i < max; i++) {
            int yNeeded = clist.MeasureY (i, xAvailable);
            data.Rects.Add (new RectS (0, yTop, xAvailable, yTop + yNeeded));
            yTop += yNeeded + node.ChildGap;
         }
         data.YTotal = yTop - node.ChildGap;
      }
      node.Y.Min = node.Y.DV = (short)data.YTotal;
   }

   public override void Draw (ref Node node) {
      var data = (Data)node.GetMemo ().Data;
      Vec2S shift = new (node.X.V0, node.Y.V0);
      for (int i = 0; i < data.CList.Count; i++) {
         RectS r = data.Rects[i] + shift;
         Lux.ZLevel = node.ZLevel;
         data.CList.Draw (i, r);
      }
   }

   internal class Data (ICustomList clist) {
      public ICustomList CList = clist;
      public int YTotal;
      public List<RectS> Rects = [];
   }
}

class WrapListClass : NodeClass {
   public override EKind Kind => EKind.WrapList;
   public override EFlags Flags => EFlags.Wrap;

   public override void Wrap (ref Node node) {
      var data = (Data)node.GetMemo ().Data;
      var clist = data.CList;
      if (data.YTotal == 0 || clist.NeedsRemeasure || mXWidth != node.X.DV) {
         data.Rects.Clear ();
         mXWidth = node.X.DV;
         int xLeft = 0, yTop = 0, dyMax = 0, iFirstInRow = 0;
         for (int i = 0, max = clist.Count; i < max; i++) {
            var (dx, dy) = clist.Measure (i);
            if (i > iFirstInRow && xLeft + dx > mXWidth) {
               // If we can't accomodate the next item in this row, go to the 
               // next row. 
               yTop += dyMax + node.ChildGap; dyMax = 0; xLeft = 0;
               iFirstInRow = i; 
            }
            data.Rects.Add (new (xLeft, yTop, xLeft + dx, yTop + dy));
            xLeft += dx + node.ChildGap; dyMax = Math.Max (dyMax, dy); 
         }
         data.YTotal = yTop + dyMax;
      }
      node.Y.Min = node.Y.DV = (short)data.YTotal;
   }
   // The width of the wrap-list - whenever this changes, we have to recompute all the
   // Rects
   int mXWidth;

   public override void Draw (ref Node node) {
      var data = (Data)node.GetMemo ().Data;
      RectS rList = node.GetGrandparent ().Rect;
      ushort nOldClip = Lux.NClipRect; Lux.ClipRect = rList;
      Vec2S shift = new (node.X.V0, node.Y.V0);
      for (int i = 0; i < data.CList.Count; i++) {
         RectS r = data.Rects[i] + shift;
         if (r.Top > rList.Bottom || r.Bottom < rList.Top) {
            data.CList.Dispose (i);
         } else {
            Lux.ZLevel = node.ZLevel;
            data.CList.Draw (i, r);
         }
      }
      Lux.NClipRect = nOldClip;
   }

   internal class Data (ICustomList clist) {
      public readonly ICustomList CList = clist;
      public int YTotal;
      public List<RectS> Rects = [];
   }
}
*/
