// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Shell for Nori console scratch applications
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;
using Nori;
namespace ConShell;

class Program {
   static void Main () {
      Lib.Init ();
      Lib.Tracer = Console.WriteLine;
   }
}
