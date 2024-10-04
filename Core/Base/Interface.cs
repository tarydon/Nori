﻿// ────── ╔╗ Nori.Core
// ╔═╦╦═╦╦╬╣ Copyright © 2024 Arvind
// ║║║║╬║╔╣║ Interfaces.cs ~ Various interfaces 
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region interface IEQuable<T> ----------------------------------------------------------------------
/// <summary>Interface implemented by classes / structs that have an EQ comparision method</summary>
public interface IEQuable<T> {
   public bool EQ (T other);
}
#endregion

#region interface IStmLocator ----------------------------------------------------------------------
/// <summary>The IStmLocator interface provides the basis for the Lib.OpenRead and related functions</summary>
/// It allows us to open a stream using an abstract filename like "wad:GL/Shader/Pixel.frag", 
/// without having to worry about where that file is stored. It could be different on developer
/// machines, and different on installations on different operating systems. In general, we will
/// never try to open any standard resource files using raw filenames, but should always use a
/// stream-locator to open the file. In this example, the _prefix_ "wad:" routes this call to
/// a specific stream locator for that virtual drive, and that would have been registered earlier
/// using Lib.Register(IStmLocator). 
public interface IStmLocator {
   public string Prefix { get; }
   public Stream? Open (string name);
}
#endregion

#region interface INotifySetChanged ----------------------------------------------------------------
public interface INotifySetChanged { event SetChangedEventHandler? SetChanged; }

public delegate void SetChangedEventHandler (object? sender, SetChangedEventArgs e);

public readonly struct SetChangedEventArgs (string setName, ESetChange change, int index) {
   public readonly string SetName => setName;
   public readonly ESetChange Change = change;
   public readonly int Index = index;
}

public enum ESetChange { Add, Remove, Empty, Fill };
#endregion