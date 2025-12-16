#version 330

uniform vec2 VPScale;
uniform float PointSize;

layout (points) in;
layout (triangle_strip, max_vertices = 4) out;

out vec2 gSTCoord;

void main (void) { 
   vec4 v0 = gl_in[0].gl_Position;
   vec2 p = v0.xy / VPScale;

   float h = PointSize / 2;
   gl_Position = vec4 (VPScale * vec2 (p.x + h, p.y + h), v0.z, 1);
   gSTCoord = vec2 (h, h);
   EmitVertex ();

   gl_Position = vec4 (VPScale * vec2 (p.x + h, p.y - h), v0.z, 1);
   gSTCoord = vec2 (h, -h);
   EmitVertex ();

   gl_Position = vec4 (VPScale * vec2 (p.x - h, p.y + h), v0.z, 1);
   gSTCoord = vec2 (-h, h);
   EmitVertex ();

   gl_Position = vec4 (VPScale * vec2 (p.x - h, p.y - h), v0.z, 1);
   gSTCoord = vec2 (-h, -h);
   EmitVertex ();

   EndPrimitive ();
}
