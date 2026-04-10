using System.Windows.Controls;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using Nori;
namespace WPFDemo;

class PaperFolderScene : Scene3 {
   // Create a listbox with all the sample files, and an image where we display the 
   // original 2D drawing (before folding)
   public void CreateUI (UIElementCollection ui) {
      ui.Clear ();
      var color = Color.FromRgb (232, 236, 240);
      var brush = new SolidColorBrush (color); brush.Freeze ();

      var b1 = new Border { Child = mLB, Margin = new Thickness (6, 6, 6, 0),
                            CornerRadius = new CornerRadius (8), Background = brush };
      mLB.ItemsSource = Directory.GetFiles ("N:/Demos/Data/Folder", "*.dxf")
                                .Select (a => Path.GetFileName (a)).ToList ();
      mLB.SelectionChanged += OnSelected;
      mLB.Background = brush;
      ui.Add (b1);

      var b2 = new Border { Child = mIM, Margin = new Thickness (6), Padding = new Thickness (8), 
                            CornerRadius = new CornerRadius (8), Background = brush };
      ui.Add (b2);
      Lib.Post (() => mLB.SelectedIndex = 0);
   }

   // Helper used to convert a Nori.DIBitmap into a WriteableBitmap (so we can use it
   // as an Image.Source)
   static WriteableBitmap GetBitmap (DIBitmap dib) {
      if (dib.Fmt != DIBitmap.EFormat.RGB8) throw new NotImplementedException ();
      var bmp = new WriteableBitmap (dib.Width, dib.Height, 96, 96, PixelFormats.Rgb24, null);
      bmp.Lock ();
      for (int i = 0; i < dib.Height; i++) {
         nint dest = nint.Add (bmp.BackBuffer, bmp.BackBufferStride * i);
         Marshal.Copy (dib.Data, dib.Stride * (dib.Height - i - 1), dest, dib.Stride);
      }
      bmp.AddDirtyRect (new Int32Rect (0, 0, dib.Width, dib.Height));
      bmp.Unlock ();
      return bmp;
   }

   // Handler called each time a different file is selected
   void OnSelected (object sender, SelectionChangedEventArgs e) {
      var s = (string)mLB.SelectedItem;
      var dwg = DXFReader.Load (Path.Combine (mDir, s));
      int cx = (int)(mIM.Width * Lux.DPIScale * 1.5), cy = (int)(mIM.Height * Lux.DPIScale * 1.5);
      cx = (cx >> 2) << 2;

      var group = new GroupVN ([new Dwg2VN (dwg), new DwgFillVN (dwg, ETess.Medium) { Color = new (192, 196, 200) }]);
      var scene = new Scene2 { Root = group, Bound = dwg.Bound.InflatedF (1.05),
                               BgrdColor = new Color4 (232, 236, 240) };
      var dib = scene.RenderImage (new (cx, cy), DIBitmap.EFormat.RGB8);
      mIM.Source = GetBitmap (dib);

      var pf = new PaperFolder (dwg);
      if (pf.Process (out var model))
         Lux.UIScene = new Scene3 { Bound = model.Bound, Root = new Model3VN (model) };
   }

   // Private data -------------------------------------------------------------
   string mDir = "N:/Demos/Data/Folder";
   Image mIM = new () { Width = 300, Height = 300, Stretch = Stretch.Fill };
   ListBox mLB = new () { Margin = new Thickness (4), MaxHeight = 200, BorderThickness = new Thickness (0) };
}
