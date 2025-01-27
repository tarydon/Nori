// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Shader.cs
// ║║║║╬║╔╣║ Temporary code - preparing for Shader<T, U>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

abstract class Shader { // POI.
   protected Shader (ShaderImp program) => Program = program;
   public readonly ShaderImp Program;

   abstract public void ApplyUniforms (object data);
   abstract public object SnapUniforms ();
}

class Line2DShader : Shader {
   Line2DShader () : base (ShaderImp.Line2D) { }   // POI. private constructor

   public override void ApplyUniforms (object data) {
      Program.Use ();
      Uniform u = (Uniform)data;
      Program.Uniform ("Xfm", u.Xfm);
      Program.Uniform ("VPScale", u.VPScale);
      Program.Uniform ("DrawColor", (Vec4F)u.Color);
      Program.Uniform ("LineWidth", u.LineWidth);
   }

   public override object SnapUniforms () 
      => new Uniform (Pix.Xfm, Pix.VPScale, Pix.DrawColor, Pix.LineWidth);

   record struct Uniform (Mat4F Xfm, Vec2F VPScale, Color4 Color, float LineWidth);

   public static readonly Line2DShader It = new ();
}

class Bezier2DShader : Shader {
   Bezier2DShader () : base (ShaderImp.Bezier2D) { }

   public override void ApplyUniforms (object data) {
      Program.Use ();
      Uniform u = (Uniform)data;
      Program.Uniform ("Xfm", u.Xfm);
      Program.Uniform ("VPScale", u.VPScale);
      Program.Uniform ("DrawColor", (Vec4F)u.Color);
      Program.Uniform ("LineWidth", u.LineWidth);
   }

   public override object SnapUniforms ()
      => new Uniform (Pix.Xfm, Pix.VPScale, Pix.DrawColor, Pix.LineWidth);

   record struct Uniform (Mat4F Xfm, Vec2F VPScale, Color4 Color, float LineWidth);

   public static readonly Bezier2DShader It = new ();
}
