// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Init.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reflection;
using System.Windows;
namespace Nori;

public class Lux2 {
   public static void Init () {
      VNode.RegisterAssembly (Assembly.GetExecutingAssembly ());
      Hub.OpenGL.OnPaint = OnPaint;
   }

   static void OnPaint (int x, int y) => Lux.Render (Lux.UIScene, new Vec2S (x, y), ETarget.Screen, DIBitmap.EFormat.Unknown);
}
