// ────── ╔╗                                                                                   TEST
// ╔═╦╦═╦╦╬╣ TAuSystem.cs
// ║║║║╬║╔╣║ Tests of the Au system (curl read / write, binary read / write)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections.Immutable;
using System.Reactive.Subjects;
using System.Reflection;
namespace Nori.Testing;

[Fixture (20, "Test of AuSystem", "IO")]
class TAuSystem {
   TAuSystem () {
      Lib.AddAssembly (Assembly.GetExecutingAssembly ());
      Lib.AddNamespace ("Nori.Testing");
      Lib.AddMetadata (mMetadata.Split ('\n'));
   }

   string mMetadata = """
      Primitives
        Bool Short Int Long UShort UInt ULong Float Double Date Guid TSpan String -Unserialized
      Bad2
        Name
      Bad3
        Name
      Drawing
        Name Layers Shapes
      Shape
        ^Dwg Layer.Name Pos
      Square
        Size
      Circle
        Radius
      Layer
        Name Color Linetype
      Shape3
        Weight Layer.Name
      Layer3
        Name
      Holder
        Name Prim Prim1
      Collection
        AList Immutable Array List
      Zoo
        Name Animals Extra
      Animal
        Age Name
      Lion
      Tiger
        Stripes
      CType1
        Loc
      Location
        X Y
      CType2
        JBuf
      EFlagTest
         SingleBit MultiBit MultiBit2 Outlier
      EOutlierTest
         InRange Outlier
      """;

   [Test (61, "Test primitives")]
   void Test1 () {
      var prim0 = new Primitives ();
      prim0.Init ();
      Check (prim0, "T001.curl", "T001");

      var prim1 = (Primitives)CurlReader.Load (NT.TmpCurl);
      prim1.Unserialized.Is (0);
      Check (prim1, "T001.curl", "T001");
   }

   [Test (62, "Various Au errors")]
   void Test2 () {
      string message = Crasher (() => CurlWriter.ToFile (new Bad1 (), NT.TmpCurl));
      message.Is ("AuException: No metadata for Nori.Testing.Bad1");

      message = Crasher (() => CurlWriter.ToFile (new Bad2 (), NT.TmpCurl));
      message.Is ("AuException: Tactic missing for Nori.Testing.Bad2.Age");

      message = Crasher (() => CurlReader.Load (CurlWriter.ToByteArray (new Bad3 ("Hello"))));
      message.Is ("AuException: No parameterless constructor found for Nori.Testing.Bad3");

      message = Crasher (() => {
         Drawing dwg = new ("Temp");
         dwg.Add (new Layer ("Std", Color4.Black, ELineType.Continuous));
         dwg.Add (new Circle (dwg, dwg.Layers[0], (1, 2), 3));
         byte[] data = CurlWriter.ToByteArray (dwg.Shapes[0]);
         CurlReader.Load (data);
      });
      message.Is ("AuException: Nori.Testing.Circle.Dwg cannot be set to null");

      var layer3 = new Layer3 { Name = "Outline" };
      var shape3 = new Shape3 (3.5, layer3);
      Check (shape3, "T003.curl", "T003");
      message = Crasher (() => CurlReader.Load (NT.TmpCurl));
      message.Is ("AuException: Missing Nori.Testing.Layer3.ByName(IReadOnlyList<object>,string)");

      Holder holder = new Holder ();
      Check (holder, "T004.curl", "T004");
      message = Crasher (() => CurlReader.Load (NT.TmpCurl));
      message.Is ("AuException: Missing Nori.Testing.Prim0.Read(UTFReader)");

      holder = new Holder () { Prim1 = new Prim1 () };
      message = Crasher (() => CurlWriter.ToFile (holder, NT.TmpCurl));
      message.Is ("AuException: Missing Nori.Testing.Prim1.Write(UTFWriter)");

      holder = new Holder () { Prim1 = new Prim1 () };
      message = Crasher (() => CurlWriter.ToFile (holder, NT.TmpCurl));

      message = Crasher (() => CurlReader.Load ($"{NT.Data}/IO/T007.curl"));
      message.Is ("AuException: No metadata for 'Leopard'");
   }

