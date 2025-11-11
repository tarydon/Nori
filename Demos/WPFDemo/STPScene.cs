namespace WPFDemo;
using System.IO;
using System.Reactive.Linq;
using System.Windows.Controls;
using Nori;

class STPScene : Scene3 {
   public STPScene (string file = "N:/TData/Step/S00178.stp") {
      // file = "C:/Etc/S00676.stp";
      //if (mFiles.Count == 0) mFiles = Directory.GetFiles ("W:/STEP/Good").ToList ();
      //file = mFiles[0]; mFiles.RemoveAt (0);
      mFile = file;
      var sr = new STEPReader (file);
      sr.Parse ();
      mModel = sr.Build ();

      Lib.Tracer = TraceVN.Print;
      BgrdColor = Color4.Gray (96);
      Bound = mModel.Bound;
      Root = new GroupVN ([new Model3VN (mModel), TraceVN.It]);
      TraceVN.TextColor = Color4.Yellow;
      Lib.Trace ($"{mFile} {Bound.Diagonal.Round (1)}");
      mKeys = HW.Keys.Where (a => a.IsPress () && a.Key == EKey.P).Subscribe (a => OnProblem ());
   }
   string mFile;
   IDisposable mKeys;

   static List<string> mFiles = [];

   void OnProblem () {
      File.Move (mFile, "W:/STEP/Problem/" + Path.GetFileName (mFile));
      Lib.Trace ($"File {mFile} moved");
   }

   public override void Picked (object obj) {
      if (!HW.IsShiftDown)
         mModel.Ents.ForEach (a => a.IsSelected = false);
      if (obj is E3Surface ent) {
         Lib.Trace ($"Picked: {ent.GetType ().Name} #{ent.Id}");
         ent.IsSelected = true;
         if (HW.IsCtrlDown)
            foreach (var ent2 in mModel.GetNeighbors (ent)) ent2.IsSelected = true;
      }
   }
   Model3 mModel;

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
