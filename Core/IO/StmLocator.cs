// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ StmLocator.cs
// ║║║║╬║╔╣║ Implementations of IStmLocator interface: FileStmLocator
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class FileStmLocator -----------------------------------------------------------------------
class FileStmLocator : IStmLocator {
   public FileStmLocator (string prefix, string baseDir)
      => (mPrefix, mBaseDir) = (prefix, baseDir);

   public string Prefix => mPrefix;
   readonly string mPrefix, mBaseDir;

   public Stream? Open (string name) {
      if (!name.StartsWith (mPrefix)) return null;
      string fullName = Path.Combine (mBaseDir, name[mPrefix.Length..]);
      if (!Path.Exists (fullName)) return null;
      return File.OpenRead (fullName);
   }
}
#endregion
