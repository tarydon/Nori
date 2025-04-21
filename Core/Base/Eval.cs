// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Eval.cs
// ║║║║╬║╔╣║ A simple expression evaluator
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using System.Buffers;

#region class EvalException ------------------------------------------------------------------------
file class EvalException (string message) : Exception (message) { }
#endregion

#region class Eval ---------------------------------------------------------------------------------
public class Eval {
   // Methods ------------------------------------------------------------------
   public bool TryEvaluate (string expr, out double res) {
      try {
         res = Evaluate (expr);
         return true;
      } catch (EvalException) {
         res = double.NaN;
         return false;
      } catch {
         throw;
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
      if (mOperators.Count > 0) throw new EvalException ("Too many operators");
      if (mOperands.Count > 1) throw new EvalException ("Too many operands");
      if (mBasePrecedence != 0) throw new EvalException ("Mismatched parenthesis");
      return mOperands.Pop ();
   }

   /// <summary>Processes the given token, and updates operator/operand stacks accordingly</summary>
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
         Operator op = negOperation ? new (EOperation.Neg, mBasePrecedence) : new (sOperationMap[tokenizer.GetLiteral ().ToString ()], mBasePrecedence);
         while (mOperators.Count > 0 && op.Precedence <= mOperators.Peek ().Precedence)
            ApplyOperator (mOperators.Pop ());
         mOperators.Push (op);
      }
   }

   /// <summary>Executes the given operator and updates the operand stack</summary>
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

      static double R2D (double f) => f * 180 / Math.PI;
      static double D2R (double f) => f * Math.PI / 180;
   }

   static Dictionary<string, EOperation> sOperationMap = new Dictionary<string, EOperation> () {
      ["+"] = EOperation.Add, ["-"] = EOperation.Sub, ["*"] = EOperation.Mul, ["/"] = EOperation.Div,
      ["sin"] = EOperation.Sin, ["cos"] = EOperation.Cos, ["tan"] = EOperation.Tan,
      ["asin"] = EOperation.Asin, ["acos"] = EOperation.Acos, ["atan"] = EOperation.Atan, ["atan2"] = EOperation.Atan2,
      ["log"] = EOperation.Log, ["exp"] = EOperation.Exp, ["sqrt"] = EOperation.Sqrt, ["sqr"] = EOperation.Sqr, ["abs"] = EOperation.Abs,
   };

   // Nested -------------------------------------------------------------------
   /// <summary>Specifies all the currently supported operations</summary>
   public enum EOperation {
      Add, Sub, Mul, Div, Neg,
      Sin, Asin, Cos, Acos, Tan, Atan, Atan2,
      Exp, Log, Sqrt, Sqr, Abs,
   }

   /// <summary>Operator info pushed onto operator stack, or applied to modify operand stack</summary>
   readonly struct Operator {
      public Operator (EOperation op, int basePrecedence = 0)
         => (Op, Precedence, COperands) = (op, basePrecedence + sPrecedence[(int)op], sCOperands[(int)op]);
      public EOperation Op { get; }
      public int Precedence { get; }
      public int COperands { get; }

      /// <summary>Specifies precedence of each operation</summary>
      static int[] sPrecedence = [
         1, 1, 2, 2, 4,
         3, 3, 3, 3, 3, 3, 3,
         3, 3, 3, 3, 3
      ];
      /// <summary>Specifies number of operands for each operation</summary>
      static int[] sCOperands = [
         2, 2, 2, 2, 1,
         1, 1, 1, 1, 1, 1, 2,
         1, 1, 1, 1, 1,
      ];
   }

   /// <summary>Raw tokenizer (works with lowercased expressions)</summary>
   class Tokenizer (string expr) {
      /// <summary>Raw token elements the tokenizer recognizes</summary>
      public enum EToken { Error, End, Punctuation, Numeric, Identifier }

      /// <summary>Gets the next token (or returns EToken.End)</summary>
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

      /// <summary>Gets current token's text span</summary>
      public ReadOnlySpan<char> GetLiteral () => mExpr.AsSpan (mTokenStart, mN - mTokenStart);
      /// <summary>Gets current token's text span, parsed as a double value</summary>
      public double GetF () {
         if (double.TryParse (GetLiteral (), out double res)) return res;
         throw new EvalException ($"Invalid numeric input : {GetLiteral ()}");
      }
      /// <summary>Gets current token's text span's first character</summary>
      public char CurrentChar => mExpr[mTokenStart];

      // Implementation -----------------------------------------------------------
      /// <summary>Captures the numeric token's text span range</summary>
      EToken Numeric () {
         mTokenStart = mN - 1;
         ReadOnlySpan<char> chars = mExpr;
         while (mN < chars.Length) {
            char ch = chars[mN++];
            if (ch is not (>= '0' and <= '9') and not '.') { mN--; break; }
         }
         return EToken.Numeric;
      }

      /// <summary>Captures the identifier token's text span range</summary>
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

   // Private ------------------------------------------------------------------
   /// <summary>Evaluator's operand stack</summary>
   readonly Stack<double> mOperands = new ();
   /// <summary>Evaluator's operator stack</summary>
   readonly Stack<Operator> mOperators = new ();
   /// <summary>Current base precedence level</summary>
   int mBasePrecedence = 0;
}
#endregion

