#version 330

uniform mat4 Xfm;
uniform vec2 VPScale;

layout (location = 0) in vec2 VertexPos;
layout (location = 1) in ivec4 CharBoxN;
layout (location = 2) in int TexOffset;

out ivec2 vCellSize;
out int vTexOffset;

void main () {
   vCellSize = ivec2 (CharBoxN.z - CharBoxN.x, CharBoxN.w - CharBoxN.y);
   vTexOffset = TexOffset;
   vec2 xyref = (Xfm * vec4 (VertexPos, 0, 1)).xy;     // xy0 now in clip space
   xyref = floor (xyref / VPScale);
   xyref = xyref + vec2 (0.01, 0.01);
   xyref *= VPScale;
   vec2 pix1 = CharBoxN.xw * VPScale;
   vec2 pix2 = CharBoxN.zy * VPScale;
   gl_Position = vec4 (pix1.x + xyref.x, -pix1.y + xyref.y,
                       pix2.x + xyref.x, -pix2.y + xyref.y);
}
