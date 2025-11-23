// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Eval.cs
// ║║║║╬║╔╣║ A simple expression evaluator
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using System.Buffers;

#region class Eval ---------------------------------------------------------------------------------
/// <summary>Eval implements a simple expression evaluator (for use in text-boxes etc)</summary>
public class Eval {
   // Methods ------------------------------------------------------------------
   public bool TryEvaluate (string expr, out double res) {
      try {
         res = Evaluate (expr);
         return true;
      } catch (EvalException) {
         res = double.NaN;
         return false;
      }
   }

   // Implementation -----------------------------------------------------------
   double Evaluate (string expr) {
      mOperands.Clear (); mOperators.Clear (); mBasePrecedence = 0; // Reset
      Tokenizer tokenizer = new (expr);
      bool minusIsSubtract = false; // Helps determine if '-' punctuation is subtraction or negation operation
      for (; ; ) {
         Tokenizer.EToken t = tokenizer.Next ();
         if (t == Tokenizer.EToken.End) break;
         if (t == Tokenizer.EToken.Error) throw new EvalException ("Invalid expression");
         Process (t, tokenizer, minusIsSubtract);
         minusIsSubtract = (t == Tokenizer.EToken.Punctuation && tokenizer.CurrentChar == ')') || t == Tokenizer.EToken.Numeric;
      }

      while (mOperators.Count > 0) ApplyOperator (mOperators.Pop ());
      if (mOperands.Count > 1) throw new EvalException ("Too many operands");
      if (mBasePrecedence != 0) throw new EvalException ("Mismatched parenthesis");
      return mOperands.Pop ();
   }

   // Processes the given token, and updates operator/operand stacks accordingly
   void Process (Tokenizer.EToken t, Tokenizer tokenizer, bool minusIsSubtract) {
      if (t == Tokenizer.EToken.Numeric) {
         mOperands.Push (tokenizer.GetF ());
         return;
      }
      if (t is Tokenizer.EToken.Punctuation or Tokenizer.EToken.Identifier) {
         char ch = tokenizer.CurrentChar;
         if (ch == '(') { mBasePrecedence += 10; return; }
         if (ch == ')') { mBasePrecedence -= 10; return; }
         bool negOperation = ch == '-' && !minusIsSubtract;
         Operator op = negOperation ? new (EOperation.Neg, mBasePrecedence) : new (sOperationMap[tokenizer.GetLiteral ()], mBasePrecedence);
         while (mOperators.Count > 0 && op.Precedence <= mOperators.Peek ().Precedence)
            ApplyOperator (mOperators.Pop ());
         mOperators.Push (op);
      }
   }

   // Executes the given operator and updates the operand stack
   void ApplyOperator (Operator op) {
      if (mOperands.Count < op.COperands) throw new EvalException ("Too few operands");
      double a = mOperands.Pop (), b = a;
      if (op.COperands == 2) a = mOperands.Pop ();
      double res = op.Op switch {
         EOperation.Add => a + b,
         EOperation.Sub => a - b,
         EOperation.Mul => a * b,
         EOperation.Div => a / b,
         EOperation.Neg => -a,
         EOperation.Sin => Math.Sin (D2R (a)),
         EOperation.Cos => Math.Cos (D2R (a)),
         EOperation.Tan => Math.Tan (D2R (a)),
         EOperation.Asin => R2D (Math.Asin (a)),
         EOperation.Acos => R2D (Math.Acos (a)),
         EOperation.Atan => R2D (Math.Atan (a)),
         EOperation.Atan2 => R2D (Math.Atan2 (a, b)),
         EOperation.Exp => Math.Exp (a),
         EOperation.Log => Math.Log10 (a),
         EOperation.Sqrt => Math.Sqrt (a),
         EOperation.Sqr => a * a,
         EOperation.Abs => Math.Abs (a),
         _ => throw new NotImplementedException ($"Unhandled operation {op.Op}")
      };
      mOperands.Push (res);

      // Helpers ...........................................
      static double R2D (double f) => f * 180 / Math.PI;
      static double D2R (double f) => f * Math.PI / 180;
   }

