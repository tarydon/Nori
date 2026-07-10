using System.Buffers;
using System.Text;

namespace Nori.UX;

public class InlayGen {
   public InlayGen (string file) {
      mText = File.ReadAllText (file) + "\u001A";
      mFile = Path.GetFileName (file);
   }
   string mFile;

   public string Generate () {
      AddL ("""
         using Nori;
         using System;
         using Nori.UX;
         using static Nori.UX.UXApi;
         namespace Nori.Inlay;

         class Inlay1 : InlayHub {
         static void Generate () {
         """);

      Token t = Expect (EToken.Element);
      try {
         OutElem (t);
      } catch (Exception e) {
         Console.ForegroundColor = ConsoleColor.Red;
         Console.WriteLine (e);
         Console.ResetColor ();
      }

      AddL ("""
         }
         }
         """);
      return DoIndent (mSB.ToString ());
   }

   // Implementation -----------------------------------------------------------
   void Add (string s) => mSB.Append (s);
   void AddL (string s) => mSB.AppendNL (s);
   void AddLineNo (Token t) { } // => AddL ($"#line ({t.Line},{t.Column}) - ({t.Line},{t.Column + t.Text.Length}) \"{mFile}\"");

   string DoIndent (string input) {
      StringBuilder sb = new ();
      int level = 0, indent = 3; 
      var S = input.Split ('\n').Select (a => a.Trim ()).ToList ();
      for (int i = 0; i < S.Count; i++) {
         if (S[i] is "}") level--;
         var tmp = new string (' ', level * indent) + S[i];
         if (S[i].EndsWith ('{')) level++;
         sb.AppendNL (tmp);
      }
      return sb.ToString ();
   }

   Token Expect (EToken e) {
      Token t = GetToken (); Lib.Check (t.E == e);
      return t; 
   }

   void Fatal (string s) {
      throw new Exception (s);
   }

   // Gathers text from the current location until the given finisher token.
   // The finisher token is consumed, but is not included in the returned string
   string GatherUntil (EToken e) {
      int start = N;
      for (; ; ) {
         Token t = GetToken ();
         if (t.E == e) {
            string s = mText[start..t.Start].Trim ();
            return s; 
         }
      }
   }

   char GetCH () {
      char ch = mText[N++];
      if (mFreshLine) { mLine++; mColumn = 0; mFreshLine = false; }
      mColumn++; mFreshLine = ch == '\n';
      return ch;
   }

   string GetExpr () {
      Lib.Check (TryGetExpr (out var s));
      return s;
   }

   Token GetToken () {
      int start;
      if (mPushedBack) { mPushedBack = false; return mPushbackToken; }
      for (; ; ) {
         char ch = GetCH ();
         if (sTokens.Contains (ch)) return new Token ((EToken)ch, mText, N - 1, N, mLine, mColumn);
         switch (ch) {
            case ' ' or '\t' or '\r': continue;
            case '"':
               start = N - 1;
               while (GetCH () != '"') { }
               return new Token (EToken.Quoted, mText, start, N, mLine, mColumn);
            default:
               start = N - 1;
               while (!sNameStop.Contains (PeekCH ())) { GetCH (); }
               var span = mText.AsSpan (start, N - start);
               foreach (var elem in sElements) 
                  if (elem.AsSpan ().Equals (span, StringComparison.Ordinal))
                     return new Token (EToken.Element, mText, start, N, mLine, mColumn);
               return new Token (EToken.Word, mText, start, N, mLine, mColumn);
         }
      }
   }
   static SearchValues<char> sTokens = SearchValues.Create ("[]{}().=\n\u001A");
   static SearchValues<char> sNameStop = SearchValues.Create ("[]{}()\".=\n\u001A \t\r");
   static string[] sElements = ["MENU", "TOPMENU", "SEPARATOR"];

   char PeekCH () => mText[N];

   void Pushback (Token t) {
      Lib.Check (!mPushedBack);
      mPushbackToken = t; mPushedBack = true;
   }
   Token mPushbackToken;
   bool mPushedBack;

