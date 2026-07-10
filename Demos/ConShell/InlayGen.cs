using System.Buffers;
using System.Text;
namespace Nori.UX;

public class InlayGen {
   public InlayGen (string file) => mText = File.ReadAllText (file) + "\u001A";
   int mN;
   string mText;

   public void GenerateTo (string outfile) {
      AddL ("using Nori;");
      AddL ("using System;");
      AddL ("using Nori.UX;");
      AddL ("using static Nori.UX.UXApi;");
      AddL ("namespace Nori.Inlay;");
      AddL ("");
      AddL ("class Inlay1 : InlayHub {");
      AddL ("static void Generate () {");
      try {
         for (int i = 0; i < 30; i++) {
            Token t = GetToken ();
            if (t.E == EToken.EOF) break;
            if (t.E == EToken.Newline) continue;
            if (t.E == EToken.CloseSquare) { AddL ("}"); continue; }
            if (t.E == EToken.Element) {
               switch (t.Text) {
                  case "TOPMENU": OutTopMenu (); break;
                  case "MENU": OutMenu (); break;
                  case "SEPARATOR": OutSeparator (); break;
                  default: throw new BadCaseException (t.Text.ToString ());
               }
               continue;
            }
            mN = t.Start;
            CopyUntil (EToken.Newline);
         }
      } catch (Exception e) {
         Console.ForegroundColor = ConsoleColor.Red;
         Console.WriteLine (e);
         Console.ResetColor ();
      }
      AddL ("}");
      AddL ("}");

      int level = 0, indent = 3;
      var S = mSB.ToString ().Split ('\n').Select (a => a.Trim ()).ToList ();
      for (int i = 0; i < S.Count; i++) {
         if (S[i] is "}") level--;
         var tmp = new string (' ', level * indent) + S[i];
         if (S[i].EndsWith ('{')) level++;
         S[i] = tmp;
      }
      File.WriteAllLines (outfile, S);
   }
   StringBuilder mSB = new ();

   void Add (string s) => mSB.Append (s);
   void AddL (string s) => mSB.AppendNL (s);

   void OutTopMenu () {
      AddL ("if (TOPMENU ()) {");
      Expect (EToken.OpenSquare); Expect (EToken.Newline);
   }

   void OutMenu () {
      // Add the 'name' and the 'shortcut' parameters
      Add ("if (MENUITEM (");
      AddStr (); AddStr (true, true);
      List<(string, string)> props = [];
      bool popup = false, closed = false;
      for (; ; ) {
         var t = GetToken ();
         if (t.E == EToken.Newline) continue;
         if (t.E is EToken.Element or EToken.CloseCurly or EToken.CloseSquare) { Pushback (t); break; }
         if (t.E == EToken.OpenSquare) { popup = true; Expect (EToken.Newline); break; }
         if (t.E == EToken.Period) {
            // If there is a period, we're setting a property on the element we're creating
            t = Expect (EToken.Word); string propname = t.TextS.ToUpper ();
            Expect (EToken.Equals); string val = GetExpression ();
            props.Add ((propname, val));
            continue; 
         }
         if (t.E == EToken.OpenCurly) {
            Add (")) {"); closed = true;
            CopyUntil (EToken.CloseCurly);
            AddL ("");
            continue; 
         }
         if (t.E == EToken.OpenParen) {
            Pushback (t); string val = GetExpression ();
            Add ($", {val}");
            continue;
         }
         Lib.Check (false);
      }
      if (!closed) {
         if (popup) { Add (", popup:true"); AddL (")) {"); }
         else AddL (")) { }");
      }
      foreach (var (k, v) in props) AddL ($"{k} ({v});");
      if (!popup) AddL ("END ();");
   }

   void OutSeparator () => AddL ("SEPARATOR ();");

   void AddP (string s) => Add ($"\"{s}\"");
   void Add (char ch) => mSB.Append (ch);

