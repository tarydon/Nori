// ────── ╔╗
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
      if (!name.StartsWith (prefix)) return null;
      mArchive ??= new ZipArchive (File.OpenRead (zipFile));
      if (mArchive.GetEntry (name[prefix.Length..].Replace ('\\', '/')) is { } ze) 
         return new ZipReadStream (ze.Open (), ze.Length);
      return null;
   }

   ZipArchive? mArchive;
}
#endregion

#region class ZipReadStream ------------------------------------------------------------------------
/// <summary>Stream implementation to wrap around a Zip archive</summary>
public class ZipReadStream (Stream stm, long length) : Stream {
   public override bool CanRead => stm.CanRead;
   public override bool CanSeek => stm.CanSeek;
   public override bool CanWrite => false;
   public override long Length => length;
   public override long Position { get => stm.Position; set => stm.Position = value; }
   public override void Flush () => throw new NotSupportedException ();
   public override int Read (byte[] buffer, int offset, int count) => stm.Read (buffer, offset, count);
   public override long Seek (long offset, SeekOrigin origin) => stm.Seek (offset, origin);
   public override void SetLength (long value) => throw new NotSupportedException ();
   public override void Write (byte[] buffer, int offset, int count) => throw new NotSupportedException ();
}
#endregion
