// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Undo.cs
// ║║║║╬║╔╣║ Implements basis of the Undo mechanism (UndoStep, UndoStack)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class UndoStep -----------------------------------------------------------------------------
/// <summary>UndoStep reprsents a basic action that can be 'undone' and 'redone'</summary>
/// Each change to your part/document/model should be represented as an UndoStep. To make a change
/// to the model (that should be undoable), encapsulate the change in a class derived from UndoStep,
/// and Push() it. This will put it on the current undo-stack, and immediately call Step(Redo)
/// on it. At that point, the UndoStep should go ahead and make the changes (such as adding/removing
/// entities, exploding blocks, adding layers etc). 
/// 
/// Later, if this needs to be undone (by the user choosing Edit/Undo), the Step(Undo) is called
/// and the code should reverse the changes. The 'Description' property exists to provide a 
/// human-readable name to be used in the UI (for the Undo/Redo menus).
public abstract class UndoStep {
   // Properties ---------------------------------------------------------------
   /// <summary>Override this to provide a description of the Step (for the Undo/Redo menu)</summary>
   public abstract string Description { get; }

   // Methods ------------------------------------------------------------------
   /// <summary>Override this to do the actual Undo or Redo of the action</summary>
   public abstract void Step (EUndoDir dir);

   /// <summary>Call this after constructing the UndoStep to push it on the current UndoStack</summary>
   /// If there is no current UndoStack, this performs the step immediately
   /// by calling it's Step(Redo) method. Likewise, when the step is pushed on the
   /// UndoStack, the Step(Redo) is called on the Action. 
   /// In other words, this initial invocation is a 'Do', not an Undo or Redo. 
   /// Sometimes, the Step you are pushing on the stack has already been (the changes corresponding
   /// to this step have already been applied). In that case, pass in false for the `redoNow`
   /// parameter to avoid an immediate call to Step(Redo). 
   public void Push (bool redoNow = true) {
      if (UndoStack.Current is { } stack) stack.Push (this, redoNow);
      else if (redoNow) Step (EUndoDir.Redo);
   }

   // Implementation -----------------------------------------------------------
   public override string ToString () => $"{GetType ().Name} '{Description}'";
}
#endregion

#region enum EUndo ---------------------------------------------------------------------------------
/// <summary>Direction to go - Undo or Redo</summary>
public enum EUndoDir { Undo, Redo };
#endregion

#region class UndoStack ----------------------------------------------------------------------------
/// <summary>An UndoStack represents a collection of undoable actions</summary>
/// A common pattern is to maintain one UndoStack per model/part/document, and to make it 
/// UndoStack.Current when that model/part is being edited.
public class UndoStack {
   // Constructors -------------------------------------------------------------
   /// <summary>Creates an undo-stack, given the maximum number of steps that can be undone</summary>
   /// If more than the maxDepth number of steps are pushed on this stack, the oldest ones
   /// are 'forgotten' and can no longer be undone
   public UndoStack (int maxDepth = 100) => mMaxDepth = maxDepth;

   // Properties ---------------------------------------------------------------
   /// <summary>The current UndoStack</summary>
   /// Typically, we will maintain an UndoStack per part, and when we start editing
   /// that part, we make that UndoStack 'current'.
   public static UndoStack? Current;

   /// <summary>The next step that can be undone (if any)</summary>
   /// Use this to update the 'Undo' menu with a suitable title, or to disable it
   /// if there is no NextUndo step
   public UndoStep? NextUndo => mSteps.SafeGet (mCursor);

   /// <summary>The next step that can be redone (if any)</summary>
   /// Use this to update the 'Redo' menu with a suitable title, or to disable it
   /// when this is null
   public UndoStep? NextRedo => mSteps.SafeGet (mCursor + 1);

