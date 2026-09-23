// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Generator.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Buffers;
namespace Nori;

class UXGenerator {
   // Constructors -------------------------------------------------------------
   public UXGenerator (string file) {
      mFile = Path.GetFileName (file);
      if (mFile.ToUpper ().StartsWith ("ROOT.")) mUID = 1; 
      mText = File.ReadAllText (file) + "\u001A";
   }
   static int mUID;

   // Methods ------------------------------------------------------------------
   public string Generate (bool includeLineNo = false) {
      mIncludeLineNo = includeLineNo;
      AddL ("""
         using Nori;
         using System;
         using static Nori.UXApi;
         namespace Nori.Inlay;

         class Inlay1 : InlayHub {
         public static void Generate () {
         """);

      ProcessLoop ();

      AddL ("""
         }
         }
         """);
      return DoIndent (mSB.ToString ());
   }

   // Implementation -----------------------------------------------------------
   void Add (string s) => mSB.Append (s);
   void AddL (string s) => mSB.AppendNL (s);

   // Add a #line directive into the output
   void AddLineNo (Token t) {
      if (mIncludeLineNo)
         AddL ($"#line ({t.Line},{t.Column}) - ({t.Line},{t.Column + t.Text.Length}) \"{mFile}\"");
   }

   // Indents the generated text (pretty-printing)
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

   // Expects and returns a token of a given type
   Token Expect (EToken e) {
      Token t = GetToken (); Lib.Check (t.E == e);
      return t;
   }

   void Fatal (string s) { throw new Exception (s); }

   // Returns one expression 
   string GetExpr () {
      Lib.Check (TryGetExpr (out var s));
      return s;
   }

   // Gathers expressions 
   List<string> GatherExpressions (string argTypes) {
      List<string> expressions = [];
      for (int i = 0; ; i++) {
         if (!TryGetExpr (out var expr)) break;
         if (i <= argTypes.Length && char.ToUpper (argTypes[i]) != 'S') expr = expr.Unquote ();
         expressions.Add (expr);
      }
      return expressions;
   }

   // Gets the next char from the file, updates mLine, mColumn etc
   char GetCH () {
      if (N >= mText.Length) return '\u001A';
      char ch = mText[N++];
      if (mFreshLine) { mLine++; mColumn = 0; mFreshLine = false; }
      mColumn++; mFreshLine = ch == '\n';
      return ch;
   }

   // Gathers text from the current location until the given finisher token.
   // The finisher token is consumed, but is not included in the returned string
   string GatherUntil (EToken e) {
      int start = N;
      for (; ; ) {
         Token t = GetToken ();
         if (t.E == e) return mText[start..t.Start].Trim ();
      }
   }

