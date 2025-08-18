// ────── ╔╗
// ╔═╦╦═╦╦╬╣ HTMLGen.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori.Doc;

class HTMLGen {
   protected readonly StringBuilder mS = new ();

   /// <summary>Outputs a complete element.</summary>
   /// This is equivalent to calling BEGIN, outputting the content, and calling END.
   /// <param name="name">Element name, like "td", "a" </param>
   /// <param name="content">Actual content that should come between the open and close tags</param>
   /// <param name="more">Attributes for the element, in a similar fashion to what BEGIN expects</param>
   public HTMLGen ELEM (string name, string content, params object[] more) {
      mS.Append ($"<{name}");
      WriteParams (more);
      if (content == "") mS.Append (" />");
      else mS.Append ($">{content}</{name}>");
      return this;
   }

   public void HEAD (string title) {
      mS.Append ($"""
         <!DOCTYPE html>
         <html lang="en" xmlns="http://www.w3.org/1999/xhtml">
         <head>
         <meta charset="utf-8" />
         <title>{title}</title>
         <link rel="stylesheet" href="doc.css">
         </head>
         <body>

         """);
   }

   public void H1 (string content, params object[] more) => ELEM ("h1", content, more).NL ();

   public void H2 (string content, params object[] more) => ELEM ("h2", content, more).NL ();

   public void P (string content, params object[] more) => ELEM ("p", content, more).NL ();

   public void PRE (string content, params object[] more) => ELEM ("pre", content, more).NL ();

   public void NL () => mS.Append ('\n');

   /// <summary>Outputs a set of attributes for any HTML element</summary>
   /// These attributes should be a set of key value pairs. The keys are all the even numbered
   /// elements in the array. The values are all the odd numbered elements that follow them. Some
   /// keys don't have any value; for such cases, simply pass in an empty string "" as the value.
   /// <param name="more"></param>
   private void WriteParams (params object[] more) {
      for (int i = 0; i < more.Length; i += 2) {
         mS.Append ($" {more[i]}");
         var str = more[i + 1].ToString ();
         if (str != "") mS.Append ($"=\"{str}\"");
      }
   }
}
