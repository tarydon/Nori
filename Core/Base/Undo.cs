namespace Nori;

public abstract class UndoStep {
   public abstract string Description { get; }
   public abstract void Step (EUndo dir);

   public void Push () {
      if (UndoStack.Current is { } stack) stack.Push (this);
      else Step (EUndo.Redo);
   }
}

public enum EUndo { Undo, Redo };

public class UndoStack {
   public void Push (UndoStep step, bool redoNow = true) {
      // Since we have an Undo stack, not an undo tree, throw away any undone
      // actions above the cursor - these can never be redone anymore, we are starting
      // a new history
      mSteps.RemoveRange (mCursor + 1, mSteps.Count - mCursor - 1);
      mSteps.Add (step); mCursor = mSteps.Count - 1;
      if (redoNow) step.Step (EUndo.Redo);
   }

   public static UndoStack? Current;

   public UndoStep? NextUndo => mSteps.SafeGet (mCursor);
   public UndoStep? NextRedo => mSteps.SafeGet (mCursor + 1);

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
