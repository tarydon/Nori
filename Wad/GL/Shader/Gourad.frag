#version 150

uniform vec4 DrawColor;

in vec4 vLightIntensity;
out vec4 gFragColor;

void main () {
   gFragColor = vec4 (vLightIntensity.rgb, DrawColor.a);
}