   // Gets the next token from the .in file
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
               EToken tok = Enum.TryParse<UXNode.EKind> (span, true, out _) ? EToken.Element : EToken.Word;
               return new Token (tok, mText, start, N, mLine, mColumn);
         }
      }
   }
   static SearchValues<char> sTokens = SearchValues.Create ("[]{}().=\n\u001A");
   static SearchValues<char> sNameStop = SearchValues.Create ("[]{}()\".=\n\u001A \t\r");

   // Outputs the code for an element (recursively may include code blocks, 
   // other elements inside). The entire code is generated effectively by calling
   // OutElem on the outermost element (for example, like a TOPMENU, or a DIALOG)
   void OutElem (Token tElem) {
      int cVar = ++NextVar;
      string elem = tElem.TextS; string? tag = null;
      var kind = Enum.Parse<UXNode.EKind> (tElem.TextS, true);
      var info = UXEngine.Classes[(int)kind];
      if (info == null) throw new Exception ($"No UXClass registered for EKind.{kind}");
      bool finishedArgs = false, openedContainer = false, addedCore = false;

      var args = GatherExpressions (info.ArgTypes);
      Lib.Check (args.Count >= info.NeedParams && args.Count <= info.NeedParams + info.OptParams);
      if (info.VarType is { } vt) AddL ($"{vt} _v{++cVar} = {args[info.BoundArg]};");
      AddLineNo (tElem);
      if (info.Inert) Add ($"{elem} (");
      else Add ($"if ({elem} (");
      string key = $"{Path.GetFileNameWithoutExtension (mFile).ToLower ()}.{tElem.Line}";
      if (info.NeedsKey) Add ($"\"{key}\"");
      List<string> props = [];   // Additional prop initialers (like .TIP="Double")

      // Add the necessary parameters
      for (int i = 0; i < args.Count; i++) {
         string arg = args[i]; tag ??= arg;
         Add (", ");
         if (i == info.BoundArg && info.VarType is { }) arg = $"ref _v{cVar}";
         Add (arg);
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
               Lib.Check (info.CCode != UXClass.EContainer.No);
               if (info.CCode == UXClass.EContainer.Maybe) Add (", hasChildren:true");
               AddL (")) {"); openedContainer = true;
               if (elem == "MENU") AddL ($"POPUPMENU ($\"{key}.p\");");
               ProcessLoop ();   // Keep adding child elements until we see a ']'
               if (elem == "MENU") AddL ("END (); // POPUPMENU");
               AddL ("}");
               props.ForEach (AddL);
               int count = elem == "DIALOG" ? 2 : 1;
               AddL ($"END ({count}); // {elem} {tag}");
               return;
            case EToken.Newline:
               // If we see a newline, make sure that we have opened the children container
               // if that is mandatory. For example, a TOPMENU line must end in an [
               if (info.CCode == UXClass.EContainer.Yes && !openedContainer) Fatal ("Expected [");
               break;
            // If we see a period, this is a property-setter (like .Icon=FileNew). Close the
            // args block if it's still open, and gather the property for outputting at the end.             
            case EToken.Period:
               t = Expect (EToken.Word);
               string expr = "";
               if (!sPropType.ContainsKey (t.TextS))
                  Console.Write ('?');
               switch (sPropType[t.TextS]) {
                  case '-': break;
                  case 'S': Expect (EToken.Equals); expr = GetExpr (); break;
                  default: Expect (EToken.Equals); expr = GetExpr ().Unquote (); break;
               }
               if (expr == "") props.Add ($"{t.TextS.ToUpper ()} ();");
               else props.Add ($"{t.TextS.ToUpper ()} ({expr});");
               break;
            // Here we process the code-block for this element (the bit that comes inside the if() {...})
            case EToken.OpenCurly:
               Lib.Check (!finishedArgs && !openedContainer);
               Add (")) {"); finishedArgs = true;
               var str = GatherUntil (EToken.CloseCurly);
               str = str.Replace (".Value", $"_v{cVar}");
               if (str.Length < 60 && !str.Contains ('\n')) AddL ($" {str} }}");
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
         else { Add (")) {"); if (addedCore) AddL (""); }
      }
      if (!addedCore) {
         if (!info.Inert) {
            if (info.VarType is { }) {
               if (elem == "RADIOBUTTON") AddL ($" {args[info.BoundArg].Replace ("==", "=")}; }}");
               else AddL ($" {args[info.BoundArg]} = _v{cVar}; }}");
            } else AddL ("}");
         }
      }
      props.ForEach (AddL);
      if (!info.Inert) AddL ($"END (); // {elem} {tag}");
      sLastElem = elem;
   }
   static string sLastElem = "";
   static int NextVar;

   // Peeks the next char in the file without consuming it
   char PeekCH () => mText[N];

   // Processes one element - this is a recursive routine where the OutElem 
   // subroutine may call this in turn 
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

   // Pushes back a token into the token-queue, so it is retrieved next time
   void Pushback (Token t) {
      Lib.Check (!mPushedBack);
      mPushbackToken = t; mPushedBack = true;
   }
   Token mPushbackToken;
   bool mPushedBack;

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
      OpenSquare = '[', CloseSquare = ']', OpenCurly = '{', CloseCurly = '}',
      OpenParen = '(', CloseParen = ')', Quote = '"', Period = '.', 
      Equals = '=', Newline = '\n', EOF = '\u001A'
   }

   readonly struct Token {
      public Token (EToken e, string text, int start, int end, int line, int column) {
         E = e; mText = text; Start = start; End = end;
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

   // Private data -------------------------------------------------------------
   readonly string mFile;        // File we're reading from 
   readonly string mText;        // Text of the file
   bool mIncludeLineNo;          // Include source line numbers in output
   StringBuilder mSB = new ();   // Text is gathered here
   int mLine, mColumn, N;        // Current line, column and character position
   bool mFreshLine = true;       // If set, we are starting a new line

   static Dictionary<string, char> sPropType = new (StringComparer.OrdinalIgnoreCase) {
      ["CHILDGAP"] = 'I', ["HORIZONTAL"] = '-', ["VERTICAL"] = '-', ["WIDTH"] = 'I', 
      ["HGROW"] = '-', ["HEIGHT"] = 'I', ["VGROW"] = '-', ["BGRDCOLOR"] = 'S', 
      ["ICON"] = 'S', ["TIP"] = 'S', ["BGRD"] = 'S', ["PADDING"] = 'I'
   };
}
