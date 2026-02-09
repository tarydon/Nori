// ────── ╔╗
// ╔═╦╦═╦╦╬╣ IniFile.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Globalization;
using System.IO;
namespace Nori;

#region class IniFile ------------------------------------------------------------------------------
/// <summary>IniFile is used to read data from a Windows-style INI file</summary>
public class IniFile {
   // Constructors -------------------------------------------------------------
   /// <summary>Open an IniFile, given the filename and the section name</summary>
   public IniFile (string filename, string section) {
      mLines = [.. File.ReadAllLines (filename)];
      Section = section;
   }
   List<string> mLines;

   // Properties ---------------------------------------------------------------
   /// <summary>The current section</summary>
   public string Section {
      get => mSection;
      set {
         if (mSection.EqIC (value)) return;
         string head = $"[{mSection = value}]";
         mSecStart = mLines.FindIndex (a => a.StartsWithIC (head));
      }
   }
   string mSection = "";
   int mSecStart = -1;

   // Methods ------------------------------------------------------------------
   /// <summary>Returns a bool from the current section (or false if key not found)</summary>
   /// Values 1, TRUE, YES are treated as boolean true
   public bool GetB (string key) => GetB (key, false);

   /// <summary>Reads a bool from the current section, with given fallback value</summary>
   public bool GetB (string key, bool fallback) {
      string s = GetS (key, fallback.ToString ());
      return s.Trim ().ToUpper () is "TRUE" or "1" or "YES";
   }

   /// <summary>Returns a double value from the current section, given the key (or 0.0 if key not found)</summary>
   public double GetD (string key) => GetD (key, 0.0);

   /// <summary>Returns a double value from the current section, given the key</summary>
   /// If the value is not found, returns the default provided
   public double GetD (string key, double fallback) => GetS (key).ToDouble (fallback);

   /// <summary>Reads an integer from the current section (or 0 if key not found)</summary>
   public int GetN (string key) => GetN (key, 0);

   /// <summary>Reads Get an integer value from the current section, given the key</summary>
   /// If the value is not found, returns the default value provided
   public int GetN (string key, int fallback) {
      string val = GetS (key);
      if (int.TryParse (val, out int n)) return n;
      if (val.StartsWithIC ("0x") && int.TryParse (val[2..], NumberStyles.HexNumber, null, out n)) return n;
      return fallback;
   }

   /// <summary>Gets a string value from the current section, given the key</summary>
   public string GetS (string key) => GetS (key, "");

   /// <summary>Gets a string value from the current section, returning the fallback value if not found</summary>
   public string GetS (string key, string fallback = "") {
      if (mSecStart != -1) {
         string key1 = $"{key}=", key2 = $"{key} =";
         for (int i = mSecStart + 1; i < mLines.Count; i++) {
            string s = mLines[i].Trim ();
            if (s.StartsWith ('[')) break;      // Got to the next section
            if (s.StartsWithIC (key1) || s.StartsWithIC (key2)) {
               var value = s[(s.IndexOf ('=') + 1)..].TrimStart ();
               int n = value.IndexOf (';');
               if (n != -1) value = value[..n];
               n = value.IndexOf ("//");
               if (n != -1) value = value[..n];
               return value.Trim ().Unquote ();
            }
         }
      }
      return fallback;
   }
}
#endregion
