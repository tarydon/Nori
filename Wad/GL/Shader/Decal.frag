#version 150

uniform sampler2D DecalTexture;

in vec2 vTexCoord;
in float vLighting;
out vec4 gFragColor;

void main () {
   vec4 color = texture2D (DecalTexture, vTexCoord);
   if (color.a < 0.05) discard;
   color = vec4 (color.rgb * vLighting, color.a);
   gFragColor = color;
}
