#version 150

uniform vec4 DrawColor;
uniform sampler2D FontTexture;

in vec2 gTexCoord;
out vec4 gFragColor;

void main (void) {
   gFragColor = vec4 (DrawColor.rgb, texture2D (FontTexture, gTexCoord).r);   
}
