#version 330

uniform vec4 DrawColor;
uniform float LineWidth;

in float gDist;

void main (void) {
   float d = abs (gDist) / LineWidth;
   float a = exp2 (-2 * d * d);
   gl_FragColor = vec4 (DrawColor.rgb, a);
}