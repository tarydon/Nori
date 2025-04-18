﻿// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ Exceptions.cs
// ║║║║╬║╔╣║ Various exception types used for Nori
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class BadCaseException ---------------------------------------------------------------------
/// <summary>Thrown when a case is not handled in some switch statement</summary>
/// This is typically a sign of unfinished code
public class BadCaseException (object e) : Exception ($"Unhandled case: {e}");
#endregion

#region class ParseException -----------------------------------------------------------------------
/// <summary>Thrown when we are not able to parse a string to a particular type</summary>
public class ParseException (string value, Type type) : Exception ($"Cannot convert '{value}' to {Lib.NiceName (type)}");
#endregion
