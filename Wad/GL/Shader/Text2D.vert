#version 330

uniform mat4 Xfm;

layout (location = 0) in ivec4 CharBoxN;
layout (location = 1) in vec2 VertexPos;
layout (location = 2) in int TexOffset;

out ivec2 vCellSize;
out int vTexOffset;

void main () {
   vCellSize = ivec2 (CharBoxN.z - CharBoxN.x, CharBoxN.w - CharBoxN.y);
   vTexOffset = TexOffset;
   vec2 xy0 = Xfm * vec4 (VertexPos, 0, 1).xy;
   vec2 xy1 = xy0 + vCellSize * VPScale;
   gl_Position = vec4 (xy0, xy1) - vec4 (1, 1, 1, 1);
}
