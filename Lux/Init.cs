// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Init.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reflection;
namespace Nori;

public class Lux2 {
   public static void Init () {
      VNode.RegisterAssembly (Assembly.GetExecutingAssembly ());
   }
}
