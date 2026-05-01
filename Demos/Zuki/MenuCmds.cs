using System.ComponentModel;
using System.Reflection;
using System.Reactive.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Nori;
using Microsoft.Win32;
namespace Zuki;

static class MenuCmds {
   // Constructors -------------------------------------------------------------
   static MenuCmds () {
      mConv = TypeDescriptor.GetConverter (typeof (KeyGesture));
      HW.Keys.Where (a => a.IsPress ()).Subscribe (OnKey);
   }
   static TypeConverter mConv;

   // Methods ------------------------------------------------------------------
   public static void Connect (Menu menu) 
      => menu.Items.OfType<MenuItem> ().ForEach (Connect);

   // Implementation -----------------------------------------------------------
   static void Connect (MenuItem mi) {
      mi.Items.OfType<MenuItem> ().ForEach (Connect);
      string? gesture = mi.InputGestureText, tag = mi.Tag as string;
      if (tag is { }) {
         var bf = BindingFlags.Static | BindingFlags.NonPublic;
         if (typeof (MenuCmds).GetMethod (tag, bf) is MethodInfo method) {
            var action = method.CreateDelegate<Action> ();
            mi.Click += (s, e) => action ();
            if (!gesture.IsBlank () && mConv.ConvertFromString (gesture) is KeyGesture kg) {
               EKey key = Enum.Parse<EKey> (kg.Key.ToString (), true);
               EKeyModifier mod = Enum.Parse<EKeyModifier> (kg.Modifiers.ToString (), true);
               sHotKeys[(key, mod)] = action;
            }
         } else if (typeof (MenuCmds).GetProperty (tag, bf) is PropertyInfo prop) {
            mi.IsChecked = (bool)prop.GetValue (null)!;
            mi.Click += (s, e) => Toggle (mi, prop);
         } else
            mi.IsEnabled = false;
      }
   }

   static void Toggle (MenuItem mi, PropertyInfo prop) {
      bool value = !(bool)prop.GetValue (null)!;
      mi.IsChecked = value; prop.SetValue (null, value);
   }

   // Events -------------------------------------------------------------------
   static void OnKey (KeyInfo k) {
      if (sHotKeys.TryGetValue ((k.Key, k.Modifier), out var action))
         action ();
   }
   static Dictionary<(EKey, EKeyModifier), Action> sHotKeys = [];

   // Handler properties -------------------------------------------------------
   static bool FillDrawing { get => Hub.FillDrawing; set => Hub.FillDrawing = value; }

   // Handlers -----------------------------------------------------------------
   static void Dim3PAngle () => Hub.Widget = new Dim3PAngleMaker ();
   static void DimRadius () => Hub.Widget = new DimRadMaker ();
   static void DimDiameter () => Hub.Widget = new DimDiaMaker ();
   static void DimAngle () => Hub.Widget = new DimAngleMaker ();
   static void Exit () => Hub.MainWindow?.Close ();
   static void New () => Hub.Dwg = new ();

   static void Open () {
      var ofd = new OpenFileDialog { Filter = "DXF files|*.dxf" };
      if (ofd.ShowDialog () is true) Hub.LoadDXF (ofd.FileName);
   }

   static void Save () {
      var dwg = Hub.Dwg;
      dwg.Layers.ForEach (a => a.Color = Color4.Black);
      if (dwg.Filename.IsBlank ()) SaveAs ();
      else DXFWriter.Save (dwg, dwg.Filename, true);
   }

   static void SaveAs () {
      var ofd = new SaveFileDialog { Filter = "DXF files|*.dxf" };
      if (ofd.ShowDialog () is true) { Hub.Dwg.Filename = ofd.FileName; Save (); Hub.SetTitle (); }
   }
}
