#version 150

uniform vec4 DrawColor;
uniform sampler2D FontTexture;

in vec2 gTexCoord;

void main (void) {
   gl_FragColor = vec4 (DrawColor.rgb, texture2D (FontTexture, gTexCoord).r);   
}