   [Test (63, "Test of Uplink, ByName, ById")]
   void Test3 () {
      Drawing dwg = new ("FloorPlan");
      dwg.Add (new Layer ("Std", Color4.Black, ELineType.Continuous));
      dwg.Add (new Layer ("Bend", Color4.Blue, ELineType.Dash));
      dwg.Add (new Circle (dwg, dwg.Layers[0], (1, 2), 3));
      dwg.Add (new Square (dwg, dwg.Layers[1], (4, 5), 6));
      Check (dwg, "T002.curl", "T002");

      Drawing dwg2 = (Drawing)CurlReader.Load (NT.TmpCurl);
      dwg2.Shapes[0].Dwg.Name.Is ("FloorPlan");    // Check that Shape.Dwg is set by Uplink
   }

   [Test (64, "Test various IList")]
   void Test4 () {
      var c = new Collection { List = [1, 2, 3], Array = [4, 5, 6], Immutable = [7, 8, 9], AList = [10, 11, 12] };
      Check (c, "T005.curl", "T005");
      var c2 = (Collection)CurlReader.Load (NT.TmpCurl);
      Check (c2, "T005.curl", "T005");
   }

   [Test (65, "Test of dictionary")]
   void Test5 () {
      Zoo zoo = new () { Name = "London (Main)" };
      zoo.Animals.Add ('L', new Lion () { Name = "Simba", Age = 12 });
      zoo.Animals.Add ('T', new Tiger () { Name = "Elsa", Age = 18 });

      zoo.Extra = [];
      zoo.Extra["Single"] = 1.2f;
      zoo.Extra["Int"] = 12;
      zoo.Extra["String"] = "Hello";
      zoo.Extra["Point2"] = new Point2 (3, 5);
      zoo.Extra["Animal"] = new Lion () { Name = "Nala", Age = 3 };
      zoo.Extra["Array"] = new int[] { 1, 2, 3, 4, 5 };
      zoo.Extra["List"] = new List<double> { 1.1, 2.2, 3.3 };
      zoo.Extra["Dict"] = new Dictionary<int, string> () { [1] = "One", [2] = "Two" };
      Check (zoo, "T006.curl", "T006");

      var zoo2 = (Zoo)CurlReader.Load (NT.TmpCurl);
      Check (zoo2, "T006.curl", "T006");

      AuType.Get (typeof (Zoo)).ToString ().Is ("AuType Zoo");
      AuType.Get (typeof (Zoo)).Fields[0].Is ("AuField string Zoo.Name");
   }

   [Test (66, "Further Au exceptions")]
   void Test6 () {
      string message = Crasher (() => AuType.Get (typeof (Drawing)).WritePrimitive (new UTFWriter (), Point2.Nil));
      message.Is ("BadCaseException: Unhandled case 'Nori.Testing.Drawing' in WritePrimitive");

      var ct1 = new CType1 { Loc = new Location { X = 1, Y = 2 } };
      Check (ct1, "T008.curl", "T008");
      var ct1b = (CType1)CurlReader.Load (NT.TmpCurl);
      Check (ct1b, "T008.curl", "T008");

      message = Crasher (() => CurlWriter.ToFile (new CType2 (), NT.TmpCurl));
      message.Is ("AuException: 64-bit enums are not supported");
   }

   [Test (70, "Test [Flags] Enum")]
   void Test7 () {
      var f0 = new EFlagTest ();
      Check (f0, "T009.curl", "T009");

      var f1 = (EFlagTest)CurlReader.Load (NT.TmpCurl);
      Check (f1, "T009.curl", "T009");
   }

   [Test (71, "Test outlier Enum value")]
   void Test8 () {
      var f0 = new EOutlierTest ();
      Check (f0, "T010.curl", "T010");

      var f1 = (EOutlierTest)CurlReader.Load (NT.TmpCurl);
      Check (f1, "T010.curl", "T010");
   }

   static string Crasher (Action act) {
      string message = "No exception";
      try { act (); } catch (Exception e) { message = e.Description (); }
      return message;
   }

   void Check (object obj, string file, string? comment) {
      CurlWriter.ToFile (obj, NT.TmpCurl, comment);
      Assert.TextFilesEqual1 ($"IO/{file}", NT.TmpCurl);
   }
}

#pragma warning disable 0414, 0649
// .........................................................
class Primitives {
   public void Init () {
      Bool = true; Short = 1; Int = 2; Long = 3; UShort = 4; UInt = 5;  ULong = 6;
      Float = 7.1f; Double = 8.2; Date = new (2001, 6, 21, 10, 25, 35);
      Guid = new ("9A19103F-16F7-4668-BE54-9A1E7A4F7556");
      TSpan = new (1, 2, 3, 4, 5, 6); String = "Ten"; Unserialized = 11;
   }