   static Dictionary<string, EOperation>.AlternateLookup<ReadOnlySpan<char>> sOperationMap = new Dictionary<string, EOperation> {
      ["+"] = EOperation.Add, ["-"] = EOperation.Sub, ["*"] = EOperation.Mul, ["/"] = EOperation.Div,
      ["sin"] = EOperation.Sin, ["cos"] = EOperation.Cos, ["tan"] = EOperation.Tan,
      ["asin"] = EOperation.Asin, ["acos"] = EOperation.Acos, ["atan"] = EOperation.Atan, ["atan2"] = EOperation.Atan2,
      ["log"] = EOperation.Log, ["exp"] = EOperation.Exp, ["sqrt"] = EOperation.Sqrt, ["sqr"] = EOperation.Sqr, ["abs"] = EOperation.Abs
   }.GetAlternateLookup<ReadOnlySpan<char>> ();

   // Nested -------------------------------------------------------------------
   // Specifies all the currently supported operations
   enum EOperation {
      Add, Sub, Mul, Div, Neg,
      Sin, Asin, Cos, Acos, Tan, Atan, Atan2,
      Exp, Log, Sqrt, Sqr, Abs
   }

   // Exception thrown during evaluation
   class EvalException (string message) : Exception (message);

   // Operator info pushed onto operator stack, or applied to modify operand stack
   readonly struct Operator {
      public Operator (EOperation op, int basePrecedence = 0)
         => (Op, Precedence, COperands) = (op, basePrecedence + sPrecedence[(int)op], sCOperands[(int)op]);
      public EOperation Op { get; }
      public int Precedence { get; }
      public int COperands { get; }

      // Specifies precedence of each operation
      static int[] sPrecedence = [
         1, 1, 2, 2, 4,
         3, 3, 3, 3, 3, 3, 3,
         3, 3, 3, 3, 3
      ];
      // Specifies number of operands for each operation
      static int[] sCOperands = [
         2, 2, 2, 2, 1,
         1, 1, 1, 1, 1, 1, 2,
         1, 1, 1, 1, 1
      ];
   }

   // Raw tokenizer (works with lowercased expressions)
   class Tokenizer (string expr) {
      // Raw token elements the tokenizer recognizes
      public enum EToken { Error, End, Punctuation, Numeric, Identifier }

      // Gets the next token (or returns EToken.End)
      public EToken Next () {
         ReadOnlySpan<char> chars = mExpr;
         while (mN < chars.Length) {
            char ch = chars[mN++];
            if (ch is ' ' or ',') continue;
            if (ch is >= '0' and <= '9') return Numeric ();
            if (ch is >= 'a' and <= 'z') return Identifier ();
            mTokenStart = mN - 1;
            return mPuncChars.Contains (ch) ? EToken.Punctuation : EToken.Error;
         }
         return EToken.End;
      }

      // Gets current token's text span
      public ReadOnlySpan<char> GetLiteral () => mExpr.AsSpan (mTokenStart, mN - mTokenStart);
      // Gets current token's text span, parsed as a double value
      public double GetF () {
         if (double.TryParse (GetLiteral (), out double res)) return res;
         throw new EvalException ($"Invalid numeric input : {GetLiteral ()}");
      }
      // Gets current token's text span's first character
      public char CurrentChar => mExpr[mTokenStart];

      // Implementation -----------------------------------------------------------
      // Captures the numeric token's text span range
      EToken Numeric () {
         mTokenStart = mN - 1;
         ReadOnlySpan<char> chars = mExpr;
         while (mN < chars.Length) {
            char ch = chars[mN++];
            if (ch is not (>= '0' and <= '9') and not '.') { mN--; break; }
         }
         return EToken.Numeric;
      }

      // Captures the identifier token's text span range
      EToken Identifier () {
         mTokenStart = mN - 1;
         ReadOnlySpan<char> chars = mExpr;
         while (mN < chars.Length) {
            char ch = chars[mN++];
            if (ch is not (>= 'a' and <= 'z') and not (>= '0' and <= '9')) { mN--; break; }
         }
         return EToken.Identifier;
      }

      readonly string mExpr = expr.ToLowerInvariant ();
      int mN, mTokenStart;
      static readonly SearchValues<char> mPuncChars = SearchValues.Create ("+-/*()");
   }

   // Private data -------------------------------------------------------------
   readonly Stack<double> mOperands = new ();      // Evaluator's operand stack
   readonly Stack<Operator> mOperators = new ();   // Evaluator's operator stack
   int mBasePrecedence;                            // Current base precedence level
}
#endregion

