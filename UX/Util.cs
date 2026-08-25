using System.Threading;
namespace Nori;

class UXTimer {
   public static void Start (uint uid, int ms, Action callback) {
      if (mTimers.ContainsKey (uid)) return;
      mTimers.Add (uid, new Timer (_ => callback (), null, ms, -1));
   }

   public static void Stop (uint uid) {
      if (mTimers.TryGetValue (uid, out var timer)) {
         timer.Dispose (); mTimers.Remove (uid);
      }
   }

   static Dictionary<uint, Timer> mTimers = [];
}