   // Outputs the code for an element (recursively may include code blocks, 
   // other elements inside). The entire code is generated effectively by calling
   // OutElem on the outermost element (for example, like a TOPMENU, or a DIALOG)
   void OutElem (Token tElem) {
      string elem = tElem.TextS;
      var info = sElemData[elem];
      
      // Add the necessary parameters, and then the optional parameters
      bool comma = false, finishedArgs = false, openedContainer = false;
      AddLineNo (tElem);
      Add ($"if ({elem} (");
      List<string> props = [];   // Additional prop initialers (like .TIP="Double")

      // Add the necessary parameters
      for (int i = 0; i < info.NeedParams; i++) {
         if (comma) Add (", "); Add (GetExpr ());
         comma = true;
      }
      for (int i = 0; i < info.OptParams; i++) {
         if (TryGetExpr (out var expr)) {
            if (comma) Add (", "); Add (expr);
            comma = true;
         } else break;
      }
      for (; ; ) {
         Token t = GetToken ();
         switch (t.E) {
            case EToken.OpenSquare:
               // If we see an open square bracket, it means we're opening this container and we
               // are going to add additional elements inside. If this is a type of element that may
               // or may not have children (like a MENU), add the 'hasChildren:true' parameter to it. 
               Lib.Check (info.CCode != EContainer.No);
               if (info.CCode == EContainer.Maybe) Add (", hasChildren:true");
               openedContainer = true;
               FinishArgs (); 
               break;
            case EToken.Newline:
               // If we see a newline, make sure that we have opened the children container
               // if that is mandatory. For example, a TOPMENU line must end in an [
               if (info.CCode == EContainer.Yes && !openedContainer) Fatal ("Expected [");
               break;
            case EToken.Element:
               // If we see a child element, recurse in to output that
               if (openedContainer) {
                  FinishArgs ();
                  OutElem (t);
               } else {
                  Lib.Check (finishedArgs);
                  Pushback (t); 
                  return;
               }
               break;
            case EToken.Period:
               FinishArgs ();
               t = Expect (EToken.Word); Expect (EToken.Equals);
               var str = GetExpr ();
               props.Add ($"{t.TextS.ToUpper ()} ({str});");
               break;
            case EToken.OpenCurly:
               FinishArgs ();
               str = GatherUntil (EToken.CloseCurly);
               AddL (str); AddL ("}");
               break;
            case EToken.Word:
               str = t.TextS + " " + GatherUntil (EToken.Newline);
               AddL (str);
               break;
            case EToken.CloseCurly:
               AddL ("}");
               break;
            case EToken.CloseSquare:
               AddL ("}");
               return;
            default: throw new BadCaseException (t.E);
         }
      }

      void FinishArgs () {
         if (finishedArgs) return;
         AddL (")) {");
         finishedArgs = true;
      }
   }

   // Tries to read an expression, and if it cannot find one, this returns false
   // (and does not consume any tokens)
   bool TryGetExpr (out string s) {
      s = string.Empty;
      Token t = GetToken ();
      switch (t.E) {
         case EToken.Quoted: s = t.TextS; break;
         case EToken.Word: s = $"\"{t.Text}\""; break;
         case EToken.OpenParen: s = GatherUntil (EToken.CloseParen); break;
         default: Pushback (t); return false;
      }
      return true; 
   }

   // Nested types -------------------------------------------------------------
   enum EToken {
      Element = 128, Word, Quoted,

      OpenSquare = '[', CloseSquare = ']',
      OpenCurly = '{', CloseCurly = '}',
      OpenParen = '(', CloseParen = ')',
      Quote = '"', Period = '.', Equals = '=',
      Newline = '\n', EOF = '\u001A'
   }

   enum EContainer {
      Yes, No, Maybe
   }

   readonly struct Token {
      public Token (EToken e, string text, int start, int end, int line, int column) {
         E = e;
         mText = text; Start = start; End = end;
         Line = line; Column = column - (end - start - 1);
      }
      readonly string mText;

      public readonly EToken E;
      public readonly int Start, End;
      public readonly int Line, Column;
      public readonly ReadOnlySpan<char> Text => mText.AsSpan (Start, End - Start);
      public readonly string TextS => Text.ToString ();

      public override string ToString () {
         string s = E == EToken.Newline ? "\\n" : TextS;
         return $"{E} {s} ({Line},{Column})";
      }
   }

   readonly struct ElemInfo {
      public ElemInfo (int needParams, int optParams, EContainer ccode)
         => (NeedParams, OptParams, CCode) = (needParams, optParams, ccode);

      public readonly int NeedParams;
      public readonly int OptParams;
      public readonly EContainer CCode;
   }

   // Private data -------------------------------------------------------------
   readonly string mText;
   int mLine, mColumn, N;
   bool mFreshLine = true;
   StringBuilder mSB = new ();

   static Dictionary<string, ElemInfo> sElemData = new () {
      ["MENU"] = new (1, 2, EContainer.Maybe),
      ["TOPMENU"] = new (0, 0, EContainer.Yes),
      ["SEPARATOR"] = new (0, 0, EContainer.No)
   };
}