   // Methods ------------------------------------------------------------------
   /// <summary>Used to club together multiple steps into a single undoable item</summary>
   /// To do this clubbing:
   /// - Push a ClubbedStep first on the stack (new ClubbedStep(...).Push ());
   /// - Push multiple actions on the stack
   /// - Finally, call ClubSteps(). This gathers all these actions and moves them
   ///   into the ClubbedStep, effectively making them a single action.
   ///   
   /// The design is like this so that individual bits of code don't need to worry
   /// about whether the step they are performing needs to be clubbed into another, or can be
   /// directly pushed on the undo stack. Instead a higher level caller can make that decision
   /// by simply placing a ClubbedStep() on the stack first, and finally calling ClubSteps 
   /// to collect these actions all into a single grouped step. 
   /// 
   /// Note that this design is recursive - an even higher level bit of code could club multiple
   /// ClubbedSteps() into a single one by having an outer ClubbedSteps wrapper around all of them. 
   public void ClubSteps () {
      int n = mSteps.Count - 1;
      Lib.Check (mCursor == n, "Coding error");
      try {
         for (int i = n; i >= 0; i--) {
            if (mSteps[i] is not ClubbedStep cs) continue;
            // Trivial cases: there are NO steps to club, or just one step to club.
            // In both cases, we can remove the ClubbedStep from the stack and return
            // immediately
            if (i == n || i == n - 1) { mSteps.RemoveAt (i); return; }
            // Otherwise, move all the steps subsequent to the marker into the ClubbedStep
            for (int j = n; j > i; j--) { cs.Steps.Insert (0, mSteps[j]); mSteps.RemoveAt (j); }
            return;
         }
      } finally {
         mCursor = mSteps.Count - 1;
      }
   }

   /// <summary>Called to push an action on the UndoStack</summary>
   public void Push (UndoStep step, bool redoNow = true) {
      // Since we have an Undo stack, not an undo tree, throw away any undone
      // actions above the cursor - these can never be redone anymore, we are starting
      // a new history
      int max = Math.Min (mCursor + 1, mMaxDepth - 1);
      while (mSteps.Count > max) mSteps.RemoveLast ();
      mSteps.Add (step); mCursor = mSteps.Count - 1;
      if (redoNow) step.Step (EUndoDir.Redo);
   }

   /// <summary>Called to perform a Redo (if any steps are available)</summary>
   public bool Redo () {
      if (mCursor < mSteps.Count - 1) { mSteps[++mCursor].Step (EUndoDir.Redo); return true; }
      return false;
   }

   /// <summary>Called to perform an Undo (if any steps are available)</summary>
   public bool Undo () {
      if (mCursor >= 0) { mSteps[mCursor--].Step (EUndoDir.Undo); return true; }
      return false;
   }

   // Private data -------------------------------------------------------------
   // The list of actions (oldest action is at mSteps[0])
   readonly List<UndoStep> mSteps = [];
   // Maximum number of steps that can be stored on the UndoStack
   readonly int mMaxDepth;
   // Points to the next action to undo (if there is one item that has just
   // been pushed on, mCursor will be 0)
   int mCursor = -1;
}
#endregion

#region class ClubbedStep --------------------------------------------------------------------------
/// <summary>Helper used to club multiple UndoStep into a single grouped action</summary>
public class ClubbedStep : UndoStep {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a ClubbedStep given a description for the complete grouped action</summary>
   /// At this point, the list of sub-steps within this ClubbedStep is not yet set. They
   /// will get pushed into the stack on top of this, and will all finally be gathered and
   /// placed into the Steps collection by UndoStack.ClubSteps().
   public ClubbedStep (string description) => mDescription = description;

   // Properties ---------------------------------------------------------------
   /// <summary>Description of the ClubbedStep</summary>
   public override string Description => mDescription;
   readonly string mDescription;

   /// <summary>List of steps within this ClubbedStep</summary>
   internal readonly List<UndoStep> Steps = [];

   // Methods ------------------------------------------------------------------
   /// <summary>Undo/Redo is implemented by calling the underlying steps to perform their Undo/Redo</summary>
   /// When we are doing a Redo, the Steps are walked in the forward order, while during an
   /// Undo, they are walked through in reverse order. 
   public override void Step (EUndoDir dir) {
      if (dir == EUndoDir.Undo) {
         for (int i = Steps.Count - 1; i >= 0; i--) Steps[i].Step (dir);
      } else 
         Steps.ForEach (a => a.Step (dir));      
   }
}
#endregion
