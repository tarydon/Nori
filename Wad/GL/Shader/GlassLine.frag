#version 330

uniform vec4 DrawColor;
uniform float LineWidth;

in float gDist;
out vec4 gFragColor;

void main (void) {
   ivec2 coord = ivec2 (gl_FragCoord.xy - 0.5);
   if (fract ((coord.x + coord.y) / 2.0) < 0.5) discard;    
   float d = abs (gDist) / LineWidth;
   float a = exp2 (-2 * d * d);
   gFragColor = vec4 (DrawColor.rgb, a);
}