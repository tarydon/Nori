#version 330

layout (points) in;
layout (triangle_strip, max_vertices = 4) out;

in ivec2 vCellSize[];
in int vTexOffset[];
flat out ivec2 gCellSize;
flat out int gTexOffset;
out vec2 gTexCoord;

void main () {
   gCellSize = vCellSize[0];
   gTexOffset = vTexOffset[0];
   vec4 box = gl_in[0].gl_Position;

   gl_Position = vec4 (box.x, box.y, 0, 1);
   gTexCoord = vec2 (0, gCellSize.y);
   EmitVertex ();

   gl_Position = vec4 (box.z, box.y, 0, 1);
   gTexCoord = vec2 (gCellSize.x, gCellSize.y);
   EmitVertex ();

   gl_Position = vec4 (box.x, box.w, 0, 1);
   gTexCoord = vec2 (0, 0);
   EmitVertex ();

   gl_Position = vec4 (box.z, box.w, 0, 1);
   gTexCoord = vec2 (gCellSize.x, 0);
   EmitVertex ();

   EndPrimitive ();
}