   string GetExpression () {
      Token t = GetToken ();
      if (t.E != EToken.OpenParen) {
         if (t.TextS[0] == '"') return t.TextS;
         return $"\"{t.TextS}\"";
      }
      string expr = "";
      for (; ; ) {
         t = GetToken (); if (t.E == EToken.CloseParen) break;
         string txt = t.TextS; expr += txt; if (txt != ".") expr += " ";
      }
      return expr.Replace (".Disabled", "DISABLED").TrimEnd ();
   }

   void CopyUntil (EToken e) {
      int start = mN;
      for (; ; ) {
         Token t = GetToken (); if (t.E != e) continue;
         Add (mText[start..mN]);
         break;
      }
   }

   void AddStr (bool optional = false, bool commabefore = false) {
      Token t = GetToken ();
      if (t.E is EToken.Quoted or EToken.Word) {
         if (commabefore) Add (", ");
         if (t.Text[0] == '$') Add ($"{t.TextS}\"");
         else Add ($"\"{t.Text}\"");
         return;
      }
      if (optional) { Pushback (t); return; }
      Lib.Check (false);
   }

   // Implementation -----------------------------------------------------------
   Token Expect (EToken e) {
      Token t = GetToken ();
      Lib.Check (t.E == e);
      return t;
   }

   void Pushback (Token t) {
      Lib.Check (!mBacked);
      mBack = t; mBacked = true;
   }
   Token mBack;
   bool mBacked;

   Token GetToken () {
      if (mBacked) { mBacked = false; return mBack; }
      for (; ; ) {
         char ch = mText[mN++];
         switch (ch) {
            case ' ' or '\t' or '\r': continue;
            case '\n': return new (EToken.Newline, mText, mN);
            case '\u001A': return new (EToken.EOF, mText, mN);
            case '[': return new (EToken.OpenSquare, mText, mN);
            case ']': return new (EToken.CloseSquare, mText, mN);
            case '(': return new (EToken.OpenParen, mText, mN);
            case ')': return new (EToken.CloseParen, mText, mN);
            case '{': return new (EToken.OpenCurly, mText, mN);
            case '}': return new (EToken.CloseCurly, mText, mN);
            case '.': return new (EToken.Period, mText, mN);
            case '=': return new (EToken.Equals, mText, mN);
            case '$':
               int start = mN++ - 1;
               while (mN < mText.Length && mText[mN] != '"') mN++;
               return new (EToken.Word, mText, start, (++mN) - 1);
            case '"':
               start = mN;
               while (mN < mText.Length && mText[mN] != '"') mN++;
               return new (EToken.Quoted, mText, start, (++mN) - 1); 
            default:
               start = mN - 1;
               while (!mStop.Contains (mText[mN])) mN++;
               var span = mText.AsSpan (start, mN - start);
               foreach (var w in mElements) {
                  if (w.AsSpan ().Equals (span, StringComparison.Ordinal))
                     return new (EToken.Element, mText, start, mN);
               }
               return new (EToken.Word, mText, start, mN);
         }
      }
   }
   static SearchValues<char> mStop = SearchValues.Create (" \t\r\n\u001A[]{}()\".=");
   static string[] mElements = ["MENU", "TOPMENU", "SEPARATOR"];

   // Nested types -------------------------------------------------------------
   readonly struct Token {
      public Token (EToken e, string text, int n) {
         E = e; mText = text; Start = n - 1; End = n;
      }
      public Token (EToken e, string text, int start, int end) {
         E = e; mText = text; Start = start; End = end;
      }
      readonly string mText;

      public override string ToString () {
         return $"{E} | {Text}";
      }

      public readonly EToken E;
      public readonly int Start, End;
      public readonly ReadOnlySpan<char> Text => mText.AsSpan (Start, End - Start);

      public readonly string TextS {
         get {
            if (E == EToken.Quoted) return $"\"{Text}\"";
            return Text.ToString ();
         }
      }
   }

   enum EToken {
      Word,
      Element,
      Quoted,
      Newline,
      CodeBlock,
      Expression,
      OpenSquare, CloseSquare,
      OpenCurly, CloseCurly,
      OpenParen, CloseParen,
      Quote, 
      Period,
      Equals,
      EOF,
   }
}
