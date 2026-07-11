using System.Buffers;
using System.Text;
namespace Nori.UX;

public class InlayGen {
   public InlayGen (string file) {
      var tf = Path.GetTempFileName ();
      File.Copy (file, tf, true); mText = File.ReadAllText (tf) + "\u001A";
      File.Delete (tf);
      mFile = Path.GetFileName (file);
   }
   string mFile;

   public string Generate (bool includeLineNo = false) {
      mIncludeLineNo = includeLineNo;
      AddL ("""
         using Nori;
         using System;
         using Nori.UX;
         using static Nori.UX.UXApi;
         namespace Nori.Inlay;

         class Inlay1 : InlayHub {
         public static void Generate () {
         """);

      try {
         ProcessLoop ();
      } catch (Exception e) {
         Lib.Trace (e);
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

   void AddLineNo (Token t) {
      if (mIncludeLineNo) 
         AddL ($"#line ({t.Line},{t.Column}) - ({t.Line},{t.Column + t.Text.Length}) \"{mFile}\"");
   }

   string DoIndent (string input) {
      StringBuilder sb = new ();
      int level = 0, indent = 3; 
      var S = input.Split ('\n').Select (a => a.Trim ()).ToList ();
      for (int i = 0; i < S.Count; i++) {
         if (S[i] is "}") level = Math.Max (--level, 0);
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
      if (N >= mText.Length) return '\u001A';
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
            case '$':
               start = N - 1; GetCH ();
               while (GetCH () != '"') { }
               return new Token (EToken.Quoted, mText, start, N, mLine, mColumn);
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

   void ProcessLoop () {
      for (; ; ) {
         Token t = GetToken ();
         if (t.E is EToken.CloseSquare or EToken.EOF) return;
         switch (t.E) {
            case EToken.Newline: break;
            case EToken.Element: OutElem (t); break;
            default:
               AddLineNo (t);
               AddL ($"{t.TextS} {GatherUntil (EToken.Newline)}"); 
               break;
         }
      }
   }

   // Outputs the code for an element (recursively may include code blocks, 
   // other elements inside). The entire code is generated effectively by calling
   // OutElem on the outermost element (for example, like a TOPMENU, or a DIALOG)
   void OutElem (Token tElem) {
      string elem = tElem.TextS; string? tag = null;
      var info = sElemData[elem];
      bool finishedArgs = false, openedContainer = false, addedCore = false;

      AddLineNo (tElem);
      if (info.Inert) Add ($"{elem} (");
      else Add ($"if ({elem} (");
      List<string> props = [];   // Additional prop initialers (like .TIP="Double")

      // Add the necessary parameters
      bool comma = false;
      for (int i = 0; i < info.NeedParams; i++) {
         string expr = GetExpr (); tag ??= expr;
         if (comma) Add (", "); Add (expr);
         comma = true;
      }
      // Add the optional parameters
      for (int i = 0; i < info.OptParams; i++) {
         if (TryGetExpr (out var expr)) {
            if (comma) Add (", "); Add (expr);
            comma = true;
         } else break;
      }
      // After adding these, we keep the parameter block still open, mainly because there could be a
      // [ that necessitates us having to add a .hasChildren=true parameter, and we don't know that yet
            
      // Then process the other code in the element
      for (; ; ) {
         Token t = GetToken ();
         switch (t.E) {
            // If we see an open square bracket, it means we're opening this container and we
            // are going to add additional elements inside. If this is a type of element that may
            // or may not have children (like a MENU), add the 'hasChildren:true' parameter to it. 
            case EToken.OpenSquare:
               Lib.Check (info.CCode != EContainer.No);
               if (info.CCode == EContainer.Maybe) Add (", hasChildren:true");
               AddL (")) {"); openedContainer = true;
               if (elem == "MENU") AddL ("POPUPMENU ();");
               ProcessLoop ();   // Keep adding child elements until we see a ']'
               if (elem == "MENU") AddL ("END (); // POPUPMENU");
               AddL ("}");
               props.ForEach (AddL);
               AddL ($"END (); // {elem} {tag}");
               return;
            case EToken.Newline:
               // If we see a newline, make sure that we have opened the children container
               // if that is mandatory. For example, a TOPMENU line must end in an [
               if (info.CCode == EContainer.Yes && !openedContainer) Fatal ("Expected [");
               break;
            // If we see a period, this is a property-setter (like .Icon=FileNew). Close the
            // args block if it's still open, and gather the property for outputting at the end.             
            case EToken.Period:
               t = Expect (EToken.Word); Expect (EToken.Equals);
               var str = GetExpr ();
               props.Add ($"{t.TextS.ToUpper ()} ({str});");
               break;
            // Here we process the code-block for this element (the bit that comes inside the if() {...})
            case EToken.OpenCurly:
               Lib.Check (!finishedArgs && !openedContainer);    
               Add (")) {"); finishedArgs = true;  
               str = GatherUntil (EToken.CloseCurly);
               if (str.Length < 60) AddL ($" {str} }}");
               else AddL ($"\n{str}\n}}");
               addedCore = true;
               break;
            case EToken.Element or EToken.CloseCurly or EToken.CloseSquare or EToken.Word:
               Lib.Check (!openedContainer);
               Pushback (t);
               goto Done;
            default: throw new BadCaseException (t.E);
         }
      }
      Done:
      if (!finishedArgs) {
         if (info.Inert) AddL (");");
         else AddL (")) {");
      }
      if (!addedCore) {
         if (!info.Inert) AddL ("}");
      }
      props.ForEach (AddL);
      if (!info.Inert) AddL ($"END (); // {elem} {tag}");
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
      public ElemInfo (int needParams, int optParams, EContainer ccode, bool inert)
         => (NeedParams, OptParams, CCode, Inert) = (needParams, optParams, ccode, inert);

      public readonly int NeedParams;
      public readonly int OptParams;
      public readonly EContainer CCode;
      public readonly bool Inert;
   }

   // Private data -------------------------------------------------------------
   readonly string mText;
   int mLine, mColumn, N;
   bool mFreshLine = true;
   StringBuilder mSB = new ();
   bool mIncludeLineNo;

   static Dictionary<string, ElemInfo> sElemData = new () {
      ["MENU"] = new (1, 2, EContainer.Maybe, false),
      ["TOPMENU"] = new (0, 0, EContainer.Yes, false),
      ["SEPARATOR"] = new (0, 0, EContainer.No, true)
   };
}
