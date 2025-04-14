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
   vec2 xy0 = (Xfm * vec4 (VertexPos, 0, 1)).xy;   // xy0 now in clip space
   xy0 = floor (xy0 / VPScale + vec2 (0.5, 0.5));  // now in integer pixel coordinates
   xy0 = (xy0 + CharBoxN.xy) * VPScale;             // Now black in clip space
   vec2 xy1 = xy0 + vCellSize * VPScale;
   gl_Position = vec4 (xy0, xy1);
}
