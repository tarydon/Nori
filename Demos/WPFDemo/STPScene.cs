namespace WPFDemo;

using System.DirectoryServices;
using System.IO;
using System.Windows.Controls;
using Nori;

class STPScene : Scene3 {
   public STPScene (string file = "N:/TData/Step/Boot.step") {
      mFile = file;
      var sr = new STEPReader (file);
      sr.Parse ();
      var model = sr.Build ();

      Lib.Tracer = TraceVN.Print;
      BgrdColor = Color4.Gray (96);
      Bound = model.Bound;
      Root = new GroupVN ([new Model3VN (model), TraceVN.It]);
      TraceVN.TextColor = Color4.Yellow;
   }
   string mFile;

   public void CreateUI (UIElementCollection ui) {
      ui.Clear ();
      var files = Directory.GetFiles ("C:/STEP", "*.step")
                           .Select (a => Path.GetFileNameWithoutExtension (a))
                           .ToList ();
      var lb = new ListBox () {
         ItemsSource = files, ClipToBounds = true, MaxHeight = 900
      };
      lb.SelectionChanged += OnFileSelected;
      ui.Add (lb);
   }

   void OpenFile (string file) {
      mFile = file;
      var sr = new STEPReader (file);
      sr.Parse ();
      var model = sr.Build ();
      Bound = model.Bound;
      Root = new GroupVN ([new Model3VN (model), TraceVN.It]);
   }

   void OnFileSelected (object sender, SelectionChangedEventArgs e) {
      var lb = (ListBox)sender;
      string name = (string)lb.SelectedItem;
      OpenFile ($"C:/STEP/{name}.step");
   }
}
