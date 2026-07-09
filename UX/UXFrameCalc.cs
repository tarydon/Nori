using Nori;
using static System.Math;
using static Nori.UX.UXNode.ESizeMode;
using static Nori.UX.UXNode.EChildAlignX;
using static Nori.UX.UXNode.EChildAlignY;
using EC = Nori.UX.UXNode.ECorner;
namespace Nori.UX;

public static partial class UXFrame {
   // Implementation -----------------------------------------------------------
   // Grows the children of a node p (and recurses down the tree)
   static void GrowChildElements (int p) {
      // TODO: Use GetRemainingSpace for this?
      ref UXNode node = ref mNodes[p];
      node.GetChildren (mTmp, true);
      int childGaps = (node.ChildGap * Max (node.ChildCount - 1, 0));
      int dxSpace = node.DX - node.Padding.Left - node.Padding.Right;
      int dySpace = node.DY - node.Padding.Top - node.Padding.Bottom;
      if (node.Horizontal) {
         dxSpace -= childGaps;
         for (int i = mTmp.Count - 1; i >= 0; i--) {
            ref UXNode child = ref mNodes[mTmp[i]];
            dxSpace -= child.DX;
            if (child.Height.Mode == Grow) child.DY += (dySpace - child.DY);
            if (child.Width.Mode != Grow) mTmp.RemoveAt (i);
         }
         if (mTmp.Count == 0) goto Done;

         while (dxSpace > 0) {
            int smallest = short.MaxValue, secondSmallest = smallest, widthToAdd = dxSpace;
            foreach (var c in mTmp) {
               ref UXNode child = ref mNodes[c];
               if (child.DX < smallest) { secondSmallest = smallest; smallest = child.DX; }
               if (child.DX > smallest) { secondSmallest = Min (secondSmallest, child.DX); widthToAdd = secondSmallest - smallest; }
            }
            widthToAdd = Max (Min (widthToAdd, dxSpace / mTmp.Count), 1);

            foreach (var c in mTmp) {
               ref UXNode child = ref mNodes[c];
               if (child.DX != smallest) continue;
               child.DX += widthToAdd;
               dxSpace -= widthToAdd; if (dxSpace <= 0) break;
            }
         }
      } else {
         dySpace -= childGaps;
         for (int i = mTmp.Count - 1; i >= 0; i--) {
            ref UXNode child = ref mNodes[mTmp[i]];
            dySpace -= child.DY;
            if (child.Width.Mode == Grow) child.DX += (dxSpace - child.DX);
            if (child.Height.Mode != Grow) mTmp.RemoveAt (i);
         }
         if (mTmp.Count == 0) goto Done;

         while (dySpace > 0) {
            int smallest = short.MaxValue, secondSmallest = smallest, heightToAdd = dySpace;
            foreach (var c in mTmp) {
               ref UXNode child = ref mNodes[c];
               if (child.DY < smallest) { secondSmallest = smallest; smallest = child.DY; }
               if (child.DY > smallest) { secondSmallest = Min (secondSmallest, child.DY); heightToAdd = secondSmallest - smallest; }
            }
            heightToAdd = Max (Min (heightToAdd, dySpace / mTmp.Count), 1);

            foreach (var c in mTmp) {
               ref UXNode child = ref mNodes[c];
               if (child.DY != smallest) continue;
               child.DY += heightToAdd;
               dySpace -= heightToAdd; if (dySpace <= 0) break;
            }
         }
      }

      Done:
      for (var c = node.FirstChild; c != 0; c = mNodes[c].Next)
         GrowChildElements (c);
   }

   static void PositionChildren (int p) {
      ref UXNode parent = ref mNodes[p];
      var (dxSpace, dySpace) = GetRemainingSpace (p);
      parent.GetChildren (mTmp, false);
      if (parent.Horizontal) {
         int left = parent.Padding.Left;
         left += parent.ChildAlignX switch { Center => dxSpace / 2, Right => dxSpace, _ => 0 };
         foreach (var c in mTmp) {
            ref UXNode child = ref mNodes[c];
            if (child.Floating) { PositionFloat (ref parent, ref child); continue; }
            child.X = (parent.X + left);
            left += (child.DX + parent.ChildGap);
            child.Y = (parent.Y + parent.Padding.Top);
            switch (parent.ChildAlignY) {
               case Middle: child.Y += ((dySpace - child.DY) / 2); break;
               case Bottom: child.Y += (dySpace - child.DY); break;
            }
         }
      } else {
         int top = parent.Padding.Top;
         top += parent.ChildAlignY switch { Middle => dySpace / 2, Bottom => dySpace, _ => 0 };
         foreach (var c in mTmp) {
            ref UXNode child = ref mNodes[c];
            if (child.Floating) { PositionFloat (ref parent, ref child); continue; }
            child.Y = (parent.Y + top);
            top += (child.DY + parent.ChildGap);
            child.X = (parent.X + parent.Padding.Left);
            switch (parent.ChildAlignX) {
               case Center: child.X += ((dxSpace - child.DX) / 2); break;
               case Right: child.X += (dxSpace - child.DX); break;
            }
         }
      }

      for (int c = parent.FirstChild; c != 0; c = mNodes[c].Next)
         PositionChildren (c);
   }

   // Helpers ------------------------------------------------------------------
   // Given a parent node, returns the amount of extra space left after accounting for
   // the padding. Along the layout direction, we also subtract the sizes of the children
   // and the gaps between children. 
   static (int, int) GetRemainingSpace (int p) {
      ref UXNode node = ref mNodes[p];
      node.GetChildren (mTmp, true);
      int childGaps = node.ChildGap * Max (node.ChildCount - 1, 0);
      int dxSpace = node.DX - node.Padding.Horizontal;
      int dySpace = node.DY - node.Padding.Vertical;
      if (node.Horizontal) {
         dxSpace -= childGaps;
         foreach (var c in mTmp) dxSpace -= mNodes[c].DX;
      } else {
         dySpace -= childGaps;
         foreach (var c in mTmp) dySpace -= mNodes[c].DY;
      }
      return (dxSpace, dySpace);
   }
   static List<int> mTmp = [];

   static void PositionFloat (ref UXNode parent, ref UXNode child) {
      child.X = child.ParentCorner switch {
         EC.Left or EC.LeftTop or EC.LeftBottom => parent.X,
         EC.Right or EC.RightTop or EC.RightBottom => parent.X + parent.DX,
         _ => parent.X + parent.DX / 2,
      };
      child.Y = child.ParentCorner switch {
         EC.Top or EC.LeftTop or EC.RightTop => parent.Y,
         EC.Bottom or EC.LeftBottom or EC.RightBottom => parent.Y + parent.DY,
         _ => parent.Y + parent.DY / 2,
      };
   }
}
