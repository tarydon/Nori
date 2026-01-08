#version 330

uniform mat4 Xfm;
uniform vec2 VPScale;

layout (location = 0) in vec3 VertexPos;
layout (location = 1) in ivec4 CharBoxN;
layout (location = 2) in int TexOffset;

out ivec2 vCellSize;
out int vTexOffset;

void main () {
   vCellSize = ivec2 (CharBoxN.z - CharBoxN.x, CharBoxN.w - CharBoxN.y);
   vTexOffset = TexOffset;
   vec2 xyref = (Xfm * vec4 (VertexPos, 1)).xy;        // xy0 now in clip space
   xyref = floor (xyref / VPScale);                    // now in pixel coordinates
   xyref = xyref + vec2 (0.01, 0.01);                  // delta to avoid truncation errors
   vec2 xy0 = (xyref + CharBoxN.xy); 
   vec2 xy1 = (xyref + CharBoxN.zw);
   gl_Position = vec4 (xy0 * VPScale, xy1 * VPScale);
}
