#version 330

uniform vec4 DrawColor;
uniform float PointSize;

in vec2 gSTCoord;
      
void main (void) {
   float radius = PointSize / 2;
   float dist = distance (gSTCoord, vec2 (0, 0));
   float a = 1 - smoothstep (radius - 1, radius, dist);
   gl_FragColor = vec4 (DrawColor.rgb, a);
}
