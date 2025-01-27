#version 330

uniform vec2 VPScale;
uniform float LineWidth;
      
layout (lines) in;
layout (triangle_strip, max_vertices = 4) out;
out float gDist;

void main (void) { 
   vec2 invScale = 1 / VPScale;
   vec2 p0 = gl_in[0].gl_Position.xy * invScale;   // Now in pixel coordinates
   vec2 p1 = gl_in[1].gl_Position.xy * invScale;

   float width = LineWidth / 2;
   vec2 dir = normalize (p1 - p0) * width;
   vec2 perpdir = vec2 (dir.y, -dir.x);
   dir *= 0.0625;

   gDist = width * 2.9;
   gl_Position = vec4 (VPScale * (p0 + perpdir - dir), 0, 1);
   EmitVertex ();

   gl_Position = vec4 (VPScale * (p1 + perpdir + dir), 0, 1);   
   EmitVertex ();

   gDist = -width * 2.9;
   gl_Position = vec4 (VPScale * (p0 - perpdir - dir), 0, 1);
   EmitVertex();

   gl_Position = vec4 (VPScale * (p1 - perpdir + dir), 0, 1);
   EmitVertex ();

   EndPrimitive ();
}