   bool Bool; short Short; int Int; long Long; ushort UShort; uint UInt;
   ulong ULong; float Float; double Double; DateTime Date; Guid Guid;
   TimeSpan TSpan; string String = ""; public int Unserialized;
}

class Bad1 { string Name = "Hello"; int Age = 1; }
class Bad2 { string Name = "Hello"; int Age = 1; }
class Bad3 (string name) { string Name = name; }

// .........................................................
class Drawing {
   Drawing () => Name = "";
   public Drawing (string name) => Name = name;
   public readonly string Name;
   public IReadOnlyList<Shape> Shapes => mShapes;
   readonly List<Shape> mShapes = [];
   public void Add (Shape shape) { shape.Dwg = this; mShapes.Add (shape); }
   public void Add (Layer layer) => mLayers.Add (layer);
   public IReadOnlyList<Layer> Layers => mLayers;
   readonly List<Layer> mLayers = [];
}
class Layer {
   public Layer () => Name = string.Empty;
   public Layer (string name, Color4 color, ELineType linetype) => (Name, Color, Linetype) = (name, color, linetype);
   public readonly string Name;
   public readonly Color4 Color;
   public readonly ELineType Linetype;
   static Layer? ByName (IReadOnlyList<object> stack, string name) {
      for (int i = stack.Count - 1; i >= 0; i--)
         if (stack[i] is Drawing dwg) return dwg.Layers.FirstOrDefault (a => a.Name == name);
      return null;
   }
}
class Shape {
   protected Shape () => (Dwg, Layer) = (null!, null!);
   public Shape (Drawing dwg, Layer layer, Point2 pos) => (Dwg, Layer, Pos) = (dwg, layer, pos);
   public Drawing Dwg;
   public Layer Layer;
   public readonly Point2 Pos;
}
class Square : Shape {
   Square () { }
   public Square (Drawing dwg, Layer layer, Point2 pos, int size) : base (dwg, layer, pos) => Size = size;
   public readonly int Size;
   public double _Area;
   public Subject<int>? Pusher;
}
class Circle : Shape {
   Circle () { }
   public Circle (Drawing dwg, Layer layer, Point2 pos, int radius) : base (dwg, layer, pos) => Radius = radius;
   public readonly int Radius;
}

// .........................................................
class Layer3 { public string? Name; }
class Shape3 {
   Shape3 () => Layer = null!;
   public Shape3 (double w, Layer3 l) => (Weight, Layer) = (w, l);
   public double Weight;
   public Layer3 Layer;
}

class Holder {
   public string Name = "Holder";
   public Prim0 Prim = new () { X = 1.5, Y = 2.5 };
   public Prim1? Prim1 = null;
}
[AuPrimitive]
class Prim0 {
   public double X, Y;
   void Write (UTFWriter w) => w.Write (X).Write (',').Write (Y);
}
[AuPrimitive]
class Prim1 {
   public double X = 0, Y = 0;
}

// .........................................................
class Collection {
   public List<int>? List;
   public int[]? Array;
   public ImmutableArray<int> Immutable;
   public AList<int>? AList;
}

class Zoo {
   public string Name = "";
   public Dictionary<char, Animal> Animals = [];
   public Dictionary<string, object>? Extra;
}
abstract class Animal {
   public int Age;
   public string Name = "";
}
class Lion : Animal {
   public Lion () { }
   public Lion (int age, string name) { Name = name; Age = age; }
}
class Tiger : Animal {
   public Tiger () { }
   public Tiger (int age, string name) { Name = name; Age = age; Stripes = true; }
   public bool Stripes;
}

// .........................................................
struct Location {
   public Location () { }
   public int X;
   public int Y;
}
enum EJumpBuffer : ulong { Back = 1, Forward = 2 };

class CType1 { public Location Loc; }
class CType2 { public EJumpBuffer JBuf; }

// .........................................................
[Flags]
enum Bits : uint { One = 0x1, Two = 0x2, Eight = 0x8, Nine = Eight | One }

class EFlagTest {
   public Bits SingleBit = Bits.Two;
   public Bits MultiBit = Bits.One | Bits.Two;
   public Bits MultiBit2 = Bits.Nine;
   public Bits Outlier = (Bits)0x7;
}

// .........................................................
enum EType { One = 1, Two }

class EOutlierTest {
   public EType InRange = (EType)1;
   public EType Outlier = (EType)99;
}
#pragma warning restore 0414, 0649
