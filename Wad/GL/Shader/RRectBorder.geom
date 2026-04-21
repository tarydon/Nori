#version 330

layout (points) in;
layout (triangle_strip, max_vertices = 4) out;

in ivec2 vSize[];
in int vRadius[];
in int vBorder[];

out vec2 gPos;				   // Pixel position, relative to center
flat out ivec2 gSize;		// Box half-size
flat out int gRadius;
flat out int gBorder;

void main () {
   vec4 b = gl_in[0].gl_Position;
   vec2 size = vSize[0] / 2;

   gRadius = vRadius[0];
   gBorder = vBorder[0];
   gSize = ivec2 (vSize[0].x / 2 - vRadius[0] - 1, vSize[0].y / 2 - vRadius[0] - 1);
   vec2 s = vec2 (b.z / 2, b.w / 2);

   gl_Position = vec4 (b.x - s.x, b.y - s.y, 0, 1);
   gPos = vec2 (-size.x, -size.y);
   EmitVertex ();
   
   gl_Position = vec4 (b.x + s.x, b.y - s.y, 0, 1);
   gPos = vec2 (size.x, -size.y);
   EmitVertex ();

   gl_Position = vec4 (b.x - s.x, b.y + s.y, 0, 1);
   gPos = vec2 (-size.x, size.y);
   EmitVertex ();

   gl_Position = vec4 (b.x + s.x, b.y + s.y, 0, 1);
   gPos = vec2 (size.x, size.y);
   EmitVertex ();
   EndPrimitive ();
}
