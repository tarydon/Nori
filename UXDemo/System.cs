// ────── ╔╗
// ╔═╦╦═╦╦╬╣ System.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics.CodeAnalysis;
using Nori;
namespace UXDemo;

static public class UXSystem {
   public static TypeFace[] Typefaces = [];
   public static INodeClass?[] Classes = new INodeClass[8];

   public static void Register (INodeClass clas) {
      int n = (int)clas.Kind;
      while (Classes.Length <= n) Array.Resize (ref Classes, Classes.Length * 2);
      if (Classes[n] != null) Fatal ($"UX.Node {clas.Kind} already registered");
      Classes[n] = clas;
   }

   public static void BeginLayout (Vec2S size) {
   }

   public static void SetMouseState (Vec2S position, int wheelDelta, bool pressed) {
   }

   public static BeginNode () {
   }

   public static void EndLayout () { 
   }

   [DoesNotReturn]
   public static void Fatal (string s) {
      throw new Exception (s);
   }
}
