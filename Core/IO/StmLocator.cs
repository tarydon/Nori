// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ StmLocator.cs
// ║║║║╬║╔╣║ Implementations of IStmLocator interface: FileStmLocator
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using System.IO.Compression;

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

#region class ZipStmLocator ------------------------------------------------------------------------
public class ZipStmLocator (string prefix, string zipFile) : IStmLocator {
   // Properties ---------------------------------------------------------------
   public string Prefix => prefix;

   // Methods ------------------------------------------------------------------
   public Stream? Open (string name) {
      if (!name.StartsWith (prefix) || !Path.Exists (zipFile)) return null;
      var archive = new ZipArchive (File.OpenRead (zipFile));
      return archive.GetEntry (name[prefix.Length..])?.Open ();
   }
}
#endregion