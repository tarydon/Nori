#version 330

layout (points) in;
layout (triangle_strip, max_vertices = 4) out;

void main () {
   vec4 b = gl_in[0].gl_Position;
   vec2 s = vec2 (b.z / 2, b.w / 2);

   gl_Position = vec4 (b.x - s.x, b.y - s.y, 0, 1);
   EmitVertex ();
   
   gl_Position = vec4 (b.x + s.x, b.y - s.y, 0, 1);
   EmitVertex ();

   gl_Position = vec4 (b.x - s.x, b.y + s.y, 0, 1);
   EmitVertex ();

   gl_Position = vec4 (b.x + s.x, b.y + s.y, 0, 1);
   EmitVertex ();
   EndPrimitive ();
}
