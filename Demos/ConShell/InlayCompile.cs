namespace UXDemo;

public class InlayCompiler {
   public InlayCompiler (string infile) => Text = File.ReadAllLines (infile);

   // Private data -------------------------------------------------------------
   readonly string[] Text;
   int N;
}
