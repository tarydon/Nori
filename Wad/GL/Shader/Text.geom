#version 150

uniform vec2 VPScale;

layout (points) in;
layout (triangle_strip, max_vertices = 4) out;

in int vCharacter[1];
in vec2 vOffset[1];
in float vScale[1];
out vec2 gTexCoord;

const vec2 CellSize = vec2 (0.0625, 0.1302083);

void main (void) {
   vec2 invScale = 1 / VPScale;
   vec4 pt3d = gl_in[0].gl_Position;
   vec2 pt = invScale * pt3d.xy + vOffset[0];
   vec2 rSize = vec2 (16 * vScale[0], 32 * vScale[0]);

   int letter = vCharacter[0] - 32;
   int row = letter / 16, col = letter % 16;
   float S0 = CellSize.x * col, T0 = CellSize.y * row;
   float S1 = S0 + CellSize.x, T1 = T0 + CellSize.y;

   gl_Position = vec4 (VPScale * pt, pt3d.zw);
   gTexCoord = vec2 (S0, 1 - T0);
   EmitVertex (); 

   gl_Position = vec4 (VPScale * (pt + vec2 (rSize.x, 0)), pt3d.zw);
   gTexCoord = vec2 (S1, 1 - T0);
   EmitVertex (); 

   gl_Position = vec4 (VPScale * (pt + vec2 (0, rSize.y)), pt3d.zw);
   gTexCoord = vec2 (S0, 1 - T1);
   EmitVertex ();

   gl_Position = vec4 (VPScale * (pt + vec2 (rSize.x, rSize.y)), pt3d.zw);
   gTexCoord = vec2 (S1, 1 - T1);
   EmitVertex (); 
   EndPrimitive ();
}
