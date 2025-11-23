// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Interfaces.cs
// ║║║║╬║╔╣║ Various interface definitions used (and exported) by Nore.Core
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region interface IEQuable<T> ----------------------------------------------------------------------
/// <summary>Interface implemented by classes / structs that have an EQ comparision method</summary>
public interface IEQuable<in T> {
   public bool EQ (T other);
}
#endregion

#region interface IIndexed -------------------------------------------------------------------------
/// <summary>IIndexed implements a class that has an unsigned 16-bit index</summary>
public interface IIndexed {
   int Idx { get; set; }
}
#endregion

#region interface IStmLocator ----------------------------------------------------------------------
/// <summary>The IStmLocator interface provides the basis for the Lib.OpenRead and related functions</summary>
/// It allows us to open a stream using an abstract filename like "nori:GL/Shader/Pixel.frag",
/// without having to worry about where that file is stored. It could be different on developer
/// machines, and different on installations on different operating systems. In general, we will
/// never try to open any standard resource files using raw filenames, but should always use a
/// stream-locator to open the file. In this example, the _prefix_ "nori:" routes this call to
/// a specific stream locator for that virtual drive, and that would have been registered earlier
/// using Lib.Register(IStmLocator).
public interface IStmLocator {
   public string Prefix { get; }
   public Stream? Open (string name);
}
#endregion
