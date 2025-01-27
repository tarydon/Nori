// ────── ╔╗                                                                                WPFDEMO
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Window class for WPFDemo application
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows;
using Nori;
using System.Windows.Input;
namespace WPFDemo;

/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      InitializeComponent ();
      Content = Pix.CreatePanel ();
      Pix.Info.Subscribe (FrameDone);
      KeyDown += OnKey;
   }

   void OnKey (object sender, KeyEventArgs e) {
      if (e.Key == Key.Escape) Close ();
   }

   void FrameDone (Pix.Stats s) {
      Title = $"Frame {s.NFrame}, Pgms:{s.PgmChanges}, VAO:{s.VAOChanges}, Uniforms:{s.ApplyUniforms}, Draws:{s.DrawCalls}, Verts:{s.VertsDrawn}, Const:{s.SetConstants}";
   }
}