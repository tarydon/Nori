#version 330

uniform vec2 VPScale;

layout (location = 0) in ivec4 CharBoxN;
layout (location = 1) in int TexOffset;

out ivec2 vCellSize;
out int vTexOffset;

void main () {
   vCellSize = ivec2 (CharBoxN.z - CharBoxN.x, CharBoxN.w - CharBoxN.y);
   vTexOffset = TexOffset;
   vec2 pix1 = (CharBoxN.xw + vec2 (0.01, 0.01)) * VPScale;
   vec2 pix2 = (CharBoxN.zy + vec2 (0.01, 0.01)) * VPScale;
   gl_Position = vec4 (pix1.x - 1, 1 - pix1.y, pix2.x - 1, 1 - pix2.y);
}
