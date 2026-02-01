// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Undo.cs
// ║║║║╬║╔╣║ Implements basis of the Undo mechanism (UndoStep, UndoStack)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class UndoStep -----------------------------------------------------------------------------
/// <summary>
/// UndoStep reprsents a basic action that can be 'undone' and 'redone'
/// </summary>
public abstract class UndoStep {
   // Properties ---------------------------------------------------------------
   /// <summary>
   /// Override this to provide a description of the Step (for the Undo/Redo menu)
   /// </summary>
   public abstract string Description { get; }

   // Methods ------------------------------------------------------------------
   /// <summary>
   /// Override this to do the actual Undo or Redo of the action
   /// </summary>
   public abstract void Step (EUndo dir);

   /// <summary>
   /// Call this after constructing the UndoStep to push it on the current UndoStack
   /// </summary>
   /// If there is no current UndoStack, this performs the step immediately
   /// by calling it's Step(Redo) method. Likewise, when the step is pushed on the
   /// UndoStack, the Step(Redo) is called on the Action. 
   /// In other words, this initial invocation is a 'Do', not an Undo or Redo. 
   public void Push () {
      if (UndoStack.Current is { } stack) stack.Push (this);
      else Step (EUndo.Redo);
   }

   // Implementation -----------------------------------------------------------
   public override string ToString () => $"{GetType ().Name} '{Description}'";
}
#endregion

#region enum EUndo ---------------------------------------------------------------------------------
/// <summary>
/// Direction to go - Undo or Redo
/// </summary>
public enum EUndo { Undo, Redo };
#endregion

#region class UndoStack ----------------------------------------------------------------------------
public class UndoStack {
   // Properties ---------------------------------------------------------------
   /// <summary>
   /// The current UndoStack 
   /// </summary>
   /// Typically, we will maintain an UndoStack per part, and when we start editing
   /// that part, we make that UndoStack 'current'.
   public static UndoStack? Current;

   /// <summary>
   /// The next step that can be undone (if any)
   /// </summary>
   /// Use this to update the 'Undo' menu with a suitable title, or to disable it
   /// if there is no NextUndo step
   public UndoStep? NextUndo => mSteps.SafeGet (mCursor);

   /// <summary>
   /// The next step that can be redone (if any)
   /// </summary>
   /// Use this to update the 'Redo' menu with a suitable title, or to disable it
   /// when this is null
   public UndoStep? NextRedo => mSteps.SafeGet (mCursor + 1);

   // Methods ------------------------------------------------------------------
   public void Push (UndoStep step, bool redoNow = true) {
      // Since we have an Undo stack, not an undo tree, throw away any undone
      // actions above the cursor - these can never be redone anymore, we are starting
      // a new history
      mSteps.RemoveRange (mCursor + 1, mSteps.Count - mCursor - 1);
      mSteps.Add (step); mCursor = mSteps.Count - 1;
      if (redoNow) step.Step (EUndo.Redo);
   }

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

   public void Undo () {
      if (mCursor >= 0) mSteps[mCursor--].Step (EUndo.Undo);
   }

   public void Redo () {
      if (mCursor < mSteps.Count - 1) mSteps[++mCursor].Step (EUndo.Redo);
   }

   // The list of actions (oldest action is at mSteps[0])
   List<UndoStep> mSteps = [];
   // Points to the next action to undo (if there is one item that has just
   // been pushed on, mCursor will be 0)
   int mCursor = -1;
}
#endregion

#region class ClubbedStep --------------------------------------------------------------------------
public class ClubbedStep : UndoStep {
   public ClubbedStep (string description) => mDescription = description;

   public override string Description => mDescription;

   public override void Step (EUndo dir) {
      if (dir == EUndo.Undo) {
         for (int i = Steps.Count - 1; i >= 0; i--) Steps[i].Step (dir);
      } else 
         Steps.ForEach (a => a.Step (dir));      
   }

   public readonly List<UndoStep> Steps = [];
   readonly string mDescription;
}
#endregion
