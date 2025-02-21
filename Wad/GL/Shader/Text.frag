#version 330

uniform sampler2DRect FontTexture;
uniform vec4 DrawColor;

flat in ivec2 gCellSize;
flat in int gTexOffset;
in vec2 gTexCoord;

void main () {
   int x = int (gTexCoord.x);
   int y = int (gTexCoord.y);
   int offset = y * gCellSize.x + x + gTexOffset;
   ivec2 st = ivec2 (offset % 8192, offset / 8192);
   float r = texelFetch (FontTexture, st).r;
   gl_FragColor = vec4 (DrawColor.rgb, r);
}
