using System.Windows.Controls;
using System.IO;
using Nori;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
namespace WPFDemo;

class PaperFolderScene : Scene3 {
   public PaperFolderScene () {

   }

   public void CreateUI (UIElementCollection ui) {
      ui.Clear ();
      mLB.ItemsSource = Directory.GetFiles ("N:/Demos/Data/Folder", "*.dxf")
                                .Select (a => Path.GetFileName (a))
                                .ToList ();
      mLB.SelectionChanged += OnSelected;
      ui.Add (mLB);
      var b = new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness (1), 
                           Child = mIM, Margin = new Thickness (4) };
      ui.Add (b);
      mTimer.Tick += OnTick;
   }

   Image mIM = new Image { Width = 300, Height = 300, Stretch = Stretch.Fill };
   ListBox mLB = new () { Margin = new Thickness (4), MaxHeight = 200 };
   DispatcherTimer mTimer = new () { Interval = TimeSpan.FromSeconds (0.1), IsEnabled = true };

   void OnSelected (object sender, SelectionChangedEventArgs e) {
      var s = (string)mLB.SelectedItem;
      var dwg = DXFReader.Load (Path.Combine (mDir, s));
      int cx = (int)(mIM.Width * Lux.DPIScale * 1.5), cy = (int)(mIM.Height * Lux.DPIScale * 1.5);
      cx = (cx >> 2) << 2;

      var group = new GroupVN ([new Dwg2VN (dwg), new DwgFillVN (dwg)]);
      var scene = new Scene2 { Root = group, Bound = dwg.Bound.InflatedF (1.1),
                               BgrdColor = new Color4 (192, 196, 200) };
      var dib = scene.RenderImage (new (cx, cy), DIBitmap.EFormat.RGB8);
      mIM.Source = GetBitmap (dib);

      var pf = new PaperFolder (dwg);
      if (pf.Process (out var model))
         Lux.UIScene = new Scene3 { Bound = model.Bound, Root = new Model3VN (model) };
   }

   BitmapSource GetBitmap (DIBitmap dib) {
      if (dib.Fmt != DIBitmap.EFormat.RGB8) throw new NotImplementedException ();
      var bmp = new WriteableBitmap (dib.Width, dib.Height, 96, 96, PixelFormats.Bgr24, null);
      bmp.Lock ();
      for (int i = 0; i < dib.Height; i++) {
         nint dest = nint.Add (bmp.BackBuffer, bmp.BackBufferStride * i);
         Marshal.Copy (dib.Data, dib.Stride * (dib.Height - i - 1), dest, dib.Stride);
      }
      bmp.AddDirtyRect (new Int32Rect (0, 0, dib.Width, dib.Height));
      bmp.Unlock ();
      return bmp;
   }

   void OnTick (object? sender, EventArgs e) {
      mLB.SelectedIndex = 0;
      mTimer.IsEnabled = false;
   }

   string mDir = "N:/Demos/Data/Folder";
}
