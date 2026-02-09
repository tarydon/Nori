// ────── ╔╗
// ╔═╦╦═╦╦╬╣ VNodes.cs
// ║║║║╬║╔╣║ Some derived types of VNode (GroupVN, SimpleVN, TraceVN, XfmVN etc)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Threading;
namespace Nori;

#region class GroupVN ------------------------------------------------------------------------------
/// <summary>A GroupVN simply has multiple children</summary>
public class GroupVN : VNode {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a GroupVN given multiple child VNodes to hold on to</summary>
   public GroupVN (IEnumerable<VNode> children) => mChildren = [.. children];
   readonly List<VNode> mChildren;

   public void Add (VNode child) { mChildren.Add (child); ChildAdded (); }

   public void Remove (VNode child) { if (mChildren.Remove (child)) ChildRemoved (child); }

   // Overrides ----------------------------------------------------------------
   // Return the children
   public override VNode? GetChild (int n) => mChildren.SafeGet (n);
}
#endregion

#region class SimpleVN -----------------------------------------------------------------------------
/// <summary>A trivial VNode that just wraps around a drawing function</summary>
/// All the code (including setting of attributes) can be done in taht single function.
/// Normally, attributes should be set in SetAttributes(), and drawing should be done in
/// Draw(). However, if the attributes will never change in the future (as in this case),
/// then we can pack all of that into the draw function
public class SimpleVN (Action setattr, Action draw) : VNode (draw) {
   public SimpleVN (Action draw) : this (() => { }, draw) { }
   public override void SetAttributes () => setattr ();
   public override void Draw () => draw ();
}
#endregion

#region class Mesh3VN ------------------------------------------------------------------------------
/// <summary>VNode used to draw a Mesh3</summary>
public class Mesh3VN : VNode {
   // Constructor --------------------------------------------------------------
   public Mesh3VN (Mesh3 mesh) : base (mesh) => mMesh = mesh;
   readonly Mesh3 mMesh;

   // Properties ---------------------------------------------------------------
   /// <summary>Color for the Mesh (set to Color4.Nil to inherit parent color)</summary>
   public Color4 Color = Color4.Yellow;
   /// <summary>Shade mode for this mesh</summary>
   public EShadeMode Mode = EShadeMode.Phong;

   // Overrides ----------------------------------------------------------------
   public override void SetAttributes () => Lux.Color = Color;
   public override void Draw () => Lux.Mesh (mMesh, Mode);
}
#endregion

#region class TraceVN ------------------------------------------------------------------------------
/// <summary>Displays Trace text in the window</summary>
[Singleton]
public partial class TraceVN : VNode {
   TraceVN () => Streaming = true;

   // Properties ---------------------------------------------------------------
   /// <summary>Text color</summary>
   public static Color4 TextColor = Color4.Blue;

   public static int HoldTime = 7;

   // Methods ------------------------------------------------------------------
   /// <summary>Prints text to the TraceVN (this is static, since the class is a singleton)</summary>
   /// The text can contain \n separators to split it into multiple lines. The text
   /// disappears after a few seconds, and if more text is printed than will fit on the
   /// screen, the oldest text scrolls up out of the screen
   public static void Print (string s) => It.Add (s);

   /// <summary>Clears all the text from the display.</summary>
   public void Clear () {
      mLines.Clear ();
      Redraw ();
   }

   // Overrides ----------------------------------------------------------------
   // Draw the lines, starting from the top left corner of the screen
   public override void Draw () {
      mcLines = Math.Max (Lux.Viewport.Y / mDYLine - 2, 10);
      int y = Lux.Viewport.Y - mDYLine;
      for (int i = 0; i < mLines.Count; i++) {
         Lux.TextPx (mLines[i].Text, new (mDYLine / 2, y));
         y -= mDYLine;
      }
   }

   // Set up the text color, typeface and ZLevel (to be above all the other drawing)
   public override void SetAttributes () {
      if (mLines.Count > 0)
         (Lux.Color, Lux.TypeFace, Lux.ZLevel) = (TextColor, Face, 100);
   }

   // Implementation -----------------------------------------------------------
   // Internal routine called to add text to the trace. It splits text into multiple
   // lines based on the \n separator. If there are more lines of text than we can
   // display, the oldest lines are removed
   void Add (string s) {
      _ = Face;      // Reading this computes a good value for mDYLine (text height in pixels)
      foreach (var w in s.TrimEnd ('\n').Split ('\n'))
         mLines.Add ((DateTime.Now, w));
      while (mLines.Count > mcLines) mLines.RemoveAt (0);
      Redraw ();
   }

   // Called when we first print text to build the TypeFace. This also starts a timer
   // so we can remove printed text after a few seconds
   TypeFace Face {
      get {
         if (mFace == null) {
            mFace = new (Lib.ReadBytes ("nori:GL/Fonts/RobotoMono-Regular.ttf"), 16);
            mDYLine = mFace.LineHeight;
            mTimer = new () { Interval = TimeSpan.FromSeconds (1), IsEnabled = true };
            mTimer.Tick += OnTick;
         }
         return mFace;
      }
   }
   TypeFace? mFace;

   // Timer handler, removes text that is more than 7 seconds old
   void OnTick (object? s, EventArgs e) {
      int n = mLines.Count;
      while (mLines.Count > 0 && mLines[0].TS + TimeSpan.FromSeconds (HoldTime) < DateTime.Now) mLines.RemoveAt (0);
      if (n != mLines.Count) Redraw ();
   }

   // Private data -------------------------------------------------------------
   int mDYLine = 20;    // Height of each line in pixes
   int mcLines = 100;   // Number of lines that will fit on the screen
   DispatcherTimer? mTimer;
   readonly List<(DateTime TS, string Text)> mLines = [];
}
#endregion

#region class XfmVN --------------------------------------------------------------------------------
/// <summary>A VNode that just applies an Xfm to the subtree underneath</summary>
public class XfmVN : VNode {
   // Constructors -------------------------------------------------------------
   /// <summary>Make an XfmVn given an xfm and a child VNode to transform</summary>
   /// If you need to transform multiple things, that child VNode could be a
   /// GroupVN which has its own children
   public XfmVN (Matrix3 xfm, VNode child) => (mXfm, mChild) = (xfm, child);
   readonly VNode mChild;

   // Properties ---------------------------------------------------------------
   /// <summary>The Xfm to apply for this subtree (relative to the parent)</summary>
   public Matrix3 Xfm {
      get => mXfm;
      set { mXfm = value; OnChanged (EProp.Xfm); }
   }
   Matrix3 mXfm;

   // Overrides ----------------------------------------------------------------
   // The only attribute to set is the Xfm
   public override void SetAttributes () => Lux.Xfm = Xfm;
   // An XfmVn contains one child
   public override VNode? GetChild (int n) => n == 0 ? mChild : null;
}
#endregion
