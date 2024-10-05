// ────── ╔╗                                                                                  COVER
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Main window for the Nori coverage analyser display utility
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SPath = System.IO.Path;
namespace Nori.Cover;

/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window {
   public MainWindow () {
      FontSize = 13;
      InitializeComponent ();
      LoadCoverage ();
   }

   // WPF handlers ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄
   // Updates the display on the right with coverage-colored file contents,
   // when a different file is selected
   void OnFileSelected (object sender, RoutedPropertyChangedEventArgs<object> e) {
      // First, load the text of the file as a single string, and set up pointers
      // (indices) to the start of each line within this large block of text
      if (mTree.SelectedItem is not TreeViewItem item) return;
      if (item.Tag is not string file) return;
      var text = File.ReadAllText (file).Replace ("\r\n", "\n");
      List<int> starts = [0];
      for (int i = 0; i < text.Length; i++)
         if (text[i] == '\n') starts.Add (i + 1);

      // Load the coverage for this file, and update the status bar on the bottom
      var blocks = mCoverage!.GetBlocksFor (file).ToList ();
      int total = blocks.Count, covered = blocks.Count (a => a.Covered);
      double percent = Math.Round (100 * covered / (double)total, 1);
      mStatus.Text = $"{file.Replace ('\\', '/')} : {covered} / {total} blocks : {percent}%";

      // Create a FlowDocument, and generate runs there corresponding to each
      // covered or uncovered block in the file. Since a run can span multiple lines
      // (by embedding \n characters within it), and because the set of blocks we get
      // from the Coverage class are already sorted, this is pretty straightforward
      var fd = new FlowDocument ();
      var para = new Paragraph { FontFamily = new ("Consolas"), FontSize = 13 };
      int prevpos = 0;
      foreach (var b in blocks) {
         int pos1 = starts[b.Start.Line - 1] + b.Start.Col - 1;
         Append (pos1, Brushes.Transparent);
         int pos2 = starts[b.End.Line - 1] + b.End.Col - 1;
         Append (pos2, b.Covered ? Brushes.LightSkyBlue : Brushes.LightSalmon);
      }
      Append (text.Length, Brushes.Transparent);
      fd.Blocks.Add (para);
      mText.Document = fd;

      // Helpers .................................
      void Append (int end, Brush brush) {
         if (end <= prevpos) return;
         para.Inlines.Add (new Run (text[prevpos..end]) { Background = brush });
         prevpos = end;
      }
   }

   // Load the coverage
   void LoadCoverage () {
      mCoverage = new Coverage ("N:/Bin/Coverage.xml");
      TestRunner.SetNoriFiles (mCoverage);
      var blocks = mCoverage.Blocks;
      int total = blocks.Count, covered = blocks.Count (a => a.Covered);
      double percent = Math.Round (100.0 * covered / total, 2);
      Title = $"Nori: {covered} / {total} covered : {percent}%";
      FillTree ();
   }
   Coverage? mCoverage;

   // Called when the application starts up, this builds the tree-view of 
   // all the files in the Nori project on the left
   void FillTree () {
      mAllFiles.Clear ();
      var tvi = new TreeViewItem { Header = "N:", IsExpanded = true };
      Dictionary<string, TreeViewItem> paths = new () { [@"N:\"] = tvi };
      mTree.Items.Add (tvi);

      foreach (var file in Directory.EnumerateFiles (@"N:\", "*.cs", SearchOption.AllDirectories)) {
         bool include = mCoverage!.Files.Any (s => s.EqIC (file));
         if (include) {
            mAllFiles.Add (file);
            string path = SPath.GetDirectoryName (file)!;
            var parent = GetItem (path);
            parent.Items.Add (new TreeViewItem { Header = SPath.GetFileName (file), Tag = file });
         }
      }

      // Helper ...............................
      TreeViewItem GetItem (string path) {
         if (paths.TryGetValue (path, out var tvi)) return tvi;
         var parent = GetItem (SPath.GetDirectoryName (path)!);
         var child = new TreeViewItem { Header = SPath.GetFileName (path), IsExpanded = true };
         paths.Add (path, child);
         parent.Items.Add (child);
         return child;
      }
   }
   List<string> mAllFiles = [];
}
