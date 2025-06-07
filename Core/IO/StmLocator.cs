// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ StmLocator.cs
// ║║║║╬║╔╣║ Implementations of IStmLocator interface: FileStmLocator
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class FileStmLocator -----------------------------------------------------------------------
public class FileStmLocator (string prefix, string baseDir) : IStmLocator {
   // Properties ---------------------------------------------------------------
   public string Prefix => prefix;

   // Methods ------------------------------------------------------------------
   public Stream? Open (string name) {
      if (!name.StartsWith (prefix)) return null;
      string fullName = Path.Combine (baseDir, name[prefix.Length..]);
      return Path.Exists (fullName) ? File.OpenRead (fullName) : null;
   }
}
#endregion
