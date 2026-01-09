#version 330

uniform sampler2DRect FontTexture;
uniform vec4 DrawColor;

flat in ivec2 gCellSize;
flat in int gTexOffset;
in vec2 gTexCoord;
layout (location = 0) out vec4 FragColor;

void main () {
   int x = int (gTexCoord.x);
   int y = int (gTexCoord.y);
   int offset = y * gCellSize.x + x + gTexOffset;
   ivec2 st = ivec2 (offset % 8192, offset / 8192);
   float r = texelFetch (FontTexture, st).r;
   if (r < 0.001) discard;
   FragColor = vec4 (DrawColor.rgb, r);
}
