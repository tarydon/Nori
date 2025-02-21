#version 330

uniform vec2 VPScale;

layout (location = 0) in ivec4 CharBoxN;
layout (location = 1) in int TexOffset;

out ivec2 vCellSize;
out int vTexOffset;

void main () {
   vCellSize = ivec2 (CharBoxN.z - CharBoxN.x, CharBoxN.w - CharBoxN.y);
   vTexOffset = TexOffset;
   gl_Position = vec4 (CharBoxN.xy * VPScale, CharBoxN.zw * VPScale) - vec4 (1, 1, 1, 1);
}
